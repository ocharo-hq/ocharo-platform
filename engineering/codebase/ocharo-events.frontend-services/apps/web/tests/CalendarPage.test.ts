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
const item = (
  n: number,
  title: string,
  starts: object,
  stage: string | null = null,
  ends?: object,
  country?: string,
) => ({
  event: {
    id: id(n),
    catalog: 'ocharo-fashion',
    facts: {
      title: { en: title },
      kind: 'trade-fair',
      starts,
      ...(ends ? { ends } : {}),
      ...(country ? { place: { country } } : {}),
    },
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

  it('titles a block in English, whatever other languages the event carries', async () => {
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
    expect(blocks('Неделя моды')).toHaveLength(0);
  });
});

describe('calendar scope pills', () => {
  /** Two national events, one in Central Asia, two international (Russia and France) and one with no country. */
  const scoped = () =>
    page([
      item(1, 'Tashkent Fashion Week', dayOf('2026-10-12'), 'shortlisted', undefined, 'UZ'),
      item(2, 'Bukhara Craft Fair', dayOf('2026-10-13'), null, undefined, 'UZ'),
      item(3, 'Almaty Fashion Days', dayOf('2026-10-14'), null, undefined, 'KZ'),
      item(4, 'Moscow Textile Show', dayOf('2026-10-15'), null, undefined, 'RU'),
      item(5, 'Paris Showroom', dayOf('2026-10-16'), null, undefined, 'FR'),
      item(6, 'Pop-up With No Country', dayOf('2026-10-17')),
      item(
        7,
        'Month Only In Uzbekistan',
        { value: '2026-10', precision: 'month', certainty: 'confirmed' },
        null,
        undefined,
        'UZ',
      ),
    ]);
  const group = () => wrapper!.get('[data-test="calendar-scopes"]');
  const pills = () => group().findAll('button');
  const pill = (label: string) => pills().find((button) => button.text().startsWith(label))!;
  const drawn = () =>
    (wrapper!.getComponent(EventCalendarViewer).props('events') as readonly EventCalendarViewerEvent[])
      .map((block) => block.title)
      .sort();
  const everything = [
    'Almaty Fashion Days',
    'Bukhara Craft Fair',
    'Moscow Textile Show',
    'Paris Showroom',
    'Pop-up With No Country',
    'Tashkent Fashion Week',
  ];

  it('offers National, Central Asia and International above the calendar, each with the count of events it draws', async () => {
    answers = [scoped];
    await render();

    expect(group().attributes('aria-label')).toBe('Scope');
    expect(pills().map((button) => button.text())).toEqual(['National2', 'Central Asia1', 'International2']);
    expect(pills().map((button) => button.attributes('aria-pressed'))).toEqual(['false', 'false', 'false']);
    // The pills come first: before the calendar in the page.
    const order = wrapper!.html();
    expect(order.indexOf('data-test="calendar-scopes"')).toBeLessThan(order.indexOf('data-test="calendar"'));
  });

  it('shows every event, those with no country included, while no pill is chosen', async () => {
    answers = [scoped];
    await render();

    expect(drawn()).toEqual(everything);
    expect(router.currentRoute.value.query.scope).toBeUndefined();
  });

  it('shows only the events of a chosen scope, keeps the counts of the others, and reads nothing again', async () => {
    answers = [scoped];
    await render();

    await pill('National').trigger('click');
    await flushPromises();

    expect(drawn()).toEqual(['Bukhara Craft Fair', 'Tashkent Fashion Week']);
    expect(pill('National').attributes('aria-pressed')).toBe('true');
    expect(pills().map((button) => button.text())).toEqual(['National2', 'Central Asia1', 'International2']);
    expect(reads).toHaveLength(1);
  });

  it('adds the events of a second pill, and shows a third scope on its own', async () => {
    answers = [scoped];
    await render();

    await pill('National').trigger('click');
    await pill('Central Asia').trigger('click');
    await flushPromises();
    expect(drawn()).toEqual(['Almaty Fashion Days', 'Bukhara Craft Fair', 'Tashkent Fashion Week']);

    await pill('National').trigger('click');
    await pill('Central Asia').trigger('click');
    await pill('International').trigger('click');
    await flushPromises();
    expect(drawn()).toEqual(['Moscow Textile Show', 'Paris Showroom']);
  });

  it('shows all events again once the chosen pills are cleared', async () => {
    answers = [scoped];
    await render();

    await pill('International').trigger('click');
    await flushPromises();
    expect(drawn()).toEqual(['Moscow Textile Show', 'Paris Showroom']);

    await pill('International').trigger('click');
    await flushPromises();
    expect(drawn()).toEqual(everything);
    expect(router.currentRoute.value.query.scope).toBeUndefined();
  });

  it('keeps the choice in the address as ?scope=, in the regions’ order, and keeps the other query', async () => {
    answers = [scoped];
    router = await eventsRouter(`/organizations/${ORG}/calendar?tab=x`);
    await render();

    await pill('Central Asia').trigger('click');
    await pill('National').trigger('click');
    await flushPromises();

    expect(router.currentRoute.value.query.scope).toBe('national,central-asia');
    expect(router.currentRoute.value.query.tab).toBe('x');
    expect(router.currentRoute.value.path).toBe(`/organizations/${ORG}/calendar`);
  });

  it('opens on the scopes the address names, and ignores a key that is not a scope', async () => {
    answers = [scoped];
    router = await eventsRouter(`/organizations/${ORG}/calendar?scope=central-asia,international,local`);
    await render();

    expect(pills().map((button) => button.attributes('aria-pressed'))).toEqual(['false', 'true', 'true']);
    expect(drawn()).toEqual(['Almaty Fashion Days', 'Moscow Textile Show', 'Paris Showroom']);
  });

  it('follows the address when it changes, as the back button does', async () => {
    answers = [scoped];
    await render();
    expect(drawn()).toEqual(everything);

    await router.replace({ query: { scope: 'central-asia' } });
    await flushPromises();
    expect(pills().map((button) => button.attributes('aria-pressed'))).toEqual(['false', 'true', 'false']);
    expect(drawn()).toEqual(['Almaty Fashion Days']);

    await router.replace({ query: {} });
    await flushPromises();
    expect(drawn()).toEqual(everything);
  });

  it('shows every event for an address whose scope names no region', async () => {
    answers = [scoped];
    router = await eventsRouter(`/organizations/${ORG}/calendar?scope=regional`);
    await render();

    expect(pills().map((button) => button.attributes('aria-pressed'))).toEqual(['false', 'false', 'false']);
    expect(drawn()).toEqual(everything);
  });

  it('counts only what the loaded window holds: an empty window reads 0 in each scope, once it has answered', async () => {
    await render();

    expect(pills().map((button) => button.text())).toEqual(['National0', 'Central Asia0', 'International0']);
  });

  it('shows the pills without counts until the first read answers', async () => {
    let release: (response: Response) => void = () => {};
    answers = [() => new Promise<Response>((resolve) => (release = resolve))];
    await render();

    expect(pills().map((button) => button.text())).toEqual(['National', 'Central Asia', 'International']);

    release(scoped());
    await flushPromises();
    expect(pills().map((button) => button.text())).toEqual(['National2', 'Central Asia1', 'International2']);
  });
});
