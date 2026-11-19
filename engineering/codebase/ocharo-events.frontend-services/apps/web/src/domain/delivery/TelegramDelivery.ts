import type { DigestSpec } from '@wow-two-beta/fe-events/contracts';

import { ocharoClosedStages, type EventStatus } from '@/domain/events/Events';

export { OcharoEventsCatalog, ocharoClosedStages, ocharoWorkflow } from '@/domain/events/Events';

/**
 * Telegram delivery: the organization's events going out to its people through Ocharo's Telegram bot, on a schedule its
 * admins set (tasks-platform.md O17). WoW2 Events keeps each schedule as a digest of the Events page's catalog and sends
 * through Ocharo's Identity; this app only edits them.
 */

const identifier = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** The settings page's address for an organization. */
export function deliveryPath(organizationId: string): string {
  if (!identifier.test(organizationId)) throw new Error('Invalid workspace destination.');
  return `/organizations/${organizationId}/telegram-delivery`;
}

/** Where this app mirrors Events' digest routes for the organization: the `fe-events` client's prefix. */
export function deliveryApiPrefix(organizationId: string): string {
  return `/api/organizations/${encodeURIComponent(organizationId)}/delivery`;
}

/**
 * Each event's link in a message: the Events app's own event page on this origin, Events filling `{id}`. Only an http(s)
 * origin gives one; anything else leaves the link to Events' configured template.
 */
export function eventLinkTemplate(origin: string, organizationId: string): string | null {
  if (!/^https?:\/\/[^/]+$/i.test(origin) || !identifier.test(organizationId)) return null;
  return `${origin}/organizations/${organizationId.toLowerCase()}/events/{id}`;
}

/** The stages of events the team is pursuing, which a deadline reminder is about. */
export const ocharoPursuedStages: readonly EventStatus[] = ['shortlisted', 'applying', 'applied'];

/** The name of the second digest an admin can add (OE05), in Uzbek like the default's: "Deadline reminders". */
export const ocharoDeadlineReminderName = 'Muddat eslatmalari';

/**
 * Ocharo's default digest (O17): weekly on Monday at 09:00 in Tashkent, new events and those starting within 30 days plus
 * deadlines within 14 days, in Uzbek, leaving out the events the team skipped or rejected (OE05). Recipients default to
 * every member of the organization (`role: member`, which Events resolves through Identity's member list at each run); a
 * single person is still named by Identity `sub`.
 */
export function ocharoDigestDefaults(): Partial<DigestSpec> {
  return {
    name: 'Ocharo tadbirlari',
    enabled: true,
    channel: 'telegram',
    schedule: { days: ['monday'], times: ['09:00'], timeZone: 'Asia/Tashkent' },
    selection: {
      newSinceLastRun: true,
      startingWithinDays: 30,
      deadlineWithinDays: 14,
      excludeStages: [...ocharoClosedStages],
    },
    recipients: [{ role: 'member' }],
    language: 'uz-Latn',
    itemCap: 20,
  };
}

/**
 * The "Deadline reminders" preset (OE05): every day at 09:00 in Tashkent, the events the team shortlisted, is applying to
 * or has applied to with an open deadline within 7 days, to every member, in Uzbek. It is created switched off, for an
 * admin to review and switch on; a link template, when given, fixes each event's link as the editor does.
 */
export function ocharoDeadlineReminder(linkTemplate: string | null = null): DigestSpec {
  return {
    name: ocharoDeadlineReminderName,
    enabled: false,
    channel: 'telegram',
    schedule: {
      days: ['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday'],
      times: ['09:00'],
      timeZone: 'Asia/Tashkent',
    },
    selection: { deadlineWithinDays: 7, stages: [...ocharoPursuedStages] },
    recipients: [{ role: 'member' }],
    language: 'uz-Latn',
    itemCap: 20,
    ...(linkTemplate ? { linkTemplate } : {}),
  };
}
