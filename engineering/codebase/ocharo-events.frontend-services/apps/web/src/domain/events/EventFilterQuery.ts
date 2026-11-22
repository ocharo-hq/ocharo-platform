/**
 * How an events filter lives in an address: the keys the fe-events kit writes for its own state (`toRouteQuery` of
 * `useEventsFilters`), which the feed and the calendar share so one address means the same filters on both pages. A
 * facet's choices are a repeated key (`?kinds=runway&kinds=market`, with no separator, so no value is split), the region
 * one value (`?regions=national`) and the search text `q` (`?q=silk`). Nothing here names a page.
 */

/** The address key of the search text. */
export const searchQueryKey = 'q';

/** The address key each facet is kept under, the kit's: the region is its `scope` facet, the other four are plurals. */
export const filterQueryKeys = {
  scope: 'regions',
  kind: 'kinds',
  stage: 'stages',
  city: 'cities',
  source: 'sources',
} as const;

/**
 * The keys of the filter card on both pages: what a link from one page to the other carries, and nothing else.
 * `when` and `sort` are the feed's own, so they stay on the feed.
 */
export const sharedFilterQueryKeys: readonly string[] = [
  searchQueryKey,
  filterQueryKeys.stage,
  filterQueryKeys.kind,
  filterQueryKeys.city,
  filterQueryKeys.source,
  filterQueryKeys.scope,
];

/** Every key the feed keeps in its address: the shared ones, the date preset (`when`) and the sort. */
export const feedFilterQueryKeys: readonly string[] = [...sharedFilterQueryKeys, 'when', 'sort'];

/** The strings a query value holds: a repeated key gives several, anything but a string (a bare `?kinds`) is dropped. */
function textsOf(value: unknown): string[] {
  return (Array.isArray(value) ? value : [value]).filter((part): part is string => typeof part === 'string');
}

/**
 * Reads the values a facet's query key chooses, in the order listed: each trimmed, once, an empty one left out
 * (`?kinds=runway&kinds=&kinds=runway` reads `['runway']`). A comma is part of a value, as the kit reads it.
 */
export function readQueryValues(value: unknown): string[] {
  const listed = textsOf(value)
    .map((part) => part.trim())
    .filter((part) => part !== '');
  return [...new Set(listed)];
}

/** The query value for chosen values, in the order given; undefined for none, which leaves the key out of the address. */
export function writeQueryValues(values: readonly string[]): string[] | undefined {
  return values.length ? [...values] : undefined;
}

/** Reads the search text of the address's `q` key as written (the first one when repeated); empty when absent. */
export function readQueryText(value: unknown): string {
  return textsOf(value)[0] ?? '';
}

/** The `q` value for a search text; undefined for none, or only blanks, which leaves the key out of the address. */
export function writeQueryText(text: string): string | undefined {
  return text.trim() === '' ? undefined : text;
}

/**
 * The part of an address's query the filter card owns, for a link to the other page: the shared keys with their
 * non-empty text values, in the keys' order; every other key (`tab`, `when`, `sort`) is left behind.
 */
export function sharedFilterQuery(query: Readonly<Record<string, unknown>>): Record<string, string[]> {
  const carried: Record<string, string[]> = {};
  for (const key of sharedFilterQueryKeys) {
    const values = textsOf(query[key]).filter((part) => part !== '');
    if (values.length) carried[key] = values;
  }
  return carried;
}

/** A path with the query's shared filter keys after it, as one address a link can point at; the bare path for none. */
export function withSharedFilters(path: string, query: Readonly<Record<string, unknown>>): string {
  const params = new URLSearchParams();
  for (const [key, values] of Object.entries(sharedFilterQuery(query)))
    for (const value of values) params.append(key, value);
  const text = params.toString();
  return text ? `${path}?${text}` : path;
}
