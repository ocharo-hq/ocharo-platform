/**
 * Brand's server reads in the query cache. The app's query client is bound to the session, so a sign-out or another
 * person or organization clears every key here; no key carries the session itself.
 */
export const BrandQueryKeys = {
  /** Every Brand read. */
  all: ['brand'] as const,
  /** The draft, releases, appearances, consumers and published kit, read together as one snapshot. */
  snapshot: ['brand', 'snapshot'] as const,
} as const;
