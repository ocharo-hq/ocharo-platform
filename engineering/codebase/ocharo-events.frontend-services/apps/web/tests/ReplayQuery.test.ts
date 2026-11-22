import { describe, expect, it } from 'vitest';
import type { DeadlineKind, EventWhen, TrackedEventModel, TrackingQuery } from '@wow-two-beta/fe-events/contracts';

import {
  buildFacets,
  buildSummary,
  filterTracking,
  pageTracking,
  parseSummaryQuery,
  parseTrackingQuery,
  sortTracking,
  type ReplayCatalog,
  type ReplayOutcome,
} from '../dev/ReplayQuery';

const today = '2026-11-22';

const catalog: ReplayCatalog = {
  kinds: ['runway', 'trade-fair', 'market', 'festival', 'conference'],
  stages: ['candidate', 'shortlisted', 'applying', 'skipped'],
  defaultStage: 'candidate',
  lostStages: ['skipped'],
};

const day = (value: string): EventWhen => ({ value, precision: 'day', certainty: 'announced' });
const month = (value: string): EventWhen => ({ value, precision: 'month', certainty: 'estimated' });
const unknown: EventWhen = { precision: 'unknown', certainty: 'estimated' };

interface Seed {
  readonly id: string;
  readonly title: string;
  readonly kind: string;
  readonly country?: string;
  readonly city?: string;
  readonly starts: EventWhen;
  readonly ends?: EventWhen;
  readonly published: string;
  readonly stage?: string;
  readonly priority?: 'high' | 'normal' | 'low';
  readonly deadline?: { readonly kind: DeadlineKind; readonly at: EventWhen };
  readonly source?: string;
}

/** One published event of the catalog, with the organization's tracking when a stage is given. */
function seed(entry: Seed): TrackedEventModel {
  const stamp = `${entry.published}T09:00:00Z`;
  return {
    event: {
      id: entry.id,
      catalog: 'fashion',
      review: 'published',
      revision: 1,
      createdAtUtc: stamp,
      updatedAtUtc: stamp,
      publishedAtUtc: stamp,
      sources: entry.source
        ? [{ kind: 'scfd', id: `${entry.id}-source`, name: entry.source, firstSeenAtUtc: stamp, lastSeenAtUtc: stamp }]
        : [],
      facts: {
        title: { und: entry.title },
        kind: entry.kind,
        place: { ...(entry.country ? { country: entry.country } : {}), ...(entry.city ? { city: entry.city } : {}) },
        starts: entry.starts,
        ...(entry.ends ? { ends: entry.ends } : {}),
        deadlines: entry.deadline ? [entry.deadline] : [],
      },
    },
    ...(entry.stage
      ? {
          tracking: {
            id: `${entry.id}-tracking`,
            eventId: entry.id,
            stage: entry.stage,
            priority: entry.priority ?? 'normal',
            revision: 1,
            createdAtUtc: stamp,
            updatedAtUtc: stamp,
          },
        }
      : {}),
  };
}

const items: TrackedEventModel[] = [
  seed({
    id: 'a',
    title: 'Alpha Runway',
    kind: 'runway',
    country: 'UZ',
    city: 'Tashkent',
    starts: day('2026-12-05'),
    published: '2026-11-01',
    stage: 'shortlisted',
    priority: 'high',
  }),
  seed({
    id: 'b',
    title: 'Bravo Fair',
    kind: 'trade-fair',
    country: 'FR',
    city: 'Paris',
    starts: day('2026-12-10'),
    ends: day('2026-12-12'),
    published: '2026-11-05',
    source: 'Afisha',
  }),
  seed({
    id: 'c',
    title: 'Charlie Fair',
    kind: 'trade-fair',
    country: 'UZ',
    city: 'Bukhara',
    starts: month('2027-01'),
    published: '2026-11-10',
    stage: 'skipped',
    deadline: { kind: 'application', at: day('2026-12-20') },
  }),
  seed({ id: 'd', title: 'Delta Market', kind: 'market', starts: unknown, published: '2026-11-15' }),
  seed({
    id: 'e',
    title: 'Echo Runway',
    kind: 'runway',
    country: 'DE',
    city: 'Frankfurt',
    starts: day('2026-11-01'),
    ends: day('2026-11-03'),
    published: '2026-10-01',
    deadline: { kind: 'application', at: day('2026-10-30') },
  }),
  seed({
    id: 'f',
    title: 'Foxtrot Festival',
    kind: 'festival',
    country: 'KZ',
    city: 'Almaty',
    starts: day('2027-03-01'),
    published: '2026-11-12',
    stage: 'applying',
    priority: 'low',
    deadline: { kind: 'registration', at: day('2026-11-30') },
  }),
  seed({
    id: 'g',
    title: 'Golf Festival',
    kind: 'festival',
    country: 'UZ',
    city: 'Tashkent',
    starts: day('2026-12-05'),
    published: '2026-11-02',
    deadline: { kind: 'application', at: day('2026-12-18') },
  }),
];

const ids = (list: readonly TrackedEventModel[]) => list.map((item) => item.event.id);
const list = (query: TrackingQuery) => ids(filterTracking(items, query, catalog, today));
const expectOk = <T>(outcome: ReplayOutcome<T>): T => {
  if (!outcome.ok) throw new Error(outcome.detail);
  return outcome.value;
};

describe('filterTracking', () => {
  it('lists the events of the chosen kinds', () => {
    expect(list({ includeUndated: true, kinds: ['trade-fair'] })).toEqual(['b', 'c']);
    expect(list({ includeUndated: true, kind: 'market', kinds: ['festival'] })).toEqual(['d', 'f', 'g']);
  });

  it('lists the events of the chosen countries, an event with no country only when unknown ones are let in', () => {
    expect(list({ includeUndated: true, countries: ['UZ'] })).toEqual(['a', 'c', 'g']);
    expect(list({ includeUndated: true, countries: ['uz'], includeUnknownCountry: true })).toEqual([
      'a',
      'c',
      'd',
      'g',
    ]);
    // A region of "every other country" leaves its neighbours out and the unknown country with them.
    expect(list({ includeUndated: true, excludeCountries: ['UZ', 'KZ'] })).toEqual(['b', 'e']);
  });

  it('reads an untracked event as the default stage when it lists or leaves out stages', () => {
    expect(list({ includeUndated: true, excludeStages: ['skipped'] })).toEqual(['a', 'b', 'd', 'e', 'f', 'g']);
    expect(list({ includeUndated: true, stages: ['candidate'] })).toEqual(['b', 'd', 'e', 'g']);
    expect(list({ includeUndated: true, excludeStages: ['candidate'] })).toEqual(['a', 'c', 'f']);
    expect(list({ includeUndated: true, stages: ['shortlisted', 'applying'] })).toEqual(['a', 'f']);
  });

  it('keeps an event whose days overlap the window and leaves undated events out unless asked', () => {
    expect(list({ from: '2026-12-01', to: '2026-12-31' })).toEqual(['a', 'b', 'g']);
    // Bravo started on the 10th and ends on the 12th, so a window opening on the 11th still holds it.
    expect(list({ from: '2026-12-11', to: '2026-12-31' })).toEqual(['b']);
    // A month-precision date covers its whole month.
    expect(list({ from: '2027-01-15', to: '2027-01-20' })).toEqual(['c']);
    // The feed's default: from today on, undated events included, the ended Echo gone.
    expect(list({ from: today, includeUndated: true })).toEqual(['a', 'b', 'c', 'd', 'f', 'g']);
  });

  it('matches the search text and a country code, the priority and the deadline window', () => {
    expect(list({ includeUndated: true, text: 'paris' })).toEqual(['b']);
    expect(list({ includeUndated: true, text: 'KZ' })).toEqual(['f']);
    expect(list({ includeUndated: true, priority: 'high' })).toEqual(['a']);
    expect(list({ includeUndated: true, deadlineBefore: '2026-12-19' })).toEqual(['f', 'g']);
  });

  it('matches a source by its name without case, and `curated` for the events no intake source reported', () => {
    expect(list({ includeUndated: true, sources: ['afisha'] })).toEqual(['b']);
    expect(list({ includeUndated: true, sources: ['curated'] })).toEqual(['a', 'c', 'd', 'e', 'f', 'g']);
  });
});

describe('sortTracking', () => {
  it('orders by start date, unknown last, then by title', () => {
    expect(ids(sortTracking(items, 'starts', today))).toEqual(['e', 'a', 'g', 'b', 'c', 'f', 'd']);
  });

  it('orders by publication, newest first', () => {
    expect(ids(sortTracking(items, 'added', today))).toEqual(['d', 'f', 'c', 'b', 'g', 'a', 'e']);
  });

  it('orders by the nearest open deadline, none last', () => {
    expect(ids(sortTracking(items, 'deadline', today))).toEqual(['f', 'g', 'c', 'a', 'b', 'd', 'e']);
  });
});

describe('buildFacets', () => {
  const query: TrackingQuery = { from: today, includeUndated: true, kinds: ['runway'], countries: ['UZ'] };

  it('counts the whole query as the total and each facet without its own selection', () => {
    const facets = buildFacets(items, query, catalog, today);
    expect(facets.total).toBe(1);
    // Kinds ignore the chosen kind but keep the chosen country: Alpha, Charlie and Golf are in Uzbekistan.
    expect(facets.kinds).toEqual([
      { value: 'runway', count: 1 },
      { value: 'trade-fair', count: 1 },
      { value: 'market', count: 0 },
      { value: 'festival', count: 1 },
      { value: 'conference', count: 0 },
    ]);
    // Countries ignore the chosen country but keep the chosen kind (Echo is over): only Alpha runs.
    expect(facets.countries).toEqual([{ value: 'UZ', count: 1 }]);
    expect(facets.cities).toEqual([{ value: 'tashkent', count: 1 }]);
  });

  it('counts stages with the untracked in the default one, priorities, and sources with curated', () => {
    const facets = buildFacets(
      items,
      { from: today, includeUndated: true, excludeStages: ['skipped'] },
      catalog,
      today,
    );
    expect(facets.total).toBe(5);
    // Stages ignore the stage selection, so the skipped Charlie still counts there.
    expect(facets.stages).toEqual([
      { value: 'candidate', count: 3 },
      { value: 'shortlisted', count: 1 },
      { value: 'applying', count: 1 },
      { value: 'skipped', count: 1 },
    ]);
    expect(facets.priorities).toEqual([
      { value: 'high', count: 1 },
      { value: 'normal', count: 0 },
      { value: 'low', count: 1 },
    ]);
    expect(facets.sources).toEqual([
      { value: 'curated', count: 4 },
      { value: 'afisha', count: 1, label: 'Afisha' },
    ]);
  });

  it('counts the events with a deadline open within 30 days, or within the query`s deadline window', () => {
    const wide = buildFacets(items, { from: today, includeUndated: true }, catalog, today);
    expect(wide.openDeadline).toEqual({ before: '2026-12-22', count: 3 });
    const narrow = buildFacets(
      items,
      { from: today, includeUndated: true, deadlineBefore: '2026-12-01' },
      catalog,
      today,
    );
    expect(narrow.openDeadline).toEqual({ before: '2026-12-01', count: 1 });
    // The deadline chip's own selection is left out of its count, but narrows everything else.
    expect(narrow.total).toBe(1);
  });
});

describe('buildSummary', () => {
  it('counts tracking per stage, the stages in order with zeros, and the events with none', () => {
    const summary = buildSummary(items, catalog, today, []);
    expect(summary.byStage).toEqual({ candidate: 0, shortlisted: 1, applying: 1, skipped: 1 });
    expect(Object.keys(summary.byStage)).toEqual(['candidate', 'shortlisted', 'applying', 'skipped']);
    expect(summary.untracked).toBe(4);
  });

  it('lists the nearest open deadlines, untracked events included, lost stages and past deadlines left out', () => {
    const { upcomingDeadlines } = buildSummary(items, catalog, today, []);
    expect(upcomingDeadlines.map((entry) => [entry.eventId, entry.stage, entry.tracked])).toEqual([
      ['f', 'applying', true],
      ['g', 'candidate', false],
    ]);
    expect(upcomingDeadlines[0]?.deadline.kind).toBe('registration');
  });

  it('lists only the deadline kinds asked for', () => {
    const { upcomingDeadlines } = buildSummary(items, catalog, today, ['application']);
    expect(upcomingDeadlines.map((entry) => entry.eventId)).toEqual(['g']);
  });
});

describe('pageTracking', () => {
  const sorted = sortTracking(items, 'starts', today);

  it('pages by cursor until the last page, which has none', () => {
    const first = expectOk(pageTracking(sorted, 'starts', undefined, 3));
    expect(ids(first.items)).toEqual(['e', 'a', 'g']);
    expect(first.nextCursor).not.toBeNull();
    const second = expectOk(pageTracking(sorted, 'starts', first.nextCursor ?? undefined, 3));
    expect(ids(second.items)).toEqual(['b', 'c', 'f']);
    const third = expectOk(pageTracking(sorted, 'starts', second.nextCursor ?? undefined, 3));
    expect(ids(third.items)).toEqual(['d']);
    expect(third.nextCursor).toBeNull();
  });

  it('refuses a cursor of another sort or one that does not read, and keeps the limit between 1 and 200', () => {
    const first = expectOk(pageTracking(sorted, 'starts', undefined, 3));
    expect(pageTracking(sorted, 'added', first.nextCursor ?? undefined, 3).ok).toBe(false);
    expect(pageTracking(sorted, 'starts', 'garbage', 3).ok).toBe(false);
    expect(expectOk(pageTracking(sorted, 'starts', undefined, 0)).items).toHaveLength(1);
    expect(expectOk(pageTracking(sorted, 'starts', undefined, 1000)).items).toHaveLength(items.length);
    expect(expectOk(pageTracking(sorted, 'starts', undefined, undefined)).nextCursor).toBeNull();
  });
});

describe('query strings', () => {
  it('reads the list the way the kit writes it: lists repeat their key', () => {
    const query = expectOk(
      parseTrackingQuery(
        new URLSearchParams(
          'kinds=runway&kinds=market&includeUndated=true&from=2026-11-22&excludeStages=skipped&sort=added&limit=5',
        ),
      ),
    );
    expect(query).toMatchObject({
      kinds: ['runway', 'market'],
      includeUndated: true,
      from: '2026-11-22',
      excludeStages: ['skipped'],
      sort: 'added',
      limit: 5,
    });
  });

  it('refuses an unknown sort and a stage both listed and left out', () => {
    expect(parseTrackingQuery(new URLSearchParams('sort=newest')).ok).toBe(false);
    expect(parseTrackingQuery(new URLSearchParams('stages=skipped&excludeStages=skipped')).ok).toBe(false);
    expect(parseTrackingQuery(new URLSearchParams('countries=UZB')).ok).toBe(false);
  });

  it('reads the summary query, the given day standing in for a missing `today`', () => {
    expect(expectOk(parseSummaryQuery(new URLSearchParams('deadlineKinds=application'), today))).toEqual({
      today,
      deadlineKinds: ['application'],
    });
    expect(parseSummaryQuery(new URLSearchParams('today=2026-12-01&deadlineKinds=party'), today).ok).toBe(false);
  });
});
