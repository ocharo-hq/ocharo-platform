import { eventsPath, eventsPlacePath } from '@/domain/events/Events';
import { deliveryPath } from '@/domain/delivery/TelegramDelivery';

/** Events' places as the frame lists them: the sidebar, the icon rail, the phone bar and the breadcrumb. */
export const EventsSectionGroup = { Main: 'main', Settings: 'settings' } as const;

export type EventsSectionGroup = (typeof EventsSectionGroup)[keyof typeof EventsSectionGroup];

/** Names one section of Events. */
export type EventsSection = 'feed' | 'calendar' | 'reminders' | 'telegram';

/** One sidebar destination. */
export interface EventsSectionEntry {
  readonly id: EventsSection;
  /** The English source label; the frame translates it. */
  readonly label: string;
  readonly group: EventsSectionGroup;
  /** Whether only an organization admin (`events:admin`) is offered it; the page answers a member itself. */
  readonly adminOnly: boolean;
}

/** An entry as one session sees it: its address for the session's organization. */
export interface EventsSectionLink extends EventsSectionEntry {
  readonly href: string;
}

/**
 * The sections in display order: the feed and the calendar, then Settings with the reminders and the Telegram delivery.
 * The Telegram delivery is the admins' page, so only they are offered it. The phone's bottom bar carries every one the
 * session is offered.
 */
export const EventsSections: readonly EventsSectionEntry[] = [
  { id: 'feed', label: 'Feed', group: EventsSectionGroup.Main, adminOnly: false },
  { id: 'calendar', label: 'Calendar', group: EventsSectionGroup.Main, adminOnly: false },
  { id: 'reminders', label: 'Reminders', group: EventsSectionGroup.Settings, adminOnly: false },
  { id: 'telegram', label: 'Telegram delivery', group: EventsSectionGroup.Settings, adminOnly: true },
];

/** The heading each group carries in the sidebar; the main group has none. */
export const EventsGroupLabels: Readonly<Record<EventsSectionGroup, string | null>> = {
  main: null,
  settings: 'Settings',
};

/** Finds a section by its id. */
export function eventsSection(id: EventsSection | null | undefined): EventsSectionEntry | null {
  return EventsSections.find((section) => section.id === id) ?? null;
}

/** Reads a route's section from its metadata; a place outside every section yields `null`. */
export function readEventsSection(meta: Readonly<Record<string, unknown>>): EventsSection | null {
  const value = meta.section;
  return EventsSections.some((section) => section.id === value) ? (value as EventsSection) : null;
}

/** The sections a session is offered: every reader sees all but the admins' Telegram delivery. */
export function visibleEventsSections(access: { readonly canAdmin: boolean }): readonly EventsSectionEntry[] {
  return EventsSections.filter((section) => access.canAdmin || !section.adminOnly);
}

/** A section's address for an organization; throws for an id that is not an organization identifier. */
export function eventsSectionPath(id: EventsSection, organizationId: string): string {
  switch (id) {
    case 'feed':
      return eventsPath(organizationId, { view: 'feed', eventId: null });
    case 'telegram':
      return deliveryPath(organizationId);
    default:
      return eventsPlacePath(organizationId, id);
  }
}
