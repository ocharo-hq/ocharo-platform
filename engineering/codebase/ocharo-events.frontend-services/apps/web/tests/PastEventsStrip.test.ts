import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiFailureFactory } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import type { TrackedEventModel, TrackingModel } from '@wow-two-beta/fe-events/contracts';
import { toEventsFailure, type EventsClient } from '@wow-two-beta/fe-events/kit';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import { ocharoWorkflow } from '@/domain/events/Events';
import PastEventsStrip from '@/presentation/events/PastEventsStrip.vue';

const ORG = '11111111-1111-4111-8111-111111111111';
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
let client: { listTracking: ReturnType<typeof vi.fn>; putTracking: ReturnType<typeof vi.fn> };

beforeEach(() => {
  useUiLocale().setUiLocale('en');
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

async function render(canTrack = true) {
  wrapper = mount(PastEventsStrip, {
    props: {
      client: client as unknown as EventsClient,
      catalog: 'ocharo-fashion',
      today: '2026-10-08',
      stages: ocharoWorkflow().stages,
      canTrack,
      eventHref: (id: string) => `/organizations/${ORG}/events/${id}`,
    },
    attachTo: document.body,
  });
  await flushPromises();
}
const rows = () => wrapper!.findAll('[data-test="past-open-item"]');

describe('past events not closed (OE12)', () => {
  it('lists an ended event Ocharo confirmed, asking Events by stage and a start no later than yesterday', async () => {
    await render();

    expect(client.listTracking).toHaveBeenCalledTimes(1);
    expect(client.listTracking).toHaveBeenCalledWith('ocharo-fashion', {
      stages: ['shortlisted', 'applying', 'applied', 'confirmed'],
      to: '2026-10-07',
      includeUndated: false,
      limit: 50,
    });
    expect(wrapper!.get('#past-open-title').text()).toBe('Past, not closed');
    expect(rows()).toHaveLength(1);
    expect(rows()[0]!.text()).toContain('Central Asia Textile Expo');
    expect(rows()[0]!.text()).toContain('Confirmed');
    expect(rows()[0]!.get('a').attributes('href')).toBe(`/organizations/${ORG}/events/${ENDED}`);
  });

  it('closes it as Attended with one tracking write that keeps the rest of its tracking, and it leaves the strip', async () => {
    await render();

    await rows()[0]!.get('[data-test="past-open-attended"]').trigger('click');
    await flushPromises();

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
    expect(wrapper!.emitted('changed')).toHaveLength(1);
    expect(wrapper!.find('[data-test="past-open"]').exists()).toBe(false);
    expect(client.listTracking).toHaveBeenCalledTimes(1);
  });

  it('closes it as Skipped too', async () => {
    await render();

    await rows()[0]!.get('[data-test="past-open-skipped"]').trigger('click');
    await flushPromises();

    expect(client.putTracking.mock.calls[0]![2]).toMatchObject({ stage: 'skipped', expectedRevision: 5 });
  });

  it('reads the list again and says so when the event changed since it was read', async () => {
    client.putTracking.mockResolvedValueOnce(
      ResultExtensions.fail(toEventsFailure(ApiFailureFactory.create('http', { status: 409, headers: new Headers() }))),
    );
    await render();

    await rows()[0]!.get('[data-test="past-open-attended"]').trigger('click');
    await flushPromises();

    expect(client.listTracking).toHaveBeenCalledTimes(2);
    expect(wrapper!.get('[data-test="past-open-changed"]').text()).toBe(
      'An event changed since the list was read, so the list was reloaded.',
    );
    expect(wrapper!.emitted('changed')).toBeUndefined();
  });

  it('shows nothing when no ended event waits to be closed, and offers a viewer no close actions', async () => {
    client.listTracking.mockResolvedValue(ResultExtensions.ok({ items: [page()[1], page()[2]], nextCursor: null }));
    await render();
    expect(wrapper!.find('[data-test="past-open"]').exists()).toBe(false);
    wrapper!.unmount();

    client.listTracking.mockResolvedValue(ResultExtensions.ok({ items: page(), nextCursor: null }));
    await render(false);
    expect(rows()).toHaveLength(1);
    expect(wrapper!.find('[data-test="past-open-attended"]').exists()).toBe(false);
  });

  it('tells how many events wait, as the list reads and as each one is closed', async () => {
    await render();
    expect(wrapper!.emitted('count')?.at(-1)).toEqual([1]);

    await rows()[0]!.get('[data-test="past-open-attended"]').trigger('click');
    await flushPromises();
    expect(wrapper!.emitted('count')?.at(-1)).toEqual([0]);
    wrapper!.unmount();

    client.listTracking.mockResolvedValue(ResultExtensions.ok({ items: [page()[1], page()[2]], nextCursor: null }));
    await render();
    expect(wrapper!.emitted('count')?.at(-1)).toEqual([0]);
  });

  it('opens the event inside the page on a plain click', async () => {
    await render();

    await rows()[0]!.get('a').trigger('click', { button: 0 });

    expect(wrapper!.emitted('open')).toEqual([[ENDED]]);
  });
});
