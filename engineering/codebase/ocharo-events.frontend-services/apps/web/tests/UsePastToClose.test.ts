import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { defineComponent, h } from 'vue';
import { ApiFailureFactory } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import type { TrackedEventModel, TrackingModel } from '@wow-two-beta/fe-events/contracts';
import { toEventsFailure, type EventsClient } from '@wow-two-beta/fe-events/kit';

import { usePastToClose } from '@/application/events/UsePastToClose';

const ENDED = '22222222-2222-4222-8222-222222222222';
const RUNNING = '33333333-3333-4333-8333-333333333333';
const UNTRACKED = '44444444-4444-4444-8444-444444444444';
const at = '2026-09-01T00:00:00Z';

const tracking = (eventId: string, stage: string, revision = 5): TrackingModel => ({
  id: `tracking-${eventId}`,
  eventId,
  stage,
  priority: 'high',
  assignee: null,
  notes: 'Stand 14, hall B.',
  fields: { cost: 'Stand from 300 USD', sourceUrl: 'https://listings.example.test/expo' },
  references: [],
  revision,
  createdAtUtc: at,
  updatedAtUtc: at,
});
const item = (id: string, title: string, starts: string, ends: string, stage: string | null): TrackedEventModel => ({
  event: {
    id,
    catalog: 'ocharo-fashion',
    facts: {
      title: { und: title },
      kind: 'trade-fair',
      starts: { value: starts, precision: 'day' },
      ends: { value: ends, precision: 'day' },
    },
    review: 'published',
    revision: 1,
    createdAtUtc: at,
    updatedAtUtc: at,
  } as unknown as TrackedEventModel['event'],
  tracking: stage ? tracking(id, stage) : null,
});

/** A trade expo Ocharo confirmed, which ended on 22 September; one still running; one Ocharo never tracked. */
const page = () => [
  item(ENDED, 'Central Asia Textile Expo', '2026-09-20', '2026-09-22', 'confirmed'),
  item(RUNNING, 'Tashkent Fashion Week', '2026-10-06', '2026-10-10', 'applied'),
  item(UNTRACKED, 'Silk Road Crafts Fair', '2026-09-10', '2026-09-12', null),
];

let wrapper: VueWrapper | undefined;
let past: ReturnType<typeof usePastToClose>;
let closed: ReturnType<typeof vi.fn<() => void>>;
let client: { listTracking: ReturnType<typeof vi.fn>; putTracking: ReturnType<typeof vi.fn> };

beforeEach(() => {
  closed = vi.fn<() => void>();
  client = {
    listTracking: vi.fn(async () => ResultExtensions.ok({ items: page(), nextCursor: null })),
    putTracking: vi.fn(async (_catalog: string, eventId: string, spec: { stage: string; expectedRevision: number }) =>
      ResultExtensions.ok({ ...tracking(eventId, spec.stage, spec.expectedRevision + 1) }),
    ),
  };
});
afterEach(() => {
  wrapper?.unmount();
  wrapper = undefined;
});

/** Runs the hook in a component, as the page does, for the day `2026-10-08`. */
async function run() {
  wrapper = mount(
    defineComponent({
      setup() {
        past = usePastToClose({
          client: () => client as unknown as EventsClient,
          catalog: () => 'ocharo-fashion',
          today: () => '2026-10-08',
          closed,
        });
        return () => h('div');
      },
    }),
  );
  await flushPromises();
}

describe('past events not closed (OE12)', () => {
  it('lists an ended event Ocharo confirmed, asking Events by stage and a start no later than yesterday', async () => {
    await run();

    expect(client.listTracking).toHaveBeenCalledTimes(1);
    expect(client.listTracking).toHaveBeenCalledWith('ocharo-fashion', {
      stages: ['shortlisted', 'applying', 'applied', 'confirmed'],
      to: '2026-10-07',
      includeUndated: false,
      limit: 50,
    });
    expect(past.items.value.map((entry) => entry.event.id)).toEqual([ENDED]);
    expect(past.count.value).toBe(1);
  });

  it('closes it as Attended with one tracking write that keeps the rest of its tracking, and it leaves the list', async () => {
    await run();

    await past.close(past.items.value[0]!, 'attended');

    expect(client.putTracking).toHaveBeenCalledTimes(1);
    expect(client.putTracking).toHaveBeenCalledWith('ocharo-fashion', ENDED, {
      stage: 'attended',
      priority: 'high',
      assignee: null,
      notes: 'Stand 14, hall B.',
      fields: { cost: 'Stand from 300 USD', sourceUrl: 'https://listings.example.test/expo' },
      references: [],
      expectedRevision: 5,
    });
    expect(closed).toHaveBeenCalledTimes(1);
    expect(past.items.value).toEqual([]);
    expect(past.count.value).toBe(0);
    expect(past.writing.value).toBeNull();
    expect(client.listTracking).toHaveBeenCalledTimes(1);
  });

  it('closes it as Skipped too', async () => {
    await run();

    await past.close(past.items.value[0]!, 'skipped');

    expect(client.putTracking.mock.calls[0]![2]).toMatchObject({ stage: 'skipped', expectedRevision: 5 });
  });

  it('reads the list again and says so when the event changed since it was read', async () => {
    client.putTracking.mockResolvedValueOnce(
      ResultExtensions.fail(toEventsFailure(ApiFailureFactory.create('http', { status: 409, headers: new Headers() }))),
    );
    await run();

    await past.close(past.items.value[0]!, 'attended');

    expect(client.listTracking).toHaveBeenCalledTimes(2);
    expect(past.changedNotice.value).toBe(true);
    expect(closed).not.toHaveBeenCalled();
  });

  it('keeps the list and reports the failure when a write fails otherwise', async () => {
    client.putTracking.mockResolvedValueOnce(
      ResultExtensions.fail(toEventsFailure(ApiFailureFactory.create('http', { status: 500, headers: new Headers() }))),
    );
    await run();

    await past.close(past.items.value[0]!, 'attended');

    expect(past.failure.value).not.toBeNull();
    expect(past.items.value).toHaveLength(1);
    expect(past.count.value).toBe(1);
    expect(client.listTracking).toHaveBeenCalledTimes(1);
  });

  it('counts none when no ended event waits to be closed', async () => {
    client.listTracking.mockResolvedValue(ResultExtensions.ok({ items: [page()[1], page()[2]], nextCursor: null }));
    await run();

    expect(past.items.value).toEqual([]);
    expect(past.count.value).toBe(0);
  });

  it('reads again on request, so a write elsewhere on the page reaches the count', async () => {
    client.listTracking.mockResolvedValue(ResultExtensions.ok({ items: [page()[1]], nextCursor: null }));
    await run();
    expect(past.count.value).toBe(0);

    client.listTracking.mockResolvedValue(ResultExtensions.ok({ items: page(), nextCursor: null }));
    await past.reload();

    expect(past.count.value).toBe(1);
  });
});
