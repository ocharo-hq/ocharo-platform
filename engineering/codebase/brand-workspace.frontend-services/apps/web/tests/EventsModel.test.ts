import { describe, expect, it } from 'vitest';

import { safeWorkspaceDestination } from '@/domain/workspace/EditorNavigation';
import {
  daysLeftLabel, daysUntil, deadlineTone, eventDraft, eventInput, eventsPath, formatEventDates, groupEventsByMonth, localIsoDate, monthHeading,
  safeEventUrl, validateEventDraft, type ShowcaseEvent,
} from '@/domain/workspace/Events';
import { readWorkspaceLocation } from '@/domain/workspace/Models';
import { eventListQuery, isEventSummary, isShowcaseEvent } from '@/integration/workspace/EventsApi';

const organization = '11111111-1111-4111-8111-111111111111';
const eventId = '22222222-2222-4222-8222-222222222222';
const at = '2026-10-01T00:00:00Z';
const showcase = (over: Partial<ShowcaseEvent> = {}): ShowcaseEvent => ({
  id: eventId, key: null, title: 'Tashkent Fashion Week', organizer: 'Fashion Council', scope: 'local', format: 'runway',
  country: 'UZ', city: 'Tashkent', venue: null, startsOn: '2026-10-12', endsOn: '2026-10-14', datePrecision: 'day',
  applicationDeadline: null, participation: '', cost: null, website: null, sourceUrl: null,
  status: 'shortlisted', priority: 'normal', notes: '', revision: 1, createdAt: at, updatedAt: at, ...over,
});

describe('events locations', () => {
  it('reads the feed and one event, and falls back to the feed for anything else', () => {
    expect(readWorkspaceLocation(`/organizations/${organization}/events`))
      .toEqual({ organizationId: organization, section: 'events', draftId: null, events: { view: 'feed', eventId: null } });
    expect(readWorkspaceLocation(`/organizations/${organization}/events/${eventId}`).events).toEqual({ view: 'event', eventId });
    expect(readWorkspaceLocation(`/organizations/${organization}/events/${eventId.toUpperCase()}`).events).toEqual({ view: 'event', eventId });
    for (const tail of ['new', 'not-an-event', `${eventId}/extra`, '12345'])
      expect(readWorkspaceLocation(`/organizations/${organization}/events/${tail}`).events).toEqual({ view: 'feed', eventId: null });
    expect(readWorkspaceLocation(`/organizations/${organization}/sourcing`)).not.toHaveProperty('events');
  });

  it('builds addresses that read back to the same location and survive sign-in', () => {
    for (const location of [{ view: 'feed', eventId: null }, { view: 'event', eventId }] as const) {
      const path = eventsPath(organization, location);
      expect(readWorkspaceLocation(path).events).toEqual(location);
      expect(safeWorkspaceDestination(path)).toBe(path);
    }
    expect(eventsPath(organization, { view: 'event', eventId: 'javascript:alert(1)' })).toBe(`/organizations/${organization}/events`);
    expect(() => eventsPath('not-an-organization', { view: 'feed', eventId: null })).toThrow();
    expect(safeWorkspaceDestination(`/organizations/${organization}/events/new`)).toBe('/');
  });
});

describe('event date ranges', () => {
  it('renders day precision as a compact range', () => {
    expect(formatEventDates(showcase(), 'en')).toBe('12–14 Oct 2026');
    expect(formatEventDates(showcase({ endsOn: null }), 'en')).toBe('12 Oct 2026');
    expect(formatEventDates(showcase({ endsOn: '2026-10-12' }), 'en')).toBe('12 Oct 2026');
    expect(formatEventDates(showcase({ startsOn: '2026-09-30', endsOn: '2026-10-02' }), 'en')).toBe('30 Sep – 2 Oct 2026');
    expect(formatEventDates(showcase({ startsOn: '2026-12-30', endsOn: '2027-01-02' }), 'en')).toBe('30 Dec 2026 – 2 Jan 2027');
  });

  it('renders month, estimated and unknown precision without inventing days', () => {
    expect(formatEventDates(showcase({ datePrecision: 'month', startsOn: '2026-11-01', endsOn: null }), 'en')).toBe('Nov 2026');
    expect(formatEventDates(showcase({ datePrecision: 'month', startsOn: '2026-11-01', endsOn: '2026-12-31' }), 'en')).toBe('Nov – Dec 2026');
    expect(formatEventDates(showcase({ datePrecision: 'estimated', startsOn: '2027-04-01', endsOn: null }), 'en')).toBe('≈ Apr 2027 (expected)');
    expect(formatEventDates(showcase({ datePrecision: 'unknown', startsOn: null, endsOn: null }), 'en')).toBe('Dates not announced');
  });

  it('uses the operator language for month names and wording', () => {
    expect(formatEventDates(showcase(), 'ru')).toBe('12–14 окт. 2026');
    expect(formatEventDates(showcase({ datePrecision: 'estimated', startsOn: '2027-04-01', endsOn: null }), 'ru')).toBe('≈ апр. 2027 (ожидается)');
    expect(formatEventDates(showcase({ datePrecision: 'unknown', startsOn: null, endsOn: null }), 'uz')).toBe('Sanalar e’lon qilinmagan');
    expect(monthHeading('2026-10', 'en')).toBe('October 2026');
    expect(monthHeading('2026-10', 'uz')).toBe('Oktabr 2026');
  });
});

describe('month grouping', () => {
  it('groups by start month in date order and closes with the undated events', () => {
    const groups = groupEventsByMonth([
      showcase({ id: 'undated', datePrecision: 'unknown', startsOn: null, endsOn: null }),
      showcase({ id: 'nov', datePrecision: 'month', startsOn: '2026-11-01', endsOn: null }),
      showcase({ id: 'oct-a', startsOn: '2026-10-12' }),
      showcase({ id: 'oct-b', startsOn: '2026-10-30', endsOn: '2026-11-02' }),
      showcase({ id: 'apr', datePrecision: 'estimated', startsOn: '2027-04-01', endsOn: null }),
    ]);
    expect(groups.map(group => group.key)).toEqual(['2026-10', '2026-11', '2027-04', 'undated']);
    expect(groups.map(group => group.events.map(event => event.id))).toEqual([['oct-a', 'oct-b'], ['nov'], ['apr'], ['undated']]);
    expect(groupEventsByMonth([])).toEqual([]);
  });
});

describe('feed query parameters', () => {
  it('sends only the filters that are set, with from=today while past events are hidden', () => {
    const page = { limit: 200, offset: 0 };
    expect(eventListQuery({ query: '  ', scope: '', format: '', status: '', from: '2026-10-08' }, page).toString())
      .toBe('from=2026-10-08&limit=200&offset=0');
    expect(eventListQuery({ query: ' silk ', scope: 'international', format: 'trade-fair', status: 'candidate', from: null }, page).toString())
      .toBe('query=silk&scope=international&format=trade-fair&status=candidate&limit=200&offset=0');
  });
});

describe('deadlines', () => {
  it('counts calendar days from the local today and marks two weeks as urgent', () => {
    expect(localIsoDate(new Date(2026, 9, 8, 23, 59))).toBe('2026-10-08');
    expect(daysUntil('2026-10-22', '2026-10-08')).toBe(14);
    expect(daysUntil('2026-10-23', '2026-10-08')).toBe(15);
    expect(daysUntil('2027-03-29', '2027-03-27')).toBe(2);
    expect(deadlineTone(14)).toBe('urgent');
    expect(deadlineTone(0)).toBe('urgent');
    expect(deadlineTone(15)).toBe('upcoming');
    expect(deadlineTone(-1)).toBe('passed');
    expect([0, 1, 9, -3].map(days => daysLeftLabel(days, 'en'))).toEqual(['Deadline today', '1 day left', '9 days left', 'Deadline passed']);
  });
});

describe('event form validation', () => {
  it('mirrors the contract: end after start, required start, null dates when unknown', () => {
    const draft = { ...eventDraft(), title: 'Expo', datePrecision: 'day' as const, startDay: '2026-10-14', endDay: '2026-10-12' };
    expect(validateEventDraft(draft).problems).toContain('The end date cannot be before the start date.');
    expect(validateEventDraft({ ...draft, startDay: '', endDay: '' }).problems).toContain('Enter the start date, or choose another date precision.');
    expect(validateEventDraft({ ...draft, startDay: '1999-12-31', endDay: '' }).problems).toContain('Dates must be between 2000-01-01 and 2100-12-31.');
    const unknown = validateEventDraft({ ...draft, datePrecision: 'unknown' });
    expect(unknown.input).toMatchObject({ datePrecision: 'unknown', startsOn: null, endsOn: null, status: 'shortlisted', priority: 'normal' });
  });

  it('stores the 1st for month pickers and normalizes blanks and country codes', () => {
    const draft = { ...eventDraft(), title: ' Expo ', datePrecision: 'estimated' as const, startMonth: '2027-04', endMonth: '', country: 'kz', cost: '  ', organizer: ' ' };
    expect(validateEventDraft(draft).input).toMatchObject({ title: 'Expo', startsOn: '2027-04-01', endsOn: null, country: 'KZ', cost: null, organizer: null });
    expect(validateEventDraft({ ...draft, endMonth: '2027-05' }).input).toMatchObject({ startsOn: '2027-04-01', endsOn: '2027-05-31' });
    expect(validateEventDraft({ ...draft, startMonth: '2027-05', endMonth: '2027-04' }).problems).toContain('The end date cannot be before the start date.');
    expect(validateEventDraft({ ...draft, startMonth: '' }).problems).toContain('Choose the month the event starts in.');
    expect(validateEventDraft({ ...draft, website: 'javascript:alert(1)' }).problems).toContain('Enter the website as a full http:// or https:// address.');
  });

  it('round-trips an existing event without changing any field', () => {
    for (const event of [
      showcase({ cost: '300 USD', website: 'https://tfw.example.test/', participation: 'Application package', notes: 'Fit' }),
      showcase({ datePrecision: 'month', startsOn: '2026-11-01', endsOn: '2026-12-01' }),
      showcase({ datePrecision: 'unknown', startsOn: null, endsOn: null, applicationDeadline: '2026-10-20' }),
    ]) {
      expect(validateEventDraft(eventDraft(event), event).input).toEqual(eventInput(event));
    }
  });
});

describe('events transport guards', () => {
  it('accepts the contract shape and rejects unknown enums, bad dates and unsafe links stay inert', () => {
    expect(isShowcaseEvent(showcase())).toBe(true);
    expect(isShowcaseEvent({ ...showcase(), status: 'registered' })).toBe(false);
    expect(isShowcaseEvent({ ...showcase(), format: 'gala' })).toBe(false);
    expect(isShowcaseEvent({ ...showcase(), startsOn: '12/10/2026' })).toBe(false);
    expect(isShowcaseEvent({ ...showcase(), revision: 0 })).toBe(false);
    expect(isEventSummary({ byStatus: { candidate: 3, shortlisted: 1 }, upcomingDeadlines: [showcase()] })).toBe(true);
    expect(isEventSummary({ byStatus: { unknown: 1 }, upcomingDeadlines: [] })).toBe(false);
    expect(safeEventUrl('javascript:alert(1)')).toBeNull();
    expect(safeEventUrl('https://tfw.example.test/apply')).toBe('https://tfw.example.test/apply');
  });
});
