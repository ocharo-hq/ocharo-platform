import type {
  DeadlineKind,
  EventStatus,
  EventWhen,
  TrackedEventModel,
  TrackingFacetCount,
  TrackingFacetsModel,
  TrackingPriority,
  TrackingQuery,
  TrackingSort,
  TrackingSummaryModel,
  UpcomingDeadlineModel,
} from '@wow-two-beta/fe-events/contracts';

/**
 * Replay mode's query engine: Events' tracking list, facets and summary computed in memory over the snapshot's items,
 * after the service's own rules (`EventListQuery`, `TrackingFilter`, `TrackingFacets`, `TrackingOrder`, the summary
 * read) and the kit's query strings (`trackingQueryParams`). Pure: the day is always passed in, nothing reads a clock
 * or a file.
 * The few rules the service keeps to itself are chosen simply and listed in `dev/dev.md`.
 */

/** The catalog and workflow facts the facets and the summary list in their own order. */
export interface ReplayCatalog {
  /** The catalog's kinds, in its order. */
  readonly kinds: readonly string[];
  /** The workflow's stage keys, in order. */
  readonly stages: readonly string[];
  /** The stage an untracked event reads as. */
  readonly defaultStage: string;
  /** The stages of class `lost`: the summary's deadlines leave them out. */
  readonly lostStages: readonly string[];
}

/** One answer of the replay: a value, or why the request is refused (the service's 400). */
export type ReplayOutcome<T> =
  { readonly ok: true; readonly value: T } | { readonly ok: false; readonly detail: string };

/** The summary read's query: the day deadlines count from and the deadline kinds to list (every kind when none). */
export interface ReplaySummaryQuery {
  readonly today: string;
  readonly deadlineKinds: readonly DeadlineKind[];
}

/** One page of the tracking list: its items and the cursor of the next page, `null` after the last. */
export interface ReplayPage {
  readonly items: readonly TrackedEventModel[];
  readonly nextCursor: string | null;
}

/** The page size the list answers when the query names none, and the most it answers. */
export const ReplayPageSize = { default: 50, max: 200 } as const;

const sorts: readonly TrackingSort[] = ['starts', 'added', 'deadline'];
const priorities: readonly TrackingPriority[] = ['high', 'normal', 'low'];
const eventStatuses: readonly EventStatus[] = ['scheduled', 'postponed', 'rescheduled', 'cancelled', 'movedOnline'];
const deadlineKindValues: readonly DeadlineKind[] = [
  'application',
  'registration',
  'earlyBird',
  'submission',
  'payment',
  'other',
];
const curatedSource = 'curated';
const intakeSourceKind = 'scfd';
const deadlineWindowDays = 30;
const upcomingDeadlineCount = 5;
const dayMs = 86_400_000;

const accept = <T>(value: T): ReplayOutcome<T> => ({ ok: true, value });
const refuse = (detail: string): ReplayOutcome<never> => ({ ok: false, detail });
const isOneOf = <T extends string>(values: readonly T[], value: string): value is T =>
  (values as readonly string[]).includes(value);
const compare = (a: string, b: string): number => (a < b ? -1 : a > b ? 1 : 0);
const isDay = (value: string): boolean => /^\d{4}-\d{2}-\d{2}$/.test(value);

/** Adds whole days to a `YYYY-MM-DD` day. */
function addDays(day: string, days: number): string {
  return new Date(Date.parse(`${day}T00:00:00Z`) + days * dayMs).toISOString().slice(0, 10);
}

// --- Reading a request ---

const textOf = (params: URLSearchParams, key: string): string | undefined => params.get(key)?.trim() || undefined;
const listOf = (params: URLSearchParams, key: string): string[] =>
  params
    .getAll(key)
    .map((value) => value.trim())
    .filter((value) => value !== '');
const flagOf = (params: URLSearchParams, key: string): boolean => params.get(key)?.trim().toLowerCase() === 'true';

/** Reads the list's and the facets' query string as the kit writes it: lists repeat their key, a flag is `true`. */
export function parseTrackingQuery(params: URLSearchParams): ReplayOutcome<TrackingQuery> {
  const sort = textOf(params, 'sort') ?? 'starts';
  if (!isOneOf(sorts, sort)) return refuse('sort must be starts, added or deadline');
  const priority = textOf(params, 'priority');
  if (priority !== undefined && !isOneOf(priorities, priority)) return refuse('priority must be high, normal or low');
  const status = textOf(params, 'status');
  if (status !== undefined && !isOneOf(eventStatuses, status)) return refuse('status is not an event status');

  const country = textOf(params, 'country');
  const countries = listOf(params, 'countries');
  const excludeCountries = listOf(params, 'excludeCountries');
  if ([...(country ? [country] : []), ...countries, ...excludeCountries].some((code) => !/^[A-Za-z]{2}$/.test(code)))
    return refuse('countries must be ISO-2 codes');

  const stage = textOf(params, 'stage');
  const stages = listOf(params, 'stages');
  const excludeStages = listOf(params, 'excludeStages');
  if ([...stages, ...(stage ? [stage] : [])].some((key) => excludeStages.includes(key)))
    return refuse('a stage may not be both listed and left out');

  const limit = Number.parseInt(params.get('limit') ?? '', 10);
  return accept({
    trackedOnly: flagOf(params, 'trackedOnly'),
    stage,
    stages,
    excludeStages,
    priority,
    from: textOf(params, 'from'),
    to: textOf(params, 'to'),
    kind: textOf(params, 'kind'),
    kinds: listOf(params, 'kinds'),
    city: textOf(params, 'city'),
    cities: listOf(params, 'cities'),
    status,
    sources: listOf(params, 'sources'),
    publishedSince: textOf(params, 'publishedSince'),
    country,
    countries,
    excludeCountries,
    includeUnknownCountry: flagOf(params, 'includeUnknownCountry'),
    text: textOf(params, 'text'),
    deadlineBefore: textOf(params, 'deadlineBefore'),
    includeUndated: flagOf(params, 'includeUndated'),
    sort,
    cursor: textOf(params, 'cursor'),
    limit: Number.isFinite(limit) ? limit : undefined,
  });
}

/** Reads the summary's query string: `today` as `YYYY-MM-DD` (the given day when absent), repeated `deadlineKinds`. */
export function parseSummaryQuery(params: URLSearchParams, fallbackToday: string): ReplayOutcome<ReplaySummaryQuery> {
  const today = textOf(params, 'today') ?? fallbackToday;
  if (!isDay(today)) return refuse('today must be a YYYY-MM-DD day');
  const kinds = listOf(params, 'deadlineKinds');
  if (!kinds.every((kind) => isOneOf(deadlineKindValues, kind))) return refuse('deadlineKinds holds an unknown kind');
  return accept({ today, deadlineKinds: kinds as DeadlineKind[] });
}

// --- What the service keeps per event ---

interface Span {
  readonly min: string;
  readonly max: string;
}

/** Reads an honest date as the days it covers: a day, a month or a year whole; `null` when unknown or malformed. */
function dateSpan(when: EventWhen | null | undefined): Span | null {
  const value = when?.value?.trim();
  if (!when || !value) return null;
  switch (when.precision) {
    case 'year':
      return /^\d{4}$/.test(value) ? { min: `${value}-01-01`, max: `${value}-12-31` } : null;
    case 'month': {
      const month = Number(value.slice(5, 7));
      if (!/^\d{4}-\d{2}$/.test(value) || month < 1 || month > 12) return null;
      const last = new Date(Date.UTC(Number(value.slice(0, 4)), month, 0)).getUTCDate();
      return { min: `${value}-01`, max: `${value}-${String(last).padStart(2, '0')}` };
    }
    case 'day':
      return isDay(value) ? { min: value, max: value } : null;
    case 'minute':
      return /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(value)
        ? { min: value.slice(0, 10), max: value.slice(0, 10) }
        : null;
    default:
      return null;
  }
}

interface Row {
  readonly item: TrackedEventModel;
  readonly id: string;
  readonly kind: string;
  readonly country: string | null;
  readonly city: string | null;
  readonly status: string;
  readonly startsMin: string | null;
  readonly endsMax: string | null;
  readonly titleSort: string;
  readonly search: string;
  readonly notes: string;
  readonly publishedAt: number | null;
  readonly stage: string | null;
  readonly priority: string | null;
  /** One entry per deadline, in order; `null` for a deadline with no known day. */
  readonly deadlines: readonly (Span | null)[];
  /** The event's source names: lowercase key and the name as stored. */
  readonly sourceNames: readonly { readonly key: string; readonly label: string }[];
  /** Whether an intake source reported the event; imports and curators' entries do not. */
  readonly intake: boolean;
}

/** The first title, preferring a known locale over `und`, as the service sorts by it. */
function firstTitle(title: Readonly<Record<string, string>>): string {
  const entries = Object.entries(title).sort(([a], [b]) => Number(a === 'und') - Number(b === 'und') || compare(a, b));
  return entries[0]?.[1] ?? '';
}

function rowOf(item: TrackedEventModel): Row {
  const { event, tracking } = item;
  const facts = event.facts;
  const starts = dateSpan(facts.starts);
  const ends = dateSpan(facts.ends);
  const sources = event.sources ?? [];
  const published = event.publishedAtUtc ? Date.parse(event.publishedAtUtc) : Number.NaN;
  return {
    item,
    id: event.id,
    kind: facts.kind ?? 'other',
    country: facts.place?.country?.trim().toUpperCase() || null,
    city: facts.place?.city?.trim().toLowerCase() || null,
    status: facts.status ?? 'scheduled',
    startsMin: starts?.min ?? null,
    endsMax: ends?.max ?? starts?.max ?? null,
    titleSort: firstTitle(facts.title).toLowerCase(),
    search: [...Object.values(facts.title), facts.organizer?.name, facts.place?.city, facts.place?.venue, facts.key]
      .filter((text): text is string => !!text)
      .join(' ')
      .toLowerCase(),
    notes: tracking?.notes?.toLowerCase() ?? '',
    publishedAt: Number.isFinite(published) ? published : null,
    stage: tracking?.stage ?? null,
    priority: tracking?.priority ?? null,
    deadlines: (facts.deadlines ?? []).map((deadline) => dateSpan(deadline.at)),
    sourceNames: sources.flatMap((source) =>
      source.name?.trim() ? [{ key: source.name.trim().toLowerCase(), label: source.name.trim() }] : [],
    ),
    intake: sources.some((source) => source.kind === intakeSourceKind),
  };
}

// --- Filtering ---

interface Filter {
  readonly includeUndated: boolean;
  readonly from: string | undefined;
  readonly to: string | undefined;
  readonly kinds: readonly string[];
  readonly countries: readonly string[];
  readonly excludeCountries: readonly string[];
  readonly includeUnknownCountry: boolean;
  readonly cities: readonly string[];
  readonly status: string | undefined;
  readonly deadlineBefore: string | undefined;
  readonly trackedOnly: boolean;
  readonly stages: readonly string[];
  readonly excludeStages: readonly string[];
  readonly priority: string | undefined;
  readonly sourceNames: readonly string[];
  readonly curated: boolean;
  readonly publishedSince: number | undefined;
  readonly text: string | undefined;
}

type Facet = 'countries' | 'cities' | 'kinds' | 'stages' | 'priority' | 'sources' | 'deadline';

const distinct = <T>(values: readonly T[]): T[] => [...new Set(values)];
const cleaned = (values: readonly (string | undefined)[], change: (value: string) => string): string[] =>
  distinct(values.flatMap((value) => (value?.trim() ? [change(value.trim())] : [])));
const same = (value: string): string => value;

function filterOf(query: TrackingQuery): Filter {
  const sources = cleaned(query.sources ?? [], (value) => value.toLowerCase());
  const since = query.publishedSince ? Date.parse(query.publishedSince) : Number.NaN;
  return {
    includeUndated: query.includeUndated === true,
    from: query.from || undefined,
    to: query.to || undefined,
    kinds: cleaned([...(query.kinds ?? []), query.kind], (value) => value.toLowerCase()),
    countries: cleaned([...(query.countries ?? []), query.country], (value) => value.toUpperCase()),
    excludeCountries: cleaned(query.excludeCountries ?? [], (value) => value.toUpperCase()),
    includeUnknownCountry: query.includeUnknownCountry === true,
    cities: cleaned([...(query.cities ?? []), query.city], (value) => value.toLowerCase()),
    status: query.status,
    deadlineBefore: query.deadlineBefore || undefined,
    trackedOnly: query.trackedOnly === true,
    stages: cleaned([...(query.stages ?? []), query.stage], same),
    excludeStages: cleaned(query.excludeStages ?? [], same),
    priority: query.priority,
    sourceNames: sources.filter((source) => source !== curatedSource),
    curated: sources.includes(curatedSource),
    publishedSince: Number.isFinite(since) ? since : undefined,
    text: query.text?.trim() ? query.text.trim().toLowerCase() : undefined,
  };
}

/** The filters with one facet's own selection left out, as each facet is counted. */
function withoutFacet(filter: Filter, facet: Facet): Filter {
  switch (facet) {
    case 'countries':
      return { ...filter, countries: [], excludeCountries: [], includeUnknownCountry: false };
    case 'cities':
      return { ...filter, cities: [] };
    case 'kinds':
      return { ...filter, kinds: [] };
    case 'stages':
      return { ...filter, stages: [], excludeStages: [] };
    case 'priority':
      return { ...filter, priority: undefined };
    case 'sources':
      return { ...filter, sourceNames: [], curated: false };
    case 'deadline':
      return { ...filter, deadlineBefore: undefined };
  }
}

/** Whether a deadline is still open on `today` and starts on or before `before`. */
const openBefore = (row: Row, before: string, today: string): boolean =>
  row.deadlines.some((span) => span !== null && span.min <= before && span.max >= today);

function matches(row: Row, filter: Filter, catalog: ReplayCatalog, today: string): boolean {
  if (!filter.includeUndated && row.startsMin === null) return false;
  if (filter.from && row.startsMin !== null && (row.endsMax ?? '') < filter.from) return false;
  if (filter.to && row.startsMin !== null && row.startsMin > filter.to) return false;
  if (filter.kinds.length > 0 && !filter.kinds.includes(row.kind)) return false;
  // An event with no country matches a country list only when unknown countries are let in.
  if (filter.countries.length > 0) {
    const listed = row.country === null ? filter.includeUnknownCountry : filter.countries.includes(row.country);
    if (!listed) return false;
  }
  if (filter.excludeCountries.length > 0) {
    const kept = row.country === null ? filter.includeUnknownCountry : !filter.excludeCountries.includes(row.country);
    if (!kept) return false;
  }
  if (filter.cities.length > 0 && (row.city === null || !filter.cities.includes(row.city))) return false;
  if (filter.status !== undefined && row.status !== filter.status) return false;
  if (filter.deadlineBefore !== undefined && !openBefore(row, filter.deadlineBefore, today)) return false;
  if (filter.publishedSince !== undefined && (row.publishedAt === null || row.publishedAt < filter.publishedSince))
    return false;

  if ((filter.trackedOnly || filter.priority !== undefined) && row.stage === null) return false;
  // An untracked event stands in the workflow's default stage.
  const stage = row.stage ?? catalog.defaultStage;
  if (filter.stages.length > 0 && !filter.stages.includes(stage)) return false;
  if (filter.excludeStages.length > 0 && filter.excludeStages.includes(stage)) return false;
  if (filter.priority !== undefined && row.priority !== filter.priority) return false;

  if (filter.sourceNames.length > 0 || filter.curated) {
    const named = row.sourceNames.some((source) => filter.sourceNames.includes(source.key));
    if (!named && !(filter.curated && !row.intake)) return false;
  }

  if (filter.text !== undefined) {
    // A two-letter search also finds the events in that country ("UZ" or "kz").
    const byCountry = /^[a-z]{2}$/.test(filter.text) && row.country === filter.text.toUpperCase();
    if (!row.search.includes(filter.text) && !byCountry && !row.notes.includes(filter.text)) return false;
  }
  return true;
}

/** Lists the items a tracking query selects, in the order given; sort and page them with the functions below. */
export function filterTracking(
  items: readonly TrackedEventModel[],
  query: TrackingQuery,
  catalog: ReplayCatalog,
  today: string,
): TrackedEventModel[] {
  const filter = filterOf(query);
  return items
    .map(rowOf)
    .filter((row) => matches(row, filter, catalog, today))
    .map((row) => row.item);
}

// --- Sorting and paging ---

/** The first day of an event's earliest deadline that is still open on `today`; `null` when none is. */
function nextDeadline(row: Row, today: string): string | null {
  const days = row.deadlines.flatMap((span) => (span !== null && span.max >= today ? [span.min] : []));
  return days.length > 0 ? days.reduce((first, day) => (day < first ? day : first)) : null;
}

/** Puts a missing value last. */
const lastWhenNull = (a: string | null, b: string | null): number =>
  a === b ? 0 : a === null ? 1 : b === null ? -1 : compare(a, b);

/**
 * Orders items as the service does: `starts` by first day with unknown last, `added` newest published first, `deadline`
 * by the earliest open deadline with none last; each ends on the title, then the id.
 */
export function sortTracking(
  items: readonly TrackedEventModel[],
  sort: TrackingSort,
  today: string,
): TrackedEventModel[] {
  const rows = items.map((item) => ({ row: rowOf(item), next: null as string | null }));
  if (sort === 'deadline') for (const entry of rows) entry.next = nextDeadline(entry.row, today);
  const byTitle = (a: Row, b: Row) => compare(a.titleSort, b.titleSort) || compare(a.id, b.id);
  rows.sort((a, b) => {
    switch (sort) {
      case 'added': {
        if (a.row.publishedAt === b.row.publishedAt) return compare(b.row.id, a.row.id);
        if (a.row.publishedAt === null) return 1;
        if (b.row.publishedAt === null) return -1;
        return b.row.publishedAt - a.row.publishedAt || compare(b.row.id, a.row.id);
      }
      case 'deadline':
        return lastWhenNull(a.next, b.next) || byTitle(a.row, b.row);
      default:
        return lastWhenNull(a.row.startsMin, b.row.startsMin) || byTitle(a.row, b.row);
    }
  });
  return rows.map((entry) => entry.row.item);
}

const cursorPattern = /^replay\.(starts|added|deadline)\.(\d+)$/;

/**
 * Cuts one page out of sorted items. The cursor is the sort and the offset of the next page; it holds for the sort it
 * came from, and another sort or an unreadable cursor is refused. `limit` is clamped to 1 to 200 (50 when absent).
 */
export function pageTracking(
  sorted: readonly TrackedEventModel[],
  sort: TrackingSort,
  cursor: string | undefined,
  limit: number | undefined,
): ReplayOutcome<ReplayPage> {
  let offset = 0;
  if (cursor) {
    const match = cursorPattern.exec(cursor);
    if (!match) return refuse('the cursor does not read');
    if (match[1] !== sort) return refuse('the cursor belongs to another sort');
    offset = Number(match[2]);
  }
  const size = Math.min(Math.max(limit ?? ReplayPageSize.default, 1), ReplayPageSize.max);
  const items = sorted.slice(offset, offset + size);
  const end = offset + items.length;
  return accept({ items, nextCursor: end < sorted.length ? `replay.${sort}.${end}` : null });
}

// --- Facets ---

const ranked = (counts: ReadonlyMap<string, number>, labels?: ReadonlyMap<string, string>): TrackingFacetCount[] =>
  [...counts]
    .sort(([a, x], [b, y]) => y - x || compare(a, b))
    .map(([value, count]) => (labels?.has(value) ? { value, count, label: labels.get(value) } : { value, count }));

/** The known keys in their order, zeros included, then any other value found, most events first. */
function ordered(known: readonly string[], counts: ReadonlyMap<string, number>): TrackingFacetCount[] {
  const keys = distinct(known);
  const others = new Map([...counts].filter(([value]) => !keys.includes(value)));
  return [...keys.map((value) => ({ value, count: counts.get(value) ?? 0 })), ...ranked(others)];
}

function tally(values: Iterable<string>): Map<string, number> {
  const counts = new Map<string, number>();
  for (const value of values) counts.set(value, (counts.get(value) ?? 0) + 1);
  return counts;
}

/**
 * Counts what each filter chip of a tracking list would list, for the list's own query: every facet is counted without
 * its own selection (and under all the others), `total` counts the whole query, and `openDeadline` counts the events
 * with a deadline open between `today` and the query's `deadlineBefore`, else 30 days on.
 */
export function buildFacets(
  items: readonly TrackedEventModel[],
  query: TrackingQuery,
  catalog: ReplayCatalog,
  today: string,
): TrackingFacetsModel {
  const filter = filterOf(query);
  const all = items.map(rowOf);
  const rows = (facet?: Facet): Row[] => {
    const wanted = facet ? withoutFacet(filter, facet) : filter;
    return all.filter((row) => matches(row, wanted, catalog, today));
  };

  const named = new Map<string, Set<string>>();
  const labels = new Map<string, string>();
  const forSources = rows('sources');
  for (const row of forSources) {
    for (const { key, label } of row.sourceNames) {
      named.set(key, (named.get(key) ?? new Set<string>()).add(row.id));
      const smallest = labels.get(key);
      if (smallest === undefined || label < smallest) labels.set(key, label);
    }
  }
  // The reserved `curated` stands for the events no intake source reported; a source cannot be named so.
  const sources = new Map<string, number>();
  for (const [key, ids] of named) if (key !== curatedSource) sources.set(key, ids.size);
  sources.set(curatedSource, forSources.filter((row) => !row.intake).length);
  const before = filter.deadlineBefore ?? addDays(today, deadlineWindowDays);

  return {
    total: rows().length,
    countries: ranked(tally(rows('countries').flatMap((row) => (row.country ? [row.country] : [])))),
    cities: ranked(tally(rows('cities').flatMap((row) => (row.city ? [row.city] : [])))),
    kinds: ordered(catalog.kinds, tally(rows('kinds').map((row) => row.kind))),
    stages: ordered(catalog.stages, tally(rows('stages').map((row) => row.stage ?? catalog.defaultStage))),
    priorities: ordered(priorities, tally(rows('priority').flatMap((row) => (row.priority ? [row.priority] : [])))),
    sources: ranked(sources, labels),
    openDeadline: { before, count: rows('deadline').filter((row) => openBefore(row, before, today)).length },
  };
}

// --- Summary ---

/**
 * Counts the tracking per stage (the workflow's stages in order, zeros included), the events with none, and the nearest
 * five open deadlines of events that are not cancelled, untracked ones included; an event in a lost stage is left out.
 */
export function buildSummary(
  items: readonly TrackedEventModel[],
  catalog: ReplayCatalog,
  today: string,
  deadlineKinds: readonly DeadlineKind[],
): TrackingSummaryModel {
  const rows = items.map(rowOf);
  const counts = tally(rows.flatMap((row) => (row.stage === null ? [] : [row.stage])));
  const byStage: Record<string, number> = {};
  for (const stage of catalog.stages) byStage[stage] = counts.get(stage) ?? 0;
  for (const [stage, count] of [...counts].sort(([a], [b]) => compare(a, b))) byStage[stage] ??= count;

  const lost = catalog.lostStages;
  const untrackedLost = lost.includes(catalog.defaultStage);
  const listed = rows.filter(
    (row) =>
      row.status !== 'cancelled' &&
      (untrackedLost
        ? row.stage !== null && !lost.includes(row.stage)
        : row.stage === null || !lost.includes(row.stage)),
  );
  const nearest = listed
    .flatMap((row) =>
      (row.item.event.facts.deadlines ?? []).flatMap((deadline, ordinal) => {
        const span = row.deadlines[ordinal] ?? null;
        const wanted = deadlineKinds.length === 0 || deadlineKinds.includes(deadline.kind);
        return span !== null && span.min >= today && wanted ? [{ row, deadline, ordinal, day: span.min }] : [];
      }),
    )
    .sort(
      (a, b) =>
        compare(a.day, b.day) ||
        compare(a.row.titleSort, b.row.titleSort) ||
        compare(a.row.id, b.row.id) ||
        a.ordinal - b.ordinal,
    )
    .slice(0, upcomingDeadlineCount);

  const upcomingDeadlines: UpcomingDeadlineModel[] = nearest.map(({ row, deadline }) => ({
    eventId: row.id,
    title: row.item.event.facts.title,
    deadline,
    stage: row.stage ?? catalog.defaultStage,
    tracked: row.stage !== null,
  }));
  return { byStage, untracked: rows.filter((row) => row.stage === null).length, upcomingDeadlines };
}
