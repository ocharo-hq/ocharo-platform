import { describe, expect, it } from 'vitest';

import {
  feedFilterQueryKeys,
  filterQueryKeys,
  readQueryText,
  readQueryValues,
  searchQueryKey,
  sharedFilterQuery,
  sharedFilterQueryKeys,
  withSharedFilters,
  writeQueryText,
  writeQueryValues,
} from '@/domain/events/EventFilterQuery';

describe('filter values in an address', () => {
  it('reads the values of a repeated key once each, trimmed, in the order listed', () => {
    expect(readQueryValues(['runway', 'trade-fair'])).toEqual(['runway', 'trade-fair']);
    expect(readQueryValues([' trade-fair ', 'runway', 'trade-fair'])).toEqual(['trade-fair', 'runway']);
    expect(readQueryValues('runway')).toEqual(['runway']);
  });

  it('does not split a value at a comma, as the kit reads it', () => {
    expect(readQueryValues('runway,trade-fair')).toEqual(['runway,trade-fair']);
    expect(readQueryValues(['tashkent, uzbekistan'])).toEqual(['tashkent, uzbekistan']);
  });

  it('leaves out an empty value', () => {
    expect(readQueryValues(['runway', '', '  ', null])).toEqual(['runway']);
  });

  it('reads no value from an absent key, a bare key or a blank one', () => {
    expect(readQueryValues(undefined)).toEqual([]);
    expect(readQueryValues(null)).toEqual([]);
    expect(readQueryValues([null])).toEqual([]);
    expect(readQueryValues('  ')).toEqual([]);
  });

  it('writes the values as a list in the order given, which a router writes as a repeated key, and nothing for none', () => {
    expect(writeQueryValues(['trade-fair', 'runway'])).toEqual(['trade-fair', 'runway']);
    expect(writeQueryValues(['runway'])).toEqual(['runway']);
    expect(writeQueryValues([])).toBeUndefined();
  });

  it('reads the search text as written under q, the first one when it is repeated', () => {
    expect(searchQueryKey).toBe('q');
    expect(readQueryText('silk road')).toBe('silk road');
    expect(readQueryText(' silk ')).toBe(' silk ');
    expect(readQueryText(['silk', 'fashion'])).toBe('silk');
    expect(readQueryText(undefined)).toBe('');
    expect(readQueryText(null)).toBe('');
  });

  it('writes the search text as typed, and nothing for none or only blanks', () => {
    expect(writeQueryText('silk ')).toBe('silk ');
    expect(writeQueryText('')).toBeUndefined();
    expect(writeQueryText('   ')).toBeUndefined();
  });
});

describe('the keys both pages share', () => {
  it('are the kit’s route keys: q, stages, kinds, cities, sources and regions, one per facet', () => {
    expect(filterQueryKeys).toEqual({
      scope: 'regions',
      kind: 'kinds',
      stage: 'stages',
      city: 'cities',
      source: 'sources',
    });
    expect([...sharedFilterQueryKeys].sort()).toEqual(['cities', 'kinds', 'q', 'regions', 'sources', 'stages']);
  });

  it('add the date preset and the sort for the feed alone', () => {
    expect([...feedFilterQueryKeys].sort()).toEqual([
      'cities',
      'kinds',
      'q',
      'regions',
      'sort',
      'sources',
      'stages',
      'when',
    ]);
    expect(sharedFilterQueryKeys).not.toContain('when');
    expect(sharedFilterQueryKeys).not.toContain('sort');
  });
});

describe('the filters a link carries to the other page', () => {
  const query = {
    kinds: ['runway', 'market'],
    regions: 'national',
    q: 'silk road',
    when: 'past',
    sort: 'added',
    tab: 'x',
    cities: [null, 'tashkent'],
    sources: '',
  };

  it('keeps the shared keys with their values and leaves every other key behind', () => {
    expect(sharedFilterQuery(query)).toEqual({
      q: ['silk road'],
      kinds: ['runway', 'market'],
      cities: ['tashkent'],
      regions: ['national'],
    });
    expect(sharedFilterQuery({})).toEqual({});
  });

  it('writes them after the path as one address, a list as a repeated key, and the bare path for none', () => {
    expect(withSharedFilters('/organizations/o/calendar', query)).toBe(
      '/organizations/o/calendar?q=silk+road&kinds=runway&kinds=market&cities=tashkent&regions=national',
    );
    expect(withSharedFilters('/organizations/o/calendar', { when: 'past', tab: 'x' })).toBe(
      '/organizations/o/calendar',
    );
  });
});
