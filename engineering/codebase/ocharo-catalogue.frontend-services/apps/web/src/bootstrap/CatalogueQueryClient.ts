import { DefaultRetryPolicy } from '@wow-two-beta/fe/foundation/resilience';
import { createQueryClient } from '@wow-two-beta/fe/query';

import { identityBridge } from '../integration/common/IdentitySession';

/**
 * Pins Catalogue's query engine: one client bound to the session, so a sign-out, an expiry or another person or
 * organization clears every private read. Reads never retry on their own; each reports its own failure in Catalogue's
 * copy, so the client has no global error seam.
 */
export function createCatalogueQueryClient() {
  return createQueryClient({ scope: identityBridge.scope, retry: { ...DefaultRetryPolicy, maxRetries: 0 } });
}
