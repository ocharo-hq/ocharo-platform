import { EventsConstants, type LocalizedText, type TrackedEventModel } from '@wow-two-beta/fe-events/contracts';
import { eventsText, pickText } from '@wow-two-beta/fe-events/kit';

import {
  filterQueryKeys,
  readQueryText,
  readQueryValues,
  searchQueryKey,
  writeQueryText,
  writeQueryValues,
} from '@/domain/events/EventFilterQuery';
import { ocharoClosedStages, ocharoKinds, ocharoRegions, ocharoWorkflow } from '@/domain/events/Events';
import {
  calendarScope,
  ocharoCalendarEntry,
  untrackedStage,
  type EventsCalendarEntry,
  type EventsCalendarOptions,
} from '@/domain/events/EventsCalendar';

/**
 * The calendar's filters: the region, kinds, stages, cities and sources an event may be narrowed to (each of the last
 * four several at once, the region one, as the feed's Region choice is) and a search text, applied in the browser to the
 * events the window read. They are what the shared filter card shows and what the address keeps, under the keys the
 * feed keeps its own (`?regions=national&kinds=runway&stages=…&cities=…&sources=…&q=…`, through `EventFilterQuery`), so
 * one address means the same filters on both pages. The facets keep their kit names (`scope`, `kind`, `stage`, `city`,
 * `source`); the feed's `when` and `sort` mean nothing to the calendar and are left alone.
 *
 * A facet with nothing chosen keeps every event; one with choices keeps an event standing in any of them, so an event
 * that has none of that facet (no country, no city) is kept by no choice. A facet's counts follow the facet bar's rule:
 * each is counted with every other facet's choices and the search applied, but not its own, so a chip shows what
 * choosing it would match. Only the events the window draws (a known day, an open stage) are counted and filtered.
 */

/** The facets, in the order the card shows them, named as the kit's card names them. */
export const calendarFacetKeys = ['scope', 'kind', 'stage', 'city', 'source'] as const;
export type CalendarFacetKey = (typeof calendarFacetKeys)[number];

/** What is chosen: the values of each facet (none for no choice) and the search text. */
export interface CalendarFilters {
  /** The region chosen (`national`, `central-asia` or `international`) as a list of at most one key; empty for every region. */
  readonly scope: readonly string[];
  /** Keys of the catalog's kinds. */
  readonly kind: readonly string[];
  /** Keys of Ocharo's stages; an event not tracked yet stands in the default stage. */
  readonly stage: readonly string[];
  /** Cities as `facetValue` writes them: what the feed's city facet holds. */
  readonly city: readonly string[];
  /** Source names as `facetValue` writes them, or `curated` for the events no source named. */
  readonly source: readonly string[];
  /** Matched anywhere in an event's titles, city and organizer, whatever the case; blank keeps every event. */
  readonly search: string;
}

/** The value of the region facet's "All" pill, the kit's: a value that is no region, so choosing it chooses none. */
export const allScopesValue = 'all';

/** Nothing chosen: every event drawn. */
export const noCalendarFilters: CalendarFilters = { scope: [], kind: [], stage: [], city: [], source: [], search: '' };

/**
 * A city or source name as a filter value: trimmed and lowercase, as Events stores a city and matches a source, so the
 * value a choice on the feed holds is the one the calendar reads for the same event (`Tashkent` is `tashkent`). Two
 * spellings that differ in case alone are one value.
 */
export function facetValue(name: string): string {
  return name.trim().toLowerCase();
}

/**
 * The values of one facet cleaned as its kind needs: once each; a city or source as `facetValue` writes it; the region
 * only the first value listed, and only when it is one of Ocharo's, as the kit reads `regions` (a region listed after
 * one that is none is not read), since the region is one choice.
 */
function cleanValues(key: CalendarFacetKey, values: readonly string[]): string[] {
  const unique = [...new Set(values.map((value) => value.trim()).filter((value) => value !== ''))];
  if (key === 'scope') {
    const [first] = unique;
    return first !== undefined && ocharoRegions().some((region) => region.key === first) ? [first] : [];
  }
  if (key === 'city' || key === 'source') return [...new Set(unique.map(facetValue).filter((value) => value !== ''))];
  return unique;
}

/**
 * Reads the filters an address's query holds, as the `regions`, `kinds`, `stages`, `cities`, `sources` and `q` keys write
 * them: a region that is not one of Ocharo's is ignored, the rest are read as listed.
 */
export function readCalendarFilters(query: Readonly<Record<string, unknown>>): CalendarFilters {
  const read = (key: CalendarFacetKey) => cleanValues(key, readQueryValues(query[filterQueryKeys[key]]));
  return {
    scope: read('scope'),
    kind: read('kind'),
    stage: read('stage'),
    city: read('city'),
    source: read('source'),
    search: readQueryText(query[searchQueryKey]),
  };
}

/**
 * The query values for the filters, each key undefined when it has nothing, which leaves it out of the address: the
 * region as one value and the other facets as a list, which a router writes as a repeated key.
 */
export function writeCalendarFilters(
  filters: CalendarFilters,
): Readonly<Record<string, string | readonly string[] | undefined>> {
  return {
    ...Object.fromEntries(
      calendarFacetKeys.map((key) => {
        const values = cleanValues(key, filters[key]);
        return [filterQueryKeys[key], key === 'scope' ? values[0] : writeQueryValues(values)] as const;
      }),
    ),
    [searchQueryKey]: writeQueryText(filters.search),
  };
}

/**
 * The filters' choices as the facet bar's selection: a key for each facet that has a choice, none for the rest; the
 * region, a single choice, as its key, the others as lists.
 */
export function calendarSelection(filters: CalendarFilters): Record<string, string | readonly string[]> {
  return Object.fromEntries(
    calendarFacetKeys
      .filter((key) => filters[key].length > 0)
      .map((key) => [key, key === 'scope' ? filters.scope[0]! : filters[key]] as const),
  );
}

/**
 * The filters after the facet bar reports a selection: its choices replace the facets', the search stays. The bar's "All"
 * region is a value that is no region, so it chooses none.
 */
export function withCalendarSelection(
  filters: CalendarFilters,
  selection: Readonly<Record<string, unknown>>,
): CalendarFilters {
  const choose = (key: CalendarFacetKey) => {
    const value = selection[key];
    const listed = Array.isArray(value) ? value : [value];
    return cleanValues(
      key,
      listed.filter((part): part is string => typeof part === 'string'),
    );
  };
  return {
    ...filters,
    scope: choose('scope'),
    kind: choose('kind'),
    stage: choose('stage'),
    city: choose('city'),
    source: choose('source'),
  };
}

/** One drawn event with the values it stands in for each facet, read once so a filter or a count only compares. */
interface Row {
  readonly entry: EventsCalendarEntry;
  readonly values: Readonly<Record<CalendarFacetKey, readonly string[]>>;
  /** What the search looks in, lowercase. */
  readonly text: string;
}

/** The names the window's cities and sources go by (the first spelling met), by facet key and value. */
type Names = Readonly<Record<CalendarFacetKey, Map<string, string>>>;

/** The rows of the events the window draws, with the names their city and source values were written from. */
function drawnRows(
  loaded: readonly TrackedEventModel[],
  options: EventsCalendarOptions,
): { rows: Row[]; names: Names } {
  const names: Names = { scope: new Map(), kind: new Map(), stage: new Map(), city: new Map(), source: new Map() };
  const named = (key: 'city' | 'source', name: string): string => {
    const value = facetValue(name);
    if (value !== '' && !names[key].has(value)) names[key].set(value, name.trim());
    return value;
  };
  const rows = loaded.flatMap((item): Row[] => {
    const entry = ocharoCalendarEntry(item, options);
    if (!entry) return [];
    const { facts, sources } = item.event;
    const city = named('city', facts.place?.city ?? '');
    const sourceValues = (sources ?? []).map((source) => named('source', source.name ?? '')).filter((value) => value);
    // An event no named source reported came from an import or a curator, as the feed's `curated` source says.
    if (sourceValues.length === 0) {
      names.source.set(EventsConstants.curatedSource, eventsText('sourceCurated', options.locale));
      sourceValues.push(EventsConstants.curatedSource);
    }
    const scope = calendarScope(item);
    return [
      {
        entry,
        values: {
          scope: scope === null ? [] : [scope],
          kind: facts.kind ? [facts.kind] : [],
          stage: [item.tracking?.stage ?? untrackedStage],
          city: city === '' ? [] : [city],
          source: [...new Set(sourceValues)],
        },
        text: [...Object.values(facts.title), facts.place?.city ?? '', facts.organizer?.name ?? '']
          .join('\n')
          .toLowerCase(),
      },
    ];
  });
  return { rows, names };
}

/** Whether the row stands in one of the facet's chosen values; no choice keeps every row. */
const keepsFacet = (row: Row, key: CalendarFacetKey, chosen: readonly string[]): boolean =>
  chosen.length === 0 || row.values[key].some((value) => chosen.includes(value));

/** Whether the row has the search text; a blank search keeps every row. */
function keepsSearch(row: Row, search: string): boolean {
  const wanted = search.trim().toLowerCase();
  return wanted === '' || row.text.includes(wanted);
}

/** Whether the row passes the search and every facet's choices, but not those of `skipped`, the facet being counted. */
function keeps(row: Row, filters: CalendarFilters, skipped?: CalendarFacetKey): boolean {
  return (
    keepsSearch(row, filters.search) &&
    calendarFacetKeys.every((key) => key === skipped || keepsFacet(row, key, filters[key]))
  );
}

/**
 * The calendar's blocks narrowed by the filters: the events the window draws that have the search text and stand in a
 * chosen value of every facet with a choice, in the order read.
 */
export function filterCalendarEvents(
  loaded: readonly TrackedEventModel[],
  options: EventsCalendarOptions,
  filters: CalendarFilters,
): EventsCalendarEntry[] {
  return drawnRows(loaded, options)
    .rows.filter((row) => keeps(row, filters))
    .map((row) => row.entry);
}

/** One choice of a facet: its value, its label in the reader's language, and what choosing it would match. */
export interface CalendarFacetOption {
  readonly value: string;
  readonly label: string;
  readonly count: number;
}

/** One facet and its choices, in the order the card lists them. */
export interface CalendarFacetCounts {
  readonly key: CalendarFacetKey;
  readonly options: readonly CalendarFacetOption[];
}

/** What the filter card shows: the counts of every facet, how many events match the filters and how many the window draws. */
export interface CalendarCounts {
  /** The events passing every facet and the search. */
  readonly match: number;
  /** The events the window draws, with nothing applied. */
  readonly total: number;
  readonly facets: readonly CalendarFacetCounts[];
}

/** The choices a facet has whatever is loaded: Ocharo's regions, the catalog's kinds, the stages still open. */
function catalogOptions(key: CalendarFacetKey, options: EventsCalendarOptions): { value: string; label: string }[] {
  const labelled = (item: { key: string; label?: LocalizedText | undefined }) => ({
    value: item.key,
    label: pickText(item.label, options.locale) || item.key,
  });
  if (key === 'scope') return ocharoRegions().map(labelled);
  if (key === 'kind') return ocharoKinds().map(labelled);
  if (key === 'stage')
    return ocharoWorkflow()
      .stages.filter((stage) => !(ocharoClosedStages as readonly string[]).includes(stage.key))
      .map(labelled);
  return [];
}

/**
 * The choices of one facet: those it always has (`catalogOptions`), then the values the window holds that are not among
 * them, most events first so the order holds as other facets change (a city or source, a kind or stage the catalog does
 * not list), then any chosen value the window no longer holds, so a choice from the address can always be undone.
 */
function facetOptions(
  key: CalendarFacetKey,
  rows: readonly Row[],
  names: Names,
  chosen: readonly string[],
  options: EventsCalendarOptions,
): { value: string; label: string }[] {
  const listed = catalogOptions(key, options);
  const known = new Set(listed.map((option) => option.value));
  const totals = new Map<string, number>();
  for (const row of rows) for (const value of row.values[key]) totals.set(value, (totals.get(value) ?? 0) + 1);
  const labelled = (value: string) => ({ value, label: names[key].get(value) ?? value });
  const found = [...totals.keys()]
    .filter((value) => !known.has(value))
    .map(labelled)
    .sort(
      (a, b) =>
        (totals.get(b.value) ?? 0) - (totals.get(a.value) ?? 0) || a.label.localeCompare(b.label, options.locale),
    );
  const missing = chosen.filter((value) => !known.has(value) && !totals.has(value)).map(labelled);
  return [...listed, ...found, ...missing];
}

/**
 * The filter card's numbers for the window: for each facet in `calendarFacetKeys` order its choices, each with the
 * events it would match (every other facet's choices and the search applied, its own not), the events matching all the
 * filters, and the events drawn with nothing applied.
 */
export function calendarFacetCounts(
  loaded: readonly TrackedEventModel[],
  options: EventsCalendarOptions,
  filters: CalendarFilters,
): CalendarCounts {
  const { rows, names } = drawnRows(loaded, options);
  const facets = calendarFacetKeys.map((key): CalendarFacetCounts => {
    const counts = new Map<string, number>();
    for (const row of rows)
      if (keeps(row, filters, key))
        for (const value of row.values[key]) counts.set(value, (counts.get(value) ?? 0) + 1);
    return {
      key,
      options: facetOptions(key, rows, names, filters[key], options).map((option) => ({
        ...option,
        count: counts.get(option.value) ?? 0,
      })),
    };
  });
  return { match: rows.filter((row) => keeps(row, filters)).length, total: rows.length, facets };
}
