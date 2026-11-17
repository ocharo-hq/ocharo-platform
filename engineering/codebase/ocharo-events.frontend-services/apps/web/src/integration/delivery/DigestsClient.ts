import { createEventsClient, type EventsClient } from '@wow-two-beta/fe-events/kit';

import { deliveryApiPrefix } from '@/domain/delivery/TelegramDelivery';
import { identityStrategy, revalidateSession } from '@/integration/common/IdentitySession';

/**
 * The digest editor's transport: WoW2 Events' digest routes as this app mirrors them for the organization
 * (`/api/organizations/{id}/delivery/api/events/v1/…`), with the session cookie and its CSRF token on every write; a 401
 * re-reads the session, so the frame offers sign-in again.
 */
export function createDigestsClient(organizationId: string): EventsClient {
  return createEventsClient({
    baseUrl: deliveryApiPrefix(organizationId),
    csrfToken: () => identityStrategy.getCsrfToken(),
    onSignInRequired: () => void revalidateSession(),
  });
}
