import type { DigestSpec } from '@wow-two-beta/fe-events/contracts';

/**
 * Telegram delivery: the organization's events going out to its people through Ocharo's Telegram bot, on a schedule its
 * admins set (tasks-platform.md O17). WoW2 Events keeps each schedule as a digest of the Events page's catalog and sends
 * through Ocharo's Identity; this app only edits them.
 */

/** The catalog the Events page reads (`Events:Catalog`); the delivery routes serve only it. */
export const OcharoEventsCatalog = 'ocharo-fashion';

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

/**
 * Ocharo's default digest (O17): weekly on Monday at 09:00 in Tashkent, new events and those starting within 30 days plus
 * deadlines within 14 days, in Uzbek. Recipients are named one by one, by their Identity `sub`, starting with the admin
 * who sets it up: Identity offers no list of an organization's members to a service, so a role reaches nobody.
 */
export function ocharoDigestDefaults(subject: string | null | undefined): Partial<DigestSpec> {
  return {
    name: 'Ocharo tadbirlari',
    enabled: true,
    channel: 'telegram',
    schedule: { days: ['monday'], times: ['09:00'], timeZone: 'Asia/Tashkent' },
    selection: { newSinceLastRun: true, startingWithinDays: 30, deadlineWithinDays: 14 },
    recipients: subject ? [{ subject }] : [],
    language: 'uz-Latn',
    itemCap: 20,
  };
}
