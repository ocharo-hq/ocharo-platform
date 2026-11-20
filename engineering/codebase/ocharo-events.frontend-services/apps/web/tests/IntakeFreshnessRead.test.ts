import { defineComponent, h, shallowRef } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiFailureFactory } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import type { IntakeFreshnessModel } from '@wow-two-beta/fe-events/contracts';
import { toEventsFailure, type EventsClient } from '@wow-two-beta/fe-events/kit';

import { useIntakeFreshness } from '@/application/events/UseEvents';

let wrapper: VueWrapper | undefined;
let state: ReturnType<typeof useIntakeFreshness>;

const model = (hours: number): IntakeFreshnessModel => ({
  lastReceivedAtUtc: new Date(Date.now() - hours * 3_600_000).toISOString(),
  failingSources: 0,
});
const clientAnswering = (answer: () => unknown) =>
  ({ getIntakeFreshness: vi.fn(async () => answer()) }) as unknown as EventsClient & {
    getIntakeFreshness: ReturnType<typeof vi.fn>;
  };

/** A component that makes the read, as the frame does, over a client that can change. */
function mountRead(source: () => EventsClient | null) {
  wrapper = mount(
    defineComponent({
      setup() {
        state = useIntakeFreshness(source, 'ocharo-fashion');
        return () => h('div');
      },
    }),
  );
}

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date', 'setInterval', 'clearInterval'] });
  vi.setSystemTime(new Date(2026, 9, 8, 10, 0));
});
afterEach(() => {
  wrapper?.unmount();
  wrapper = undefined;
  vi.useRealTimers();
});

describe('intake freshness read (E24)', () => {
  it('reads the catalog once on mount and keeps the answer and the instant it was read', async () => {
    const client = clientAnswering(() => ResultExtensions.ok(model(3)));
    mountRead(() => client);
    await flushPromises();

    expect(client.getIntakeFreshness).toHaveBeenCalledTimes(1);
    expect(client.getIntakeFreshness).toHaveBeenCalledWith('ocharo-fashion');
    expect(state.freshness.value).toEqual(model(3));
    expect(state.now.value).toBe(Date.now());
  });

  it('reads again every five minutes and when the tab is back in view, never twice for one event', async () => {
    const client = clientAnswering(() => ResultExtensions.ok(model(3)));
    mountRead(() => client);
    await flushPromises();

    window.dispatchEvent(new Event('focus'));
    await flushPromises();
    expect(client.getIntakeFreshness).toHaveBeenCalledTimes(2);

    await vi.advanceTimersByTimeAsync(5 * 60 * 1000);
    expect(client.getIntakeFreshness).toHaveBeenCalledTimes(3);
  });

  it('leaves the answer null when the read fails, as for a person outside the catalog’s organization', async () => {
    const client = clientAnswering(() =>
      ResultExtensions.fail(toEventsFailure(ApiFailureFactory.create('http', { status: 404, headers: new Headers() }))),
    );
    mountRead(() => client);
    await flushPromises();

    expect(client.getIntakeFreshness).toHaveBeenCalledTimes(1);
    expect(state.freshness.value).toBeNull();
  });

  it('reads nothing while there is no client, and reads once one arrives', async () => {
    const client = clientAnswering(() => ResultExtensions.ok(model(3)));
    const source = shallowRef<EventsClient | null>(null);
    mountRead(() => source.value);
    await flushPromises();
    expect(state.freshness.value).toBeNull();

    source.value = client;
    await flushPromises();
    expect(client.getIntakeFreshness).toHaveBeenCalledTimes(1);
    expect(state.freshness.value).toEqual(model(3));

    source.value = null;
    await flushPromises();
    expect(state.freshness.value).toBeNull();
  });

  it('stops reading when its component is gone', async () => {
    const client = clientAnswering(() => ResultExtensions.ok(model(3)));
    mountRead(() => client);
    await flushPromises();
    wrapper!.unmount();
    wrapper = undefined;

    window.dispatchEvent(new Event('focus'));
    await vi.advanceTimersByTimeAsync(10 * 60 * 1000);
    expect(client.getIntakeFreshness).toHaveBeenCalledTimes(1);
  });
});
