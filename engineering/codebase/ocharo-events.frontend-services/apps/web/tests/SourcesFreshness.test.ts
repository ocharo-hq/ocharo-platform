import { ref, shallowRef } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { IntakeFreshnessModel } from '@wow-two-beta/fe-events/contracts';

import { SourcesFreshnessKey } from '@/application/events/UseSourcesFreshness';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import SourcesFreshness from '@/presentation/events/SourcesFreshness.vue';

let wrapper: VueWrapper | undefined;
/** The state the frame offers: the last freshness read and the instant it was made. */
const freshness = shallowRef<IntakeFreshnessModel | null>(null);
const now = ref(Date.now());

/** The instant `hours` before the test's clock, as the wire carries it. */
const hoursAgo = (hours: number) => new Date(Date.now() - hours * 3_600_000).toISOString();
const answers = (model: IntakeFreshnessModel | null) => {
  freshness.value = model;
  now.value = Date.now();
};
const line = () => wrapper!.find('[data-test="sources-freshness"]');

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(2026, 9, 8, 10, 0));
  useUiLocale().setUiLocale('en');
  freshness.value = null;
});
afterEach(() => {
  wrapper?.unmount();
  wrapper = undefined;
  vi.useRealTimers();
});

async function render(compact = false, offered = true) {
  wrapper = mount(SourcesFreshness, {
    props: { compact },
    global: { provide: offered ? { [SourcesFreshnessKey as symbol]: { freshness, now } } : {} },
    attachTo: document.body,
  });
  await flushPromises();
}

describe('sources freshness (E24)', () => {
  it('says when the sources last delivered, with no warning while the last record is recent and nothing fails', async () => {
    answers({ lastReceivedAtUtc: hoursAgo(3), newestObservedAtUtc: hoursAgo(4), failingSources: 0 });
    await render();

    expect(line().text()).toBe('Sources updated 3 hours ago');
    expect(line().attributes('data-state')).toBe('fresh');
    expect(line().classes()).not.toContain('text-warning');
    expect(line().attributes('role')).toBeUndefined();
  });

  it('warns when the last record is more than 24 hours old', async () => {
    answers({ lastReceivedAtUtc: hoursAgo(50), newestObservedAtUtc: hoursAgo(52), failingSources: 0 });
    await render();

    expect(line().text()).toBe('Sources updated 2 days ago · No new records in over 24 hours.');
    expect(line().attributes('data-state')).toBe('warning');
    expect(line().classes()).toContain('text-warning');
    expect(line().attributes('role')).toBe('status');
  });

  it('does not warn at 24 hours, the last moment a record still counts as fresh', async () => {
    answers({ lastReceivedAtUtc: hoursAgo(24), failingSources: 0 });
    await render();

    expect(line().text()).toBe('Sources updated yesterday');
    expect(line().attributes('data-state')).toBe('fresh');
  });

  it('warns with how many sources fail, even when the last record is recent', async () => {
    answers({ lastReceivedAtUtc: hoursAgo(1), newestObservedAtUtc: hoursAgo(2), failingSources: 2 });
    await render();

    expect(line().text()).toBe('Sources updated 1 hour ago · Sources failing: 2');
    expect(line().attributes('data-state')).toBe('warning');
  });

  it('warns about a failing source before the catalog has received a record, with no time to give', async () => {
    answers({ lastReceivedAtUtc: null, newestObservedAtUtc: null, failingSources: 1 });
    await render();

    expect(line().text()).toBe('Sources failing: 1');
    expect(line().attributes('data-state')).toBe('warning');
  });

  it('shows nothing before the catalog has received a record and while no source fails', async () => {
    answers({ failingSources: 0 });
    await render();

    expect(line().exists()).toBe(false);
  });

  it('shows nothing, and no error of its own, before a read has answered or when it failed', async () => {
    answers(null);
    await render();

    expect(line().exists()).toBe(false);
    expect(wrapper!.find('[role="alert"]').exists()).toBe(false);
  });

  it('shows nothing outside the frame that makes the read', async () => {
    await render(false, false);

    expect(line().exists()).toBe(false);
  });

  it('follows a read that arrives after it was shown', async () => {
    answers({ lastReceivedAtUtc: hoursAgo(3), failingSources: 0 });
    await render();
    expect(line().text()).toBe('Sources updated 3 hours ago');

    answers({ lastReceivedAtUtc: hoursAgo(50), failingSources: 0 });
    await flushPromises();
    expect(line().attributes('data-state')).toBe('warning');
  });

  it('reads the age in the interface language', async () => {
    answers({ lastReceivedAtUtc: hoursAgo(3), failingSources: 0 });
    await render();
    expect(line().text()).toBe('Sources updated 3 hours ago');

    useUiLocale().setUiLocale('uz');
    await flushPromises();
    expect(line().text()).toBe('Manbalar 3 soat oldin yangilandi');

    useUiLocale().setUiLocale('ru');
    await flushPromises();
    expect(line().text()).toBe('Источники обновлены 3 часа назад');
  });
});

describe('sources freshness as the icon rail', () => {
  it('shows only an icon, with the full text as its tooltip and accessible name', async () => {
    answers({ lastReceivedAtUtc: hoursAgo(3), failingSources: 0 });
    await render(true);

    expect(line().attributes('data-compact')).toBeDefined();
    expect(line().text()).toBe('');
    const icon = line().get('[role="img"]');
    expect(icon.attributes('aria-label')).toBe('Sources updated 3 hours ago');
    expect(icon.attributes('title')).toBe('Sources updated 3 hours ago');
    expect(line().find('svg').exists()).toBe(true);
  });

  it('keeps the warning tone and role, naming the warning in the tooltip', async () => {
    answers({ lastReceivedAtUtc: hoursAgo(50), failingSources: 1 });
    await render(true);

    expect(line().attributes('data-state')).toBe('warning');
    expect(line().classes()).toContain('text-warning');
    expect(line().attributes('role')).toBe('status');
    expect(line().get('[role="img"]').attributes('aria-label')).toBe(
      'Sources updated 2 days ago · No new records in over 24 hours. · Sources failing: 1',
    );
  });

  it('shows nothing at all, icon included, while there is nothing to say', async () => {
    answers({ failingSources: 0 });
    await render(true);

    expect(line().exists()).toBe(false);
  });
});
