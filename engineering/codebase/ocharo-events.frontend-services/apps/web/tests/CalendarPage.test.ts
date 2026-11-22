import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { EventCalendarViewer, type EventCalendarViewerEvent } from '@wow-two-beta/fe/presentation/display';
import { FacetBar, type FacetChoiceDefinition, type FacetDefinition } from '@wow-two-beta/fe/presentation/forms';

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

/** What an event can say beyond its title, day, stage and country: its kind (a trade fair by default), city, organizer and the sources that reported it. */
interface Extra {
  kind?: string;
  city?: string;
  organizer?: string;
  sources?: string[];
}
const item = (
  n: number,
  title: string,
  starts: object,
  stage: string | null = null,
  ends?: object,
  country?: string,
  extra: Extra = {},
) => ({
  event: {
    id: id(n),
    catalog: 'ocharo-fashion',
    facts: {
      title: { en: title },
      kind: extra.kind ?? 'trade-fair',
      starts,
      ...(ends ? { ends } : {}),
      ...(country || extra.city
        ? { place: { ...(country ? { country } : {}), ...(extra.city ? { city: extra.city } : {}) } }
        : {}),
      ...(extra.organizer ? { organizer: { name: extra.organizer } } : {}),
    },
    ...(extra.sources
      ? {
          sources: extra.sources.map((name, index) => ({
            kind: 'scfd',
            id: `s${n}-${index}`,
            name,
            firstSeenAtUtc: at,
            lastSeenAtUtc: at,
          })),
        }
      : {}),
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
    expect(
      wrapper!.get('[data-test="calendar"] [role="group"][aria-label="View"]').findAll('button').length,
    ).toBeGreaterThanOrEqual(4);
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

describe('calendar filter card', () => {
  /**
   * Six events the window draws and one it does not (a month with no day):
   *   1  UZ  runway       Tashkent  shortlisted  Afisha      organizer Ocharo Studio
   *   2  UZ  market       Bukhara   candidate    Afisha and Timepad
   *   3  KZ  runway       Almaty    applying     Timepad
   *   4  RU  trade fair   Moscow    candidate    no source named
   *   5  FR  trade fair   Paris     candidate    no source named
   *   6  no country, trade fair, no city
   */
  const scoped = () =>
    page([
      item(1, 'Tashkent Fashion Week', dayOf('2026-10-12'), 'shortlisted', undefined, 'UZ', {
        kind: 'runway',
        city: 'Tashkent',
        organizer: 'Ocharo Studio',
        sources: ['Afisha'],
      }),
      item(2, 'Bukhara Craft Fair', dayOf('2026-10-13'), null, undefined, 'UZ', {
        kind: 'market',
        city: 'Bukhara',
        sources: ['Afisha', 'Timepad'],
      }),
      item(3, 'Almaty Fashion Days', dayOf('2026-10-14'), 'applying', undefined, 'KZ', {
        kind: 'runway',
        city: 'Almaty',
        sources: ['Timepad'],
      }),
      item(4, 'Moscow Textile Show', dayOf('2026-10-15'), null, undefined, 'RU', { city: 'Moscow' }),
      item(5, 'Paris Showroom', dayOf('2026-10-16'), null, undefined, 'FR', { city: 'Paris' }),
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
  const card = () => wrapper!.get('[data-test="event-filters"]');
  const group = (label: string) => card().get(`[role="group"][aria-label="${label}"]`);
  const pills = (label: string) => group(label).findAll('button');
  const pill = (label: string, text: string) => pills(label).find((button) => button.text().startsWith(text))!;
  const labelled = (label: string) => pills(label).map((button) => button.text());
  const matchPill = () => card().get('[data-facet-match]').text();
  const search = () => card().get('input[aria-label="Search events"]');
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
  const query = () => router.currentRoute.value.query;

  it('is one card under the heading and above the viewer, never inside the viewer’s toolbar', async () => {
    answers = [scoped];
    await render();

    expect(wrapper!.findAll('[data-test="event-filters"]')).toHaveLength(1);
    const section = wrapper!.get('section').element;
    expect(section.children[0]!.tagName).toBe('H1');
    expect(section.children[1]).toBe(card().element);
    const calendar = wrapper!.get('[data-test="calendar"]');
    expect(card().element.compareDocumentPosition(calendar.element) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(calendar.element.contains(card().element)).toBe(false);
    expect(calendar.find('[data-test="calendar-scopes"]').exists()).toBe(false);
    expect(wrapper!.find('[data-test="calendar-scopes"]').exists()).toBe(false);
  });

  it('is a Card with radius 2xl and a padding that is smaller on a phone', async () => {
    answers = [scoped];
    await render();

    const classes = card().classes();
    expect(classes).toContain('rounded-2xl');
    expect(classes).toContain('p-3');
    expect(classes).toContain('sm:p-5');
  });

  it('leaves the viewer its own toolbar: today, previous, next, the title and the view switcher', async () => {
    answers = [scoped];
    await render();

    const calendar = wrapper!.get('[data-test="calendar"]');
    expect(calendar.get('h3').text()).toBe('October 2026');
    expect(calendar.find('button[aria-label="Previous"]').exists()).toBe(true);
    expect(calendar.find('button[aria-label="Next"]').exists()).toBe(true);
    expect(calendar.get('[role="group"][aria-label="View"]').findAll('button').length).toBeGreaterThanOrEqual(4);
    expect(calendar.findAll('button').some((button) => button.text() === 'Today')).toBe(true);
  });

  it('offers Region and Kind as pills with the count of events each would match, and the rest behind Filters', async () => {
    answers = [scoped];
    await render();

    // The region is one choice, as on the feed: "All" leads it, with no count of its own.
    expect(labelled('Region')).toEqual(['All', 'National2', 'Central Asia1', 'International2']);
    expect(labelled('Kind')).toEqual([
      'Runway show2',
      'Trade fair3',
      'Market1',
      'Exhibition0',
      'Competition0',
      'Festival0',
      'Conference0',
      'Other0',
    ]);
    expect(pills('Region').map((button) => button.attributes('aria-pressed'))).toEqual([
      'false',
      'false',
      'false',
      'false',
    ]);
    expect(
      card()
        .findAll('button')
        .some((button) => button.text().startsWith('Filters')),
    ).toBe(true);
    expect(card().find('[role="group"][aria-label="Stage"]').exists()).toBe(false);
  });

  it('reads "n of m" for the loaded window, with the search in the kit’s words', async () => {
    answers = [scoped];
    await render();

    expect(matchPill()).toBe('6 of 6');
    expect(search().attributes('placeholder')).toBe('Title, organizer or city');
  });

  it('shows every event, those with no country included, while nothing is chosen', async () => {
    answers = [scoped];
    await render();

    expect(drawn()).toEqual(everything);
    expect(query().kinds).toBeUndefined();
    expect(query().regions).toBeUndefined();
  });

  it('narrows the drawn events to a chosen kind and writes kinds to the address, reading nothing again', async () => {
    answers = [scoped];
    await render();

    await pill('Kind', 'Market').trigger('click');
    await flushPromises();

    expect(drawn()).toEqual(['Bukhara Craft Fair']);
    expect(query().kinds).toEqual(['market']);
    expect(pill('Kind', 'Market').attributes('aria-pressed')).toBe('true');
    expect(matchPill()).toBe('1 of 6');
    expect(reads).toHaveLength(1);

    await pill('Kind', 'Runway show').trigger('click');
    await flushPromises();

    expect(drawn()).toEqual(['Almaty Fashion Days', 'Bukhara Craft Fair', 'Tashkent Fashion Week']);
    // Several kinds are a repeated key, as the feed writes them.
    expect(query().kinds).toEqual(['market', 'runway']);
    expect(matchPill()).toBe('3 of 6');
  });

  it('shows only the events of a chosen region, keeps the region counts, and counts the kinds within it', async () => {
    answers = [scoped];
    await render();

    await pill('Region', 'National').trigger('click');
    await flushPromises();

    expect(drawn()).toEqual(['Bukhara Craft Fair', 'Tashkent Fashion Week']);
    expect(pill('Region', 'National').attributes('aria-pressed')).toBe('true');
    expect(query().regions).toBe('national');
    // A facet counts with the others’ choices applied, not its own: the regions stay, the kinds follow the region.
    expect(labelled('Region')).toEqual(['All', 'National2', 'Central Asia1', 'International2']);
    expect(labelled('Kind').slice(0, 3)).toEqual(['Runway show1', 'Trade fair0', 'Market1']);
    expect(reads).toHaveLength(1);
  });

  it('takes one region at a time, as the feed does: choosing another replaces it', async () => {
    answers = [scoped];
    await render();

    await pill('Region', 'National').trigger('click');
    await pill('Region', 'Central Asia').trigger('click');
    await flushPromises();
    expect(drawn()).toEqual(['Almaty Fashion Days']);
    expect(pills('Region').map((button) => button.attributes('aria-pressed'))).toEqual([
      'false',
      'false',
      'true',
      'false',
    ]);
    expect(query().regions).toBe('central-asia');

    await pill('Region', 'International').trigger('click');
    await flushPromises();
    // The pop-up with no country belongs to no region, so choosing one leaves it out.
    expect(drawn()).toEqual(['Moscow Textile Show', 'Paris Showroom']);
  });

  it('shows every region again for the All pill, which is no choice and leaves the key out of the address', async () => {
    answers = [scoped];
    await render();

    await pill('Region', 'National').trigger('click');
    await flushPromises();
    expect(query().regions).toBe('national');

    await pill('Region', 'All').trigger('click');
    await flushPromises();
    expect(drawn()).toEqual(everything);
    expect(query().regions).toBeUndefined();
  });

  it('shows all events again once the chosen pills are cleared, and leaves the key out of the address', async () => {
    answers = [scoped];
    await render();

    await pill('Region', 'International').trigger('click');
    await flushPromises();
    expect(drawn()).toEqual(['Moscow Textile Show', 'Paris Showroom']);

    await pill('Region', 'International').trigger('click');
    await flushPromises();
    expect(drawn()).toEqual(everything);
    expect(query().regions).toBeUndefined();
  });

  it('keeps the choices in the address under the feed’s keys, and the other query keys, the feed’s own included', async () => {
    answers = [scoped];
    router = await eventsRouter(`/organizations/${ORG}/calendar?tab=x&when=past&sort=added`);
    await render();

    await pill('Region', 'Central Asia').trigger('click');
    await pill('Kind', 'Runway show').trigger('click');
    await search().setValue('fashion');
    await flushPromises();

    expect(query().regions).toBe('central-asia');
    expect(query().kinds).toEqual(['runway']);
    expect(query().q).toBe('fashion');
    expect(query().tab).toBe('x');
    // The date preset and the sort are the feed's: the calendar leaves them in the address, unread.
    expect(query().when).toBe('past');
    expect(query().sort).toBe('added');
    expect(Object.keys(query()).sort()).toEqual(['kinds', 'q', 'regions', 'sort', 'tab', 'when']);
    expect(router.currentRoute.value.path).toBe(`/organizations/${ORG}/calendar`);
  });

  it('opens on the choices the address names, the region as the kit reads it, and ignores a region that is none', async () => {
    answers = [scoped];
    router = await eventsRouter(`/organizations/${ORG}/calendar?regions=international&kinds=trade-fair`);
    await render();

    expect(pills('Region').map((button) => button.attributes('aria-pressed'))).toEqual([
      'false',
      'false',
      'false',
      'true',
    ]);
    expect(pill('Kind', 'Trade fair').attributes('aria-pressed')).toBe('true');
    expect(drawn()).toEqual(['Moscow Textile Show', 'Paris Showroom']);
  });

  it('follows the address when it changes, as the back button does', async () => {
    answers = [scoped];
    await render();
    expect(drawn()).toEqual(everything);

    await router.replace({ query: { regions: 'central-asia', q: 'almaty' } });
    await flushPromises();
    expect(pills('Region').map((button) => button.attributes('aria-pressed'))).toEqual([
      'false',
      'false',
      'true',
      'false',
    ]);
    expect((search().element as HTMLInputElement).value).toBe('almaty');
    expect(drawn()).toEqual(['Almaty Fashion Days']);

    await router.replace({ query: {} });
    await flushPromises();
    expect((search().element as HTMLInputElement).value).toBe('');
    expect(drawn()).toEqual(everything);
  });

  it('shows every event for an address whose region is none of Ocharo’s', async () => {
    answers = [scoped];
    router = await eventsRouter(`/organizations/${ORG}/calendar?regions=regional`);
    await render();

    expect(pills('Region').map((button) => button.attributes('aria-pressed'))).toEqual([
      'false',
      'false',
      'false',
      'false',
    ]);
    expect(drawn()).toEqual(everything);
  });

  it('searches the titles, the cities and the organizers, whatever the case, and writes the text to the address', async () => {
    answers = [scoped];
    await render();

    await search().setValue('FASHION');
    await flushPromises();
    expect(drawn()).toEqual(['Almaty Fashion Days', 'Tashkent Fashion Week']);
    expect(matchPill()).toBe('2 of 6');
    expect(query().q).toBe('FASHION');

    await search().setValue('moscow');
    await flushPromises();
    expect(drawn()).toEqual(['Moscow Textile Show']);

    await search().setValue('ocharo studio');
    await flushPromises();
    expect(drawn()).toEqual(['Tashkent Fashion Week']);

    await search().setValue('');
    await flushPromises();
    expect(drawn()).toEqual(everything);
    expect(query().q).toBeUndefined();
  });

  it('counts the pills with the search applied', async () => {
    answers = [scoped];
    await render();

    await search().setValue('fashion');
    await flushPromises();

    expect(labelled('Region')).toEqual(['All', 'National1', 'Central Asia1', 'International0']);
  });

  it('shows the choices behind Filters as removable chips, and removing one widens the events', async () => {
    answers = [scoped];
    router = await eventsRouter(
      `/organizations/${ORG}/calendar?stages=candidate&cities=bukhara&cities=paris&sources=afisha`,
    );
    await render();

    expect(drawn()).toEqual(['Bukhara Craft Fair']);
    const chips = () => card().get('[data-facet-chips]');
    expect(chips().text()).toContain('Stage: Candidate');
    expect(chips().text()).toContain('City: Bukhara');
    expect(chips().text()).toContain('City: Paris');
    expect(chips().text()).toContain('Source: Afisha');

    await card().get('button[aria-label="Remove City: Bukhara"]').trigger('click');
    await flushPromises();
    expect(query().cities).toEqual(['paris']);
    expect(drawn()).toEqual([]);

    await card().get('button[aria-label="Remove Source: Afisha"]').trigger('click');
    await flushPromises();
    expect(query().sources).toBeUndefined();
    expect(drawn()).toEqual(['Paris Showroom']);
  });

  it('narrows by a stage, a city or a source from the address', async () => {
    answers = [scoped];
    router = await eventsRouter(`/organizations/${ORG}/calendar?stages=applying`);
    await render();
    expect(drawn()).toEqual(['Almaty Fashion Days']);

    await router.replace({ query: { cities: 'tashkent' } });
    await flushPromises();
    expect(drawn()).toEqual(['Tashkent Fashion Week']);

    await router.replace({ query: { sources: 'timepad' } });
    await flushPromises();
    expect(drawn()).toEqual(['Almaty Fashion Days', 'Bukhara Craft Fair']);

    await router.replace({ query: { sources: 'curated' } });
    await flushPromises();
    expect(drawn()).toEqual(['Moscow Textile Show', 'Paris Showroom', 'Pop-up With No Country']);
  });

  it('clears the choices and the search together with Clear all', async () => {
    answers = [scoped];
    router = await eventsRouter(`/organizations/${ORG}/calendar?regions=national&kinds=market&q=bukhara`);
    await render();
    expect(drawn()).toEqual(['Bukhara Craft Fair']);

    const clear = card()
      .findAll('button')
      .find((button) => button.text() === 'Clear all')!;
    await clear.trigger('click');
    await flushPromises();

    expect(drawn()).toEqual(everything);
    expect(query().regions).toBeUndefined();
    expect(query().kinds).toBeUndefined();
    expect(query().q).toBeUndefined();
    expect(matchPill()).toBe('6 of 6');
  });

  it('counts only what the loaded window holds: an empty window reads 0 of 0, once it has answered', async () => {
    await render();

    expect(labelled('Region')).toEqual(['All', 'National0', 'Central Asia0', 'International0']);
    expect(matchPill()).toBe('0 of 0');
  });

  it('shows the card with no counts and no match pill during the bare loading skeleton, counted once the calendar mounts', async () => {
    let release: (response: Response) => void = () => {};
    answers = [() => new Promise<Response>((resolve) => (release = resolve))];
    await render();

    expect(wrapper!.find('[data-test="calendar"]').exists()).toBe(false);
    expect(labelled('Region')).toEqual(['All', 'National', 'Central Asia', 'International']);
    expect(card().get('[data-facet-match]').text()).toBe('');

    release(scoped());
    await flushPromises();
    expect(labelled('Region')).toEqual(['All', 'National2', 'Central Asia1', 'International2']);
    expect(matchPill()).toBe('6 of 6');
  });

  it('reads the address the feed writes and narrows its events: the same keys, the feed’s own `when` and `sort` ignored', async () => {
    answers = [scoped];
    router = await eventsRouter(
      `/organizations/${ORG}/calendar?regions=national&kinds=runway&kinds=market&stages=shortlisted&cities=tashkent&sources=afisha&q=fashion&when=past&sort=deadline`,
    );
    await render();

    // The feed’s `kinds=runway&kinds=market` and the rest are one set of choices here: only Tashkent Fashion Week is left.
    expect(drawn()).toEqual(['Tashkent Fashion Week']);
    expect(pill('Region', 'National').attributes('aria-pressed')).toBe('true');
    expect(pills('Kind').map((button) => button.attributes('aria-pressed'))).toEqual([
      'true',
      'false',
      'true',
      'false',
      'false',
      'false',
      'false',
      'false',
    ]);
    expect((search().element as HTMLInputElement).value).toBe('fashion');
    const chips = card().get('[data-facet-chips]').text();
    for (const chosen of ['Stage: Shortlisted', 'City: Tashkent', 'Source: Afisha']) expect(chips).toContain(chosen);
    expect(matchPill()).toBe('1 of 6');
    // It reads the window it shows, whatever the address says of dates or order.
    expect(reads).toHaveLength(1);
    expect(reads[0]!.searchParams.has('sort')).toBe(false);
  });

  it('counts the window it has read, client-side, and never asks the server for facet counts', async () => {
    answers = [scoped];
    await render();

    expect(labelled('Region')).toEqual(['All', 'National2', 'Central Asia1', 'International2']);
    expect(reads.every((read) => read.pathname === `${CATALOG}/tracking`)).toBe(true);
  });

  const barFacet = (key: string) =>
    (wrapper!.getComponent(FacetBar).props('facets') as readonly FacetDefinition[]).find(
      (facet) => facet.key === key,
    ) as FacetChoiceDefinition;

  it('gives the region and kind pills their one-line hints, and the feed’s calmer timing, and no other facet', async () => {
    answers = [scoped];
    await render();

    const bar = wrapper!.getComponent(FacetBar);
    expect(bar.props('hintOpenDelay')).toBe(5000);
    expect(bar.props('hintFadeDuration')).toBe(200);
    expect(barFacet('scope').options.map((option) => option.hint?.description)).toEqual([
      undefined,
      'Events in Uzbekistan.',
      'Events in Kazakhstan, Kyrgyzstan, Tajikistan and Turkmenistan.',
      'Events anywhere else in the world.',
    ]);
    expect(barFacet('kind').options.every((option) => option.hint !== undefined)).toBe(true);
    for (const key of ['stage', 'city', 'source'])
      expect(barFacet(key).options.some((option) => option.hint !== undefined)).toBe(false);
  });

  it('greys a pill that matches nothing and is not chosen, and keeps it pressable', async () => {
    answers = [scoped];
    await render();

    expect(pill('Kind', 'Exhibition').attributes('data-empty')).toBeDefined();
    expect(pill('Kind', 'Exhibition').attributes('disabled')).toBeUndefined();
    expect(pill('Kind', 'Market').attributes('data-empty')).toBeUndefined();
    expect(pill('Region', 'All').attributes('data-empty')).toBeUndefined();
  });

  it('lays the bar out as the feed does on a phone, with the kit’s class, and leaves no mount for the feed’s controls', async () => {
    answers = [scoped];
    await render();

    expect(wrapper!.getComponent(FacetBar).classes()).toContain('max-sm:[&_[data-facet-inline]]:flex-nowrap');
    expect(card().find('[data-test="event-filter-actions"]').exists()).toBe(false);
  });
});
