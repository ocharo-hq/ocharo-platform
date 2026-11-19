import { ref } from 'vue';
import type { EventsClient, EventsFailure } from '@wow-two-beta/fe-events/kit';

import {
  eventLinkTemplate,
  ocharoDeadlineReminder,
  ocharoDeadlineReminderName,
  ocharoDigestDefaults,
  OcharoEventsCatalog,
  ocharoWorkflow,
} from '@/domain/delivery/TelegramDelivery';
import { createDigestsClient } from '@/integration/delivery/DigestsClient';

/**
 * What the Telegram delivery page hands Events' digest editor for one organization: the client over this app's mirror of
 * the digest routes, the catalog, Ocharo's default digest addressed to every member, Ocharo's stages for the editor's stage
 * pickers, and each event's link to its page here. It also adds the "Deadline reminders" digest (OE05) through the mirror,
 * once: the offer shows only while the organization has no digest of that name.
 */
export function useTelegramDelivery(organizationId: string) {
  const client: EventsClient = createDigestsClient(organizationId);
  const linkTemplate = eventLinkTemplate(globalThis.location.origin, organizationId);
  /** Whether the organization already has the reminder digest; null until the list is read, and when it cannot be. */
  const reminderPresent = ref<boolean | null>(null);
  const reminderAdding = ref(false);
  const reminderFailure = ref<EventsFailure | null>(null);
  /** Changes when a digest is added here, so the digest panel reads the list again. */
  const panelVersion = ref(0);

  async function readReminder(): Promise<void> {
    const listed = await client.listDigests(OcharoEventsCatalog);
    reminderPresent.value = listed.ok
      ? listed.value.some((digest) => digest.name === ocharoDeadlineReminderName)
      : null;
  }

  async function addReminder(): Promise<void> {
    if (reminderAdding.value) return;
    reminderAdding.value = true;
    reminderFailure.value = null;
    const created = await client.createDigest(OcharoEventsCatalog, ocharoDeadlineReminder(linkTemplate));
    reminderAdding.value = false;
    if (created.ok) {
      reminderPresent.value = true;
      panelVersion.value += 1;
    } else reminderFailure.value = created.failure;
  }

  return {
    client,
    catalog: OcharoEventsCatalog,
    defaults: ocharoDigestDefaults(),
    workflow: ocharoWorkflow(),
    linkTemplate,
    reminderPresent,
    reminderAdding,
    reminderFailure,
    panelVersion,
    readReminder,
    addReminder,
  };
}
