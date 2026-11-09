/**
 * Catalogue's server reads in the query cache. The app's query client is bound to the session, so a sign-out or another
 * person or organization clears every key here; no key carries the session itself.
 */
export const CatalogueQueryKeys = {
  /** Every Catalogue read. */
  all: ['catalogue'] as const,
  /** The catalogue settings, their revision and the import source, read together. */
  settings: ['catalogue', 'settings'] as const,
  /** The published brand kit, read from the Brand API root `apiUrl`. */
  brandKit: (apiUrl: string | null) => ['catalogue', 'brand-kit', apiUrl] as const,
} as const;
