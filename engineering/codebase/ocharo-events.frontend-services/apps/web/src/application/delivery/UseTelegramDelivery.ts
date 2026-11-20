import type { EventsClient } from '@wow-two-beta/fe-events/kit';

import {
  eventLinkTemplate,
  ocharoDigestDefaults,
  OcharoEventsCatalog,
  ocharoWorkflow,
} from '@/domain/delivery/TelegramDelivery';
import { createDigestsClient } from '@/integration/delivery/DigestsClient';

/**
 * What the Telegram delivery page hands Events' digest editor for one organization: the client over this app's mirror of
 * the digest routes, the catalog, Ocharo's default digest addressed to every member, Ocharo's stages for the editor's stage
 * pickers, and each event's link to its page here.
 */
export function useTelegramDelivery(organizationId: string) {
  const client: EventsClient = createDigestsClient(organizationId);
  const linkTemplate = eventLinkTemplate(globalThis.location.origin, organizationId);
  return {
    client,
    catalog: OcharoEventsCatalog,
    defaults: ocharoDigestDefaults(),
    workflow: ocharoWorkflow(),
    linkTemplate,
  };
}
