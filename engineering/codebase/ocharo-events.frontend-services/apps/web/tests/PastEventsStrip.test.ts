import { mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { ApiFailureFactory } from '@wow-two-beta/fe/foundation/http';
import type { TrackedEventModel, TrackingModel } from '@wow-two-beta/fe-events/contracts';
import { toEventsFailure } from '@wow-two-beta/fe-events/kit';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import { ocharoWorkflow } from '@/domain/events/Events';
import PastEventsStrip from '@/presentation/events/PastEventsStrip.vue';

const ORG = '11111111-1111-4111-8111-111111111111';
const ENDED = '22222222-2222-4222-8222-222222222222';
const at = '2026-09-01T00:00:00Z';

const tracking = (eventId: string, stage: string): TrackingModel => ({
  id: `tracking-${eventId}`,
  eventId,
  stage,
  priority: 'high',
  assignee: null,
  notes: 'Stand 14, hall B.',
  references: [],
  revision: 5,
  createdAtUtc: at,
  updatedAtUtc: at,
});
/** A trade expo Ocharo confirmed, which ended on 22 September. */
const ended = (): TrackedEventModel => ({
  event: {
    id: ENDED,
    catalog: 'ocharo-fashion',
    facts: {
      title: { und: 'Central Asia Textile Expo' },
      kind: 'trade-fair',
      starts: { value: '2026-09-20', precision: 'day' },
      ends: { value: '2026-09-22', precision: 'day' },
    },
    review: 'published',
    revision: 1,
    createdAtUtc: at,
    updatedAtUtc: at,
  } as unknown as TrackedEventModel['event'],
  tracking: tracking(ENDED, 'confirmed'),
});

let wrapper: VueWrapper | undefined;

beforeEach(() => {
  useUiLocale().setUiLocale('en');
});
afterEach(() => {
  wrapper?.unmount();
  wrapper = undefined;
});

function render(props: Partial<InstanceType<typeof PastEventsStrip>['$props']> = {}) {
  wrapper = mount(PastEventsStrip, {
    props: {
      items: [ended()],
      failure: null,
      writing: null,
      changedNotice: false,
      stages: ocharoWorkflow().stages,
      canTrack: true,
      eventHref: (id: string) => `/organizations/${ORG}/events/${id}`,
      ...props,
    },
    attachTo: document.body,
  });
  return wrapper;
}
const rows = () => wrapper!.findAll('[data-test="past-open-item"]');

describe('past events strip (OE12)', () => {
  it('lists an ended event with its dates and stage, linked to its page, under "Past, not closed"', () => {
    render();

    expect(wrapper!.get('#past-open-title').text()).toBe('Past, not closed');
    expect(rows()).toHaveLength(1);
    expect(rows()[0]!.text()).toContain('Central Asia Textile Expo');
    expect(rows()[0]!.text()).toContain('Confirmed');
    expect(rows()[0]!.get('a').attributes('href')).toBe(`/organizations/${ORG}/events/${ENDED}`);
  });

  it('asks to close the event as Attended or as Skipped, with the event', async () => {
    render();

    await rows()[0]!.get('[data-test="past-open-attended"]').trigger('click');
    await rows()[0]!.get('[data-test="past-open-skipped"]').trigger('click');

    expect(wrapper!.emitted('close')).toEqual([
      [ended(), 'attended'],
      [ended(), 'skipped'],
    ]);
  });

  it('shows nothing while no event waits, and offers a viewer the list without close actions', () => {
    render({ items: [] });
    expect(wrapper!.find('[data-test="past-open"]').exists()).toBe(false);
    wrapper!.unmount();

    render({ canTrack: false });
    expect(rows()).toHaveLength(1);
    expect(wrapper!.find('[data-test="past-open-attended"]').exists()).toBe(false);
  });

  it('says so when the event changed since the list was read', () => {
    render({ changedNotice: true });

    expect(wrapper!.get('[data-test="past-open-changed"]').text()).toBe(
      'An event changed since the list was read, so the list was reloaded.',
    );
  });

  it('shows a failure with a retry, even with no event left to list', async () => {
    render({
      items: [],
      failure: toEventsFailure(ApiFailureFactory.create('http', { status: 500, headers: new Headers() })),
    });

    expect(wrapper!.find('[data-test="past-open"]').exists()).toBe(true);
    const retry = wrapper!.findAll('button').find((button) => /reload/i.test(button.text()));
    expect(retry).toBeDefined();
    await retry!.trigger('click');
    expect(wrapper!.emitted('retry')).toHaveLength(1);
  });

  it('opens the event inside the page on a plain click', async () => {
    render();

    await rows()[0]!.get('a').trigger('click', { button: 0 });

    expect(wrapper!.emitted('open')).toEqual([[ENDED]]);
  });
});
