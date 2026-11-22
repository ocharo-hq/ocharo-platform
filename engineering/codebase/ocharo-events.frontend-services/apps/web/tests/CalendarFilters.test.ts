import { describe, expect, it } from 'vitest';
import type { TrackedEventModel } from '@wow-two-beta/fe-events/contracts';

import {
  allScopesValue,
  calendarFacetCounts,
  calendarFacetKeys,
  calendarSelection,
  facetValue,
  filterCalendarEvents,
  noCalendarFilters,
  readCalendarFilters,
  withCalendarSelection,
  writeCalendarFilters,
  type CalendarFacetKey,
  type CalendarFilters,
} from '@/domain/events/CalendarFilters';
import type { EventsCalendarOptions } from '@/domain/events/EventsCalendar';

const options: EventsCalendarOptions = { locale: 'en', zone: 'Asia/Tashkent' };
const at = '2026-10-01T00:00:00Z';

interface Fixture {
  title?: Record<string, string>;
  /** The start as a day (`2026-10-12`) or, for an event with no day, a month (`2026-10`). */
  starts?: string;
  stage?: string;
  kind?: string;
  country?: string;
  city?: string;
  organizer?: string;
  /** The names of the sources that reported the event. */
  sources?: string[];
}

/** An event as Events lists it, with the facts the filters read. */
function item(id: string, fixture: Fixture = {}): TrackedEventModel {
  const { starts = '2026-10-12', stage, kind, country, city, organizer, sources } = fixture;
  return {
    event: {
      id,
      catalog: 'ocharo-fashion',
      facts: {
        title: fixture.title ?? { en: `Event ${id}` },
        ...(kind ? { kind } : {}),
        starts: { value: starts, precision: starts.length > 7 ? 'day' : 'month', certainty: 'confirmed' },
        ...(country || city ? { place: { country, city } } : {}),
        ...(organizer ? { organizer: { name: organizer } } : {}),
      },
      ...(sources
        ? {
            sources: sources.map((name, n) => ({
              kind: 'scfd',
              id: `${id}-${n}`,
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
      ? { id: `t-${id}`, eventId: id, stage, priority: 'normal', revision: 1, createdAtUtc: at, updatedAtUtc: at }
      : null,
  } as unknown as TrackedEventModel;
}

/**
 * Six events the window draws, in this order, and two it does not (a closed stage, a month with no day):
 *   a  UZ  runway       Tashkent  shortlisted  Afisha
 *   b  UZ  market       Bukhara   candidate    Afisha and Timepad
 *   c  KZ  runway       Almaty    applying     Timepad
 *   d  RU  trade-fair   Moscow    confirmed    no source named: curated
 *   e  FR  trade-fair   Paris     candidate    AFISHA (the same source in capitals)
 *   f  no country, kind, city or source
 */
const events = [
  item('a', {
    title: { en: 'Tashkent Fashion Week' },
    country: 'UZ',
    kind: 'runway',
    city: 'Tashkent',
    organizer: 'Ocharo Studio',
    stage: 'shortlisted',
    sources: ['Afisha'],
  }),
  item('b', {
    title: { en: 'Bukhara Craft Fair' },
    country: 'UZ',
    kind: 'market',
    city: 'Bukhara',
    sources: ['Afisha', 'Timepad'],
  }),
  item('c', {
    title: { en: 'Almaty Fashion Days' },
    country: 'KZ',
    kind: 'runway',
    city: 'Almaty',
    stage: 'applying',
    sources: ['Timepad'],
  }),
  item('d', {
    title: { en: 'Moscow Textile Show' },
    country: 'RU',
    kind: 'trade-fair',
    city: 'Moscow',
    stage: 'confirmed',
  }),
  item('e', { title: { en: 'Paris Showroom' }, country: 'FR', kind: 'trade-fair', city: 'Paris', sources: ['AFISHA'] }),
  item('f', { title: { en: 'Pop-up With No Country' } }),
  item('closed', { country: 'UZ', kind: 'runway', city: 'Tashkent', stage: 'skipped' }),
  item('month', { country: 'UZ', kind: 'runway', city: 'Tashkent', starts: '2026-10' }),
];

const filtered = (filters: Partial<CalendarFilters>, items = events) =>
  filterCalendarEvents(items, options, { ...noCalendarFilters, ...filters }).map((entry) => entry.id);
const counted = (filters: Partial<CalendarFilters>, items = events) =>
  calendarFacetCounts(items, options, { ...noCalendarFilters, ...filters });
/** One facet's choices as `value: count`. */
const countsOf = (key: CalendarFacetKey, filters: Partial<CalendarFilters>, items = events) =>
  Object.fromEntries(
    counted(filters, items)
      .facets.find((facet) => facet.key === key)!
      .options.map((option) => [option.value, option.count]),
  );

describe('calendar filter values', () => {
  it('writes a city or source name trimmed and lowercase, as Events stores a city, so the feed’s value is the calendar’s', () => {
    expect(facetValue('Tashkent')).toBe('tashkent');
    expect(facetValue('  Nizhny Novgorod ')).toBe('nizhny novgorod');
    expect(facetValue('Tashkent, Uzbekistan')).toBe('tashkent, uzbekistan');
    expect(facetValue('AFISHA')).toBe('afisha');
    expect(facetValue('  ')).toBe('');
  });

  it('names the "All" region pill by a value that is no region', () => {
    expect(allScopesValue).toBe('all');
  });
});

describe('filtering the calendar events', () => {
  it('draws every event the window draws when nothing is chosen, those with no country included', () => {
    expect(filtered({})).toEqual(['a', 'b', 'c', 'd', 'e', 'f']);
  });

  it('keeps the events of a chosen region by their country, and never one with no country', () => {
    expect(filtered({ scope: ['national'] })).toEqual(['a', 'b']);
    expect(filtered({ scope: ['central-asia'] })).toEqual(['c']);
    expect(filtered({ scope: ['international'] })).toEqual(['d', 'e']);
  });

  it('keeps the events of a chosen kind, several at once, and none that has no kind', () => {
    expect(filtered({ kind: ['runway'] })).toEqual(['a', 'c']);
    expect(filtered({ kind: ['runway', 'market'] })).toEqual(['a', 'b', 'c']);
    expect(filtered({ kind: ['trade-fair'] })).toEqual(['d', 'e']);
  });

  it('keeps the events at a chosen stage, an event not tracked yet standing at the candidate stage', () => {
    expect(filtered({ stage: ['candidate'] })).toEqual(['b', 'e', 'f']);
    expect(filtered({ stage: ['shortlisted', 'applying'] })).toEqual(['a', 'c']);
    expect(filtered({ stage: ['confirmed'] })).toEqual(['d']);
  });

  it('never draws a closed stage, however it is chosen', () => {
    expect(filtered({ stage: ['skipped'] })).toEqual([]);
  });

  it('keeps the events in a chosen city, several at once, and none that has no city', () => {
    expect(filtered({ city: ['tashkent'] })).toEqual(['a']);
    expect(filtered({ city: ['tashkent', 'paris'] })).toEqual(['a', 'e']);
  });

  it('keeps the events a chosen source reported, one source in any spelling, and curated for those no source named', () => {
    expect(filtered({ source: ['afisha'] })).toEqual(['a', 'b', 'e']);
    expect(filtered({ source: ['timepad'] })).toEqual(['b', 'c']);
    expect(filtered({ source: ['curated'] })).toEqual(['d', 'f']);
    expect(filtered({ source: ['afisha', 'curated'] })).toEqual(['a', 'b', 'd', 'e', 'f']);
  });

  it('keeps an event that has every facet with a choice in one of its chosen values', () => {
    expect(filtered({ scope: ['international'], kind: ['trade-fair'] })).toEqual(['d', 'e']);
    expect(filtered({ scope: ['national'], kind: ['runway'] })).toEqual(['a']);
    expect(filtered({ scope: ['national'], source: ['timepad'] })).toEqual(['b']);
    expect(filtered({ scope: ['national'], kind: ['trade-fair'] })).toEqual([]);
  });

  it('searches the titles, the city and the organizer for the text, whatever the case', () => {
    expect(filtered({ search: 'fashion' })).toEqual(['a', 'c']);
    expect(filtered({ search: 'FASHION' })).toEqual(['a', 'c']);
    expect(filtered({ search: 'ashion we' })).toEqual(['a']);
    expect(filtered({ search: 'moscow' })).toEqual(['d']);
    expect(filtered({ search: 'ocharo' })).toEqual(['a']);
    expect(filtered({ search: 'nothing like this' })).toEqual([]);
  });

  it('searches a title in any language the event carries', () => {
    const silk = item('silk', { title: { en: 'Silk Fair', 'ru-Cyrl': 'Шёлковая ярмарка' } });

    expect(filtered({ search: 'шёлк' }, [silk])).toEqual(['silk']);
    expect(filtered({ search: 'silk' }, [silk])).toEqual(['silk']);
  });

  it('keeps every event for a blank search, and narrows by the search and the choices together', () => {
    expect(filtered({ search: '   ' })).toEqual(['a', 'b', 'c', 'd', 'e', 'f']);
    expect(filtered({ search: 'fashion', scope: ['national'] })).toEqual(['a']);
  });

  it('gives the blocks the calendar draws, in the order read', () => {
    const [entry] = filterCalendarEvents(events, options, { ...noCalendarFilters, kind: ['market'] });

    expect(entry).toMatchObject({ id: 'b', title: 'Bukhara Craft Fair', isAllDay: true });
  });
});

describe('counting the calendar facets', () => {
  it('lists the facets in the card’s order, with the events matching and the events the window draws', () => {
    const counts = counted({ kind: ['runway'] });

    expect(counts.facets.map((facet) => facet.key)).toEqual(['scope', 'kind', 'stage', 'city', 'source']);
    expect(calendarFacetKeys).toEqual(['scope', 'kind', 'stage', 'city', 'source']);
    expect(counts.match).toBe(2);
    // A closed stage and an event with no day are not drawn, so they are not counted.
    expect(counts.total).toBe(6);
    expect(counted({}).match).toBe(6);
  });

  it('offers the three regions with their labels, each with its events, whether or not it has any', () => {
    const scope = counted({}).facets[0]!;

    expect(scope.options).toEqual([
      { value: 'national', label: 'National', count: 2 },
      { value: 'central-asia', label: 'Central Asia', count: 1 },
      { value: 'international', label: 'International', count: 2 },
    ]);
    expect(counted({}, []).facets[0]!.options.map((option) => option.count)).toEqual([0, 0, 0]);
  });

  it('offers the catalog’s kinds in its order with their labels, zeros included', () => {
    const kind = counted({}).facets[1]!;

    expect(kind.options.map((option) => [option.value, option.label, option.count])).toEqual([
      ['runway', 'Runway show', 2],
      ['trade-fair', 'Trade fair', 2],
      ['market', 'Market', 1],
      ['exhibition', 'Exhibition', 0],
      ['competition', 'Competition', 0],
      ['festival', 'Festival', 0],
      ['conference', 'Conference', 0],
      ['other', 'Other', 0],
    ]);
  });

  it('offers the stages still open in Ocharo’s order with their labels, an event not tracked yet at Candidate', () => {
    const stage = counted({}).facets[2]!;

    expect(stage.options.map((option) => [option.value, option.label, option.count])).toEqual([
      ['candidate', 'Candidate', 3],
      ['shortlisted', 'Shortlisted', 1],
      ['applying', 'Applying', 1],
      ['applied', 'Applied', 0],
      ['confirmed', 'Confirmed', 1],
      ['attended', 'Attended', 0],
    ]);
  });

  it('offers the cities and sources the window holds, most events first then by name, each under its first spelling', () => {
    const { facets } = counted({});

    expect(facets[3]!.options.map((option) => [option.value, option.label, option.count])).toEqual([
      ['almaty', 'Almaty', 1],
      ['bukhara', 'Bukhara', 1],
      ['moscow', 'Moscow', 1],
      ['paris', 'Paris', 1],
      ['tashkent', 'Tashkent', 1],
    ]);
    expect(facets[4]!.options.map((option) => [option.value, option.label, option.count])).toEqual([
      ['afisha', 'Afisha', 3],
      ['curated', 'Curated', 2],
      ['timepad', 'Timepad', 2],
    ]);
  });

  it('counts each facet with every other facet’s choices applied, but not its own', () => {
    const filters = { kind: ['runway'], scope: ['national'] };

    // Kind ignores its own choice (runway) and keeps the region's: the national events are a runway and a market.
    expect(countsOf('kind', filters)).toMatchObject({ runway: 1, market: 1, 'trade-fair': 0 });
    // Region ignores its own choice and keeps the kind's: the runway events are one national and one in Central Asia.
    expect(countsOf('scope', filters)).toEqual({ national: 1, 'central-asia': 1, international: 0 });
    // The rest keep both choices: only the national runway event is left.
    expect(countsOf('stage', filters)).toMatchObject({ shortlisted: 1, candidate: 0, applying: 0 });
    expect(countsOf('city', filters)).toMatchObject({ tashkent: 1, almaty: 0, bukhara: 0 });
    expect(countsOf('source', filters)).toMatchObject({ afisha: 1, timepad: 0, curated: 0 });
  });

  it('does not change a facet’s counts when its own choices change', () => {
    const none = countsOf('kind', {});

    expect(countsOf('kind', { kind: ['runway'] })).toEqual(none);
    expect(countsOf('kind', { kind: ['runway', 'market'] })).toEqual(none);
    expect(counted({ kind: ['runway', 'market'] }).match).toBe(3);
  });

  it('applies the search to every facet’s counts', () => {
    const filters = { search: 'fashion' };

    expect(counted(filters).match).toBe(2);
    expect(countsOf('scope', filters)).toEqual({ national: 1, 'central-asia': 1, international: 0 });
    expect(countsOf('kind', filters)).toMatchObject({ runway: 2, 'trade-fair': 0, market: 0 });
    expect(counted(filters).total).toBe(6);
  });

  it('keeps the region semantics: an event with no country is in no region, so a chosen region leaves it out', () => {
    expect(countsOf('scope', {})).toEqual({ national: 2, 'central-asia': 1, international: 2 });
    expect(counted({ scope: ['international'] }).match).toBe(2);
    expect(counted({}).match).toBe(6);
  });

  it('adds a kind or stage the catalog does not list after the listed ones, with its key as its label', () => {
    const items = [item('x', { kind: 'webinar', stage: 'waitlisted' }), ...events];
    const { facets } = counted({}, items);

    expect(facets[1]!.options.at(-1)).toEqual({ value: 'webinar', label: 'webinar', count: 1 });
    expect(facets[2]!.options.at(-1)).toEqual({ value: 'waitlisted', label: 'waitlisted', count: 1 });
  });

  it('keeps a chosen value the window no longer holds, with no events, so the choice can be undone', () => {
    const { facets, match } = counted({ city: ['samarkand'], kind: ['festival-x'], source: ['olx'] });

    expect(match).toBe(0);
    expect(facets[3]!.options.at(-1)).toEqual({ value: 'samarkand', label: 'samarkand', count: 0 });
    expect(facets[1]!.options.at(-1)).toEqual({ value: 'festival-x', label: 'festival-x', count: 0 });
    expect(facets[4]!.options.at(-1)).toEqual({ value: 'olx', label: 'olx', count: 0 });
  });

  it('counts an empty window as nothing, with no city or source to offer', () => {
    const counts = counted({}, []);

    expect(counts.match).toBe(0);
    expect(counts.total).toBe(0);
    expect(counts.facets[3]!.options).toEqual([]);
    expect(counts.facets[4]!.options).toEqual([]);
    expect(counts.facets[1]!.options.every((option) => option.count === 0)).toBe(true);
  });
});

describe('the filters in an address', () => {
  it('reads every facet and the search from the kit’s keys, the city and source as lowercase values', () => {
    expect(
      readCalendarFilters({
        regions: 'central-asia',
        kinds: ['runway', 'market'],
        stages: 'applying',
        cities: ['Tashkent', 'Bukhara '],
        sources: 'Afisha',
        q: 'silk',
      }),
    ).toEqual({
      scope: ['central-asia'],
      kind: ['runway', 'market'],
      stage: ['applying'],
      city: ['tashkent', 'bukhara'],
      source: ['afisha'],
      search: 'silk',
    });
  });

  it('reads the region as the kit does: the first value, and only when it is one of Ocharo’s regions', () => {
    expect(readCalendarFilters({ regions: 'national' }).scope).toEqual(['national']);
    expect(readCalendarFilters({ regions: ['international', 'national'] }).scope).toEqual(['international']);
    expect(readCalendarFilters({ regions: ['bogus', 'national'] }).scope).toEqual([]);
    expect(readCalendarFilters({ regions: 'local' }).scope).toEqual([]);
    expect(readCalendarFilters({ regions: 'all' }).scope).toEqual([]);
  });

  it('knows nothing of the old keys, nor of the feed’s date preset and sort', () => {
    expect(
      readCalendarFilters({ scope: 'national', kind: 'runway', city: 'paris', when: 'past', sort: 'added' }),
    ).toEqual(noCalendarFilters);
  });

  it('reads no filters from a query that holds none', () => {
    expect(readCalendarFilters({})).toEqual(noCalendarFilters);
    expect(readCalendarFilters({ tab: 'x', kinds: null })).toEqual(noCalendarFilters);
  });

  it('writes the region as one value and the other facets as lists, and nothing for none', () => {
    const written = writeCalendarFilters({
      ...noCalendarFilters,
      scope: ['central-asia'],
      kind: ['market', 'runway'],
      city: ['Tashkent'],
      search: 'silk road',
    });

    expect(written).toEqual({
      regions: 'central-asia',
      kinds: ['market', 'runway'],
      stages: undefined,
      cities: ['tashkent'],
      sources: undefined,
      q: 'silk road',
    });
    expect(Object.values(writeCalendarFilters(noCalendarFilters)).every((value) => value === undefined)).toBe(true);
    expect(writeCalendarFilters({ ...noCalendarFilters, scope: ['bogus'] }).regions).toBeUndefined();
  });

  it('reads back what it writes', () => {
    const chosen: CalendarFilters = {
      scope: ['international'],
      kind: ['market', 'runway'],
      stage: ['applying'],
      city: ['tashkent'],
      source: ['afisha', 'curated'],
      search: 'silk road',
    };

    expect(readCalendarFilters(writeCalendarFilters(chosen))).toEqual(chosen);
  });
});

describe('the filters as the facet bar’s selection', () => {
  it('gives a key for each facet with a choice and none for the rest, the region as one value and the others as lists', () => {
    expect(calendarSelection({ ...noCalendarFilters, kind: ['runway'], scope: ['national'], search: 'silk' })).toEqual({
      scope: 'national',
      kind: ['runway'],
    });
    expect(calendarSelection(noCalendarFilters)).toEqual({});
  });

  it('takes the bar’s choices in place of the facets’, keeping the search', () => {
    const before: CalendarFilters = { ...noCalendarFilters, stage: ['applying'], search: 'silk' };

    expect(withCalendarSelection(before, { scope: 'international', kind: 'runway', city: ['Tashkent'] })).toEqual({
      scope: ['international'],
      kind: ['runway'],
      stage: [],
      city: ['tashkent'],
      source: [],
      search: 'silk',
    });
  });

  it('chooses no region for the "All" pill, which the bar reports as a value that is no region', () => {
    const before: CalendarFilters = { ...noCalendarFilters, scope: ['national'] };

    expect(withCalendarSelection(before, { scope: allScopesValue }).scope).toEqual([]);
    expect(withCalendarSelection(before, {}).scope).toEqual([]);
  });

  it('clears every facet for an empty selection, and ignores a key that is no facet or a value that is no text', () => {
    const before: CalendarFilters = { ...noCalendarFilters, kind: ['runway'], city: ['paris'], search: 'silk' };

    expect(withCalendarSelection(before, {})).toEqual({ ...noCalendarFilters, search: 'silk' });
    expect(withCalendarSelection(before, { priority: true, kind: [1, 'market', null] })).toEqual({
      ...noCalendarFilters,
      kind: ['market'],
      search: 'silk',
    });
  });
});
