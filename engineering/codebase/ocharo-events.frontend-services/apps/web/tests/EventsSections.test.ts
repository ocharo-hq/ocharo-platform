import { describe, expect, it } from 'vitest';

import {
  EventsGroupLabels,
  EventsSectionGroup,
  EventsSections,
  eventsSection,
  eventsSectionPath,
  readEventsSection,
  visibleEventsSections,
} from '@/domain/shared/EventsSections';

const organization = '11111111-1111-4111-8111-111111111111';

describe('events sections', () => {
  it('lists the feed and the calendar, then Reminders and Telegram delivery under Settings', () => {
    expect(EventsSections.map((section) => [section.id, section.label, section.group])).toEqual([
      ['feed', 'Feed', EventsSectionGroup.Main],
      ['calendar', 'Calendar', EventsSectionGroup.Main],
      ['reminders', 'Reminders', EventsSectionGroup.Settings],
      ['telegram', 'Telegram delivery', EventsSectionGroup.Settings],
    ]);
    expect(EventsGroupLabels).toEqual({ main: null, settings: 'Settings' });
  });

  it('offers Telegram delivery to an admin only', () => {
    expect(visibleEventsSections({ canAdmin: true }).map((section) => section.id)).toEqual([
      'feed',
      'calendar',
      'reminders',
      'telegram',
    ]);
    expect(visibleEventsSections({ canAdmin: false }).map((section) => section.id)).toEqual([
      'feed',
      'calendar',
      'reminders',
    ]);
  });

  it('addresses each section under the session organization, Telegram delivery at its existing path', () => {
    expect(EventsSections.map((section) => eventsSectionPath(section.id, organization))).toEqual([
      `/organizations/${organization}/events`,
      `/organizations/${organization}/calendar`,
      `/organizations/${organization}/reminders`,
      `/organizations/${organization}/telegram-delivery`,
    ]);
    expect(() => eventsSectionPath('calendar', '../admin')).toThrow();
  });

  it('reads a route’s section from its metadata, and none for a place outside every section', () => {
    expect(readEventsSection({ section: 'calendar' })).toBe('calendar');
    expect(readEventsSection({ section: 'deadlines' })).toBeNull();
    expect(readEventsSection({ section: null })).toBeNull();
    expect(readEventsSection({})).toBeNull();
    expect(eventsSection('reminders')?.label).toBe('Reminders');
    expect(eventsSection(null)).toBeNull();
  });
});
