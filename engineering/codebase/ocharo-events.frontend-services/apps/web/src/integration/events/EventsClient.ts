import { createEventsClient, type EventsClient } from '@wow-two-beta/fe-events/kit';

import { identityStrategy, revalidateSession } from '@/integration/common/IdentitySession';

/**
 * A `fe-events` client over one of this app's mirrors of WoW2 Events (`/api/organizations/{id}/events-api` for the page,
 * `/api/organizations/{id}/delivery` for the digest editor): the session cookie, its CSRF token on every write, and the
 * session re-read after a 401 or a 403, so the frame offers sign-in again or follows the person's changed permissions.
 */
export function createOcharoEventsClient(baseUrl: string): EventsClient {
  return createEventsClient({
    baseUrl,
    csrfToken: () => identityStrategy.getCsrfToken(),
    onSignInRequired: () => void revalidateSession(),
    fetch: async (input, init) => {
      const response = await globalThis.fetch(input, init);
      if (response.status === 403) void revalidateSession();
      return response;
    },
  });
}
