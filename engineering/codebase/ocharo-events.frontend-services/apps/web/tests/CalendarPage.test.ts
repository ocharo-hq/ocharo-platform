import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { EventCalendarViewer, type EventCalendarViewerEvent } from '@wow-two-beta/fe/presentation/display';

import { calendarEventCap } from '@/application/events/UseEventsCalendar';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { calendarToneColors } from '@/domain/events/EventsCalendar';
import CalendarPage from '@/presentation/events/pages/CalendarPage.vue';
import { eventsRouter } from './helpers/EventsRouter';

const ORG = '11111111-1111-4111-8111-111111111111';
const MIRROR = `/api/organizations/${ORG}/events-api/api/events/v1`;
const CATALOG = `${MIRROR}/catalogs/ocharo-fashion`;
const at = '2026-10-01T00:00:00Z';

const id = (n: number) => `00000000-0000-4000-8000-${String(n).padStart(12, '0')}`;
const item = (n: number, title: string, starts: object, stage: string | null = null, ends?: object) => ({
  event: {
    id: id(n),
    catalog: 'ocharo-fashion',
    facts: { title: { en: title }, kind: 'trade-fair', starts, ...(ends ? { ends } : {}) },
    review: 'published',
    revision: 1,
    createdAtUtc: at,
    updatedAtUtc: at,
  },
  tracking: stage
    ? { id: `t${n}`, eventId: id(n), stage, priority: 'normal', revision: 1, createdAtUtc: at, updatedAtUtc: at }
    : null,
});
const dayOf = (value: string) => ({ value, precision: 'day', certainty: 'confirmed' });

let wrapper: VueWrapper | undefined;
let router: Awaited<ReturnType<typeof eventsRouter>>;
/** The tracking list reads the calendar made, in order. */
let reads: URL[] = [];
/** What each read answers, by its position; the last answer repeats. */
let answers: Array<() => Promise<Response> | Response> = [];

const ok = (data: unknown) => Response.json({ data });
const page = (items: unknown[], nextCursor: string | null = null) => ok({ items, nextCursor });

beforeEach(async () => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(2026, 9, 8, 10, 0));
  useUiLocale().setUiLocale('en');
  reads = [];
  answers = [() => page([])];
  router = await eventsRouter(`/organizations/${ORG}/calendar`);
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL) => {
      const request = input instanceof Request ? input : null;
      const url = new URL(request ? request.url : String(input), 'https://localhost');
      if (url.pathname !== `${CATALOG}/tracking`) return Response.json({ title: 'Not found' }, { status: 404 });
      reads.push(url);
      return (answers[Math.min(reads.length - 1, answers.length - 1)] ?? answers[0]!)();
    }),
  );
});
afterEach(() => {
  wrapper?.unmount();
  wrapper = undefined;
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

async function render() {
  wrapper = mount(CalendarPage, {
    props: { organizationId: ORG },
    global: { plugins: [router] },
    attachTo: document.body,
  });
  await flushPromises();
}
const blocks = (title: string) =>
  wrapper!.findAll('button').filter((button) => button.text().replace('• ', '') === title);
const control = (label: string) => wrapper!.findAll('button').find((button) => button.text() === label)!;

describe('calendar place', () => {
  it('titles the page "Calendar" and offers the SDK calendar on the current month', async () => {
    await render();

    expect(wrapper!.findAll('h1')).toHaveLength(1);
    expect(wrapper!.get('h1').text()).toBe('Calendar');
    expect(wrapper!.get('[data-test="calendar"] h3').text()).toBe('October 2026');
    // Every view the viewer has is offered, the default switcher's.
    expect(wrapper!.get('[data-test="calendar"] [role="group"]').findAll('button').length).toBeGreaterThanOrEqual(4);
    expect(control('month').attributes('aria-pressed')).toBe('true');
  });

  it('reads the visible month through the feed’s calls: no undated events, closed stages left out, paged by 100', async () => {
    await render();

    expect(reads).toHaveLength(1);
    const query = reads[0]!.searchParams;
    // October 2026 with a week before and two after: the month grid draws 42 days from the 1st's week.
    expect(query.get('from')).toBe('2026-09-24');
    expect(query.get('to')).toBe('2026-11-14');
    expect(query.get('includeUndated')).toBe('false');
    expect(query.getAll('excludeStages')).toEqual(['skipped', 'rejected']);
    expect(query.get('limit')).toBe('100');
    expect(query.has('cursor')).toBe(false);
  });

  it('draws an event with a known day on every day it spans, and leaves out one known only to its month', async () => {
    answers = [
      () =>
        page([
          item(1, 'Tashkent Fashion Week', dayOf('2026-10-12'), 'shortlisted', dayOf('2026-10-14')),
          item(2, 'Silk Road Trade Fair', { value: '2026-10', precision: 'month', certainty: 'confirmed' }),
        ]),
    ];
    await render();

    // One bar spans the three days within its week (SDK 0.0.54 draws a multi-day event as a bar); the month-only event has no day and is not drawn.
    const bar = blocks('Tashkent Fashion Week');
    expect(bar).toHaveLength(1);
    expect(bar[0]!.attributes('data-bar-span')).toBe('3');
    expect(blocks('Silk Road Trade Fair')).toHaveLength(0);
  });

  it('colours a block by Ocharo’s stage with theme variables', async () => {
    answers = [
      () =>
        page([
          item(1, 'Pursued', dayOf('2026-10-12'), 'applying'),
          item(2, 'Confirmed', dayOf('2026-10-13'), 'confirmed'),
          item(3, 'Untracked', dayOf('2026-10-14')),
        ]),
    ];
    await render();

    // The colour reaches the viewer as the block's own; a DOM style would drop the `color-mix` of a tone in happy-dom.
    const colours = Object.fromEntries(
      (wrapper!.getComponent(EventCalendarViewer).props('events') as readonly EventCalendarViewerEvent[]).map(
        (block) => [block.title, block.color],
      ),
    );
    expect(colours).toEqual({
      Pursued: calendarToneColors.pursuing,
      Confirmed: calendarToneColors.participating,
      Untracked: calendarToneColors.neutral,
    });
  });

  it('pages through the cursor until the window is complete', async () => {
    answers = [
      () => page([item(1, 'First page', dayOf('2026-10-12'))], 'cursor-1'),
      () => page([item(2, 'Second page', dayOf('2026-10-13'))]),
    ];
    await render();

    expect(reads).toHaveLength(2);
    expect(reads[1]!.searchParams.get('cursor')).toBe('cursor-1');
    expect(reads[1]!.searchParams.get('from')).toBe(reads[0]!.searchParams.get('from'));
    expect(blocks('First page')).toHaveLength(1);
    expect(blocks('Second page')).toHaveLength(1);
  });

  it('stops paging at the cap of 1000 events for a window', async () => {
    let next = 0;
    answers = [
      () => {
        const items = Array.from({ length: 100 }, () => item(++next, `Event ${next}`, dayOf('2026-10-12')));
        return page(items, `cursor-${next}`);
      },
    ];
    await render();

    expect(calendarEventCap).toBe(1000);
    expect(reads).toHaveLength(10);
    expect(next).toBe(1000);
  });

  it('reads again for the next month, and not for a view change inside the days already read', async () => {
    await render();
    expect(reads).toHaveLength(1);

    await control('week').trigger('click');
    await control('day').trigger('click');
    await flushPromises();
    expect(reads).toHaveLength(1);

    await control('month').trigger('click');
    await wrapper!.get('button[aria-label="Next"]').trigger('click');
    await flushPromises();

    expect(wrapper!.get('[data-test="calendar"] h3').text()).toBe('November 2026');
    expect(reads).toHaveLength(2);
    expect(reads[1]!.searchParams.get('from')).toBe('2026-10-25');
    expect(reads[1]!.searchParams.get('to')).toBe('2026-12-14');
  });

  it('keeps the events on screen while the next window loads', async () => {
    let release: (response: Response) => void = () => {};
    answers = [
      () => page([item(1, 'Tashkent Fashion Week', dayOf('2026-10-12'))]),
      () => new Promise<Response>((resolve) => (release = resolve)),
    ];
    await render();
    expect(blocks('Tashkent Fashion Week')).toHaveLength(1);

    await wrapper!.get('button[aria-label="Previous"]').trigger('click');
    await flushPromises();
    expect(wrapper!.get('[data-test="calendar"]').attributes('aria-busy')).toBe('true');

    release(page([]));
    await flushPromises();
    expect(wrapper!.get('[data-test="calendar"]').attributes('aria-busy')).toBeUndefined();
  });

  it('opens an event on click at its address in the feed', async () => {
    answers = [() => page([item(1, 'Tashkent Fashion Week', dayOf('2026-10-12'))])];
    await render();

    await blocks('Tashkent Fashion Week')[0]!.trigger('click');
    await flushPromises();

    expect(router.currentRoute.value.path).toBe(`/organizations/${ORG}/events/${id(1)}`);
  });

  it('shows a loading state until the first read answers', async () => {
    let release: (response: Response) => void = () => {};
    answers = [() => new Promise<Response>((resolve) => (release = resolve))];
    await render();

    expect(wrapper!.get('[data-test="calendar-loading"]').attributes('aria-busy')).toBe('true');
    expect(wrapper!.get('[data-test="calendar-loading"] [role="status"]').text()).toBe('Loading…');
    expect(wrapper!.find('[data-test="calendar"]').exists()).toBe(false);

    release(page([]));
    await flushPromises();
    expect(wrapper!.find('[data-test="calendar-loading"]').exists()).toBe(false);
    expect(wrapper!.find('[data-test="calendar"]').exists()).toBe(true);
  });

  it('says why a read failed and reads again on retry', async () => {
    answers = [
      () => Response.json({ title: 'Down' }, { status: 500 }),
      () => page([item(1, 'Tashkent Fashion Week', dayOf('2026-10-12'))]),
    ];
    await render();

    const alert = wrapper!.get('[role="alert"]');
    expect(alert.text()).not.toBe('');
    expect(blocks('Tashkent Fashion Week')).toHaveLength(0);

    await alert.get('button').trigger('click');
    await flushPromises();

    expect(reads).toHaveLength(2);
    expect(wrapper!.find('[role="alert"]').exists()).toBe(false);
    expect(blocks('Tashkent Fashion Week')).toHaveLength(1);
  });

  it('titles a block in the interface language', async () => {
    answers = [
      () =>
        page([
          {
            ...item(1, 'x', dayOf('2026-10-12')),
            event: {
              ...item(1, 'x', dayOf('2026-10-12')).event,
              facts: {
                title: { en: 'Fashion Week', 'ru-Cyrl': 'Неделя моды' },
                kind: 'runway',
                starts: dayOf('2026-10-12'),
              },
            },
          },
        ]),
    ];
    await render();
    expect(blocks('Fashion Week')).toHaveLength(1);

    useUiLocale().setUiLocale('ru');
    await flushPromises();
    expect(blocks('Неделя моды')).toHaveLength(1);
  });
});
