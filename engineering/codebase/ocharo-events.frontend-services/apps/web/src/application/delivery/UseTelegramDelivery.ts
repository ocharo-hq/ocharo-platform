import { computed, type MaybeRefOrGetter, toValue } from 'vue';
import type { EventsClient } from '@wow-two-beta/fe-events/kit';

import { eventLinkTemplate, ocharoDigestDefaults, OcharoEventsCatalog } from '@/domain/delivery/TelegramDelivery';
import { createDigestsClient } from '@/integration/delivery/DigestsClient';

/**
 * What the Telegram delivery page hands Events' digest editor for one organization: the client over this app's mirror of
 * the digest routes, the catalog, Ocharo's default digest addressed to the admin, and each event's link to its page here.
 */
export function useTelegramDelivery(organizationId: string, subject: MaybeRefOrGetter<string | null>) {
  const client: EventsClient = createDigestsClient(organizationId);
  return {
    client,
    catalog: OcharoEventsCatalog,
    defaults: computed(() => ocharoDigestDefaults(toValue(subject))),
    linkTemplate: eventLinkTemplate(globalThis.location.origin, organizationId),
  };
}
