import { describe, expect, it } from 'vitest';

import {
  daysLeftLabel,
  daysUntil,
  deadlineTone,
  eventDraft,
  eventLimits,
  eventsPath,
  formatAmount,
  formatEventDates,
  formatStartTime,
  groupEventsByMonth,
  localIsoDate,
  monthHeading,
  readEventsAddress,
  readEventsOrganizationId,
  safeEventUrl,
  ticketPriceLabel,
  titleLanguageTag,
  trackingDraft,
  trackingInput,
  validateEventDraft,
  validateTrackingDraft,
  type ShowcaseEvent,
} from '@/domain/events/Events';
import { eventListQuery, isEventSummary, isShowcaseEvent } from '@/integration/events/EventsApi';

const organization = '11111111-1111-4111-8111-111111111111';
const eventId = '22222222-2222-4222-8222-222222222222';
const at = '2026-10-01T00:00:00Z';
const showcase = (over: Partial<ShowcaseEvent> = {}): ShowcaseEvent => ({
  id: eventId,
  key: null,
  title: 'Tashkent Fashion Week',
  titleLanguage: null,
  languages: [],
  organizer: 'Fashion Council',
  scope: 'local',
  format: 'runway',
  eventStatus: 'scheduled',
  attendance: 'offline',
  country: 'UZ',
  city: 'Tashkent',
  venue: null,
  startsOn: '2026-10-12',
  endsOn: '2026-10-14',
  datePrecision: 'day',
  applicationDeadline: null,
  participation: '',
  cost: null,
  tickets: null,
  organizerWebsite: null,
  address: null,
  startsAt: null,
  timeZone: null,
  ticketUrl: null,
  website: null,
  sourceUrl: null,
  status: 'shortlisted',
  priority: 'normal',
  notes: '',
  revision: 1,
  eventRevision: 1,
  createdAt: at,
  updatedAt: at,
  ...over,
});

describe('events locations', () => {
  it('reads the feed and one event, and falls back to the feed for anything else', () => {
    expect(readEventsOrganizationId(`/organizations/${organization}/events`)).toBe(organization);
    expect(readEventsOrganizationId(`/organizations/${organization}/sourcing`)).toBeNull();
    expect(readEventsAddress(`/organizations/${organization}/events`)).toEqual({ view: 'feed', eventId: null });
    expect(readEventsAddress(`/organizations/${organization}/events/${eventId}`)).toEqual({ view: 'event', eventId });
    expect(readEventsAddress(`/organizations/${organization}/events/${eventId.toUpperCase()}`)).toEqual({
      view: 'event',
      eventId,
    });
    for (const tail of ['new', 'not-an-event', `${eventId}/extra`, '12345'])
      expect(readEventsAddress(`/organizations/${organization}/events/${tail}`)).toEqual({
        view: 'feed',
        eventId: null,
      });
    expect(readEventsAddress(`/organizations/${organization}/sourcing`)).toBeNull();
  });

  it('builds addresses that read back to the same location', () => {
    for (const location of [
      { view: 'feed', eventId: null },
      { view: 'event', eventId },
    ] as const) {
      const path = eventsPath(organization, location);
      expect(readEventsAddress(path)).toEqual(location);
    }
    expect(eventsPath(organization, { view: 'event', eventId: 'javascript:alert(1)' })).toBe(
      `/organizations/${organization}/events`,
    );
    expect(() => eventsPath('not-an-organization', { view: 'feed', eventId: null })).toThrow();
  });
});

describe('event date ranges', () => {
  it('renders day precision as a compact range', () => {
    expect(formatEventDates(showcase(), 'en')).toBe('12–14 Oct 2026');
    expect(formatEventDates(showcase({ endsOn: null }), 'en')).toBe('12 Oct 2026');
    expect(formatEventDates(showcase({ endsOn: '2026-10-12' }), 'en')).toBe('12 Oct 2026');
    expect(formatEventDates(showcase({ startsOn: '2026-09-30', endsOn: '2026-10-02' }), 'en')).toBe(
      '30 Sep – 2 Oct 2026',
    );
    expect(formatEventDates(showcase({ startsOn: '2026-12-30', endsOn: '2027-01-02' }), 'en')).toBe(
      '30 Dec 2026 – 2 Jan 2027',
    );
  });

  it('renders month, estimated and unknown precision without inventing days', () => {
    expect(formatEventDates(showcase({ datePrecision: 'month', startsOn: '2026-11-01', endsOn: null }), 'en')).toBe(
      'Nov 2026',
    );
    expect(
      formatEventDates(showcase({ datePrecision: 'month', startsOn: '2026-11-01', endsOn: '2026-12-31' }), 'en'),
    ).toBe('Nov – Dec 2026');
    expect(formatEventDates(showcase({ datePrecision: 'estimated', startsOn: '2027-04-01', endsOn: null }), 'en')).toBe(
      '≈ Apr 2027 (expected)',
    );
    expect(formatEventDates(showcase({ datePrecision: 'unknown', startsOn: null, endsOn: null }), 'en')).toBe(
      'Dates not announced',
    );
  });

  it("adds the start time the source announced, with its zone only where it is not the viewer's", () => {
    // The golden Afisha jazz evening, as Events serves it since E08: 2026-12-05T19:00 in Asia/Tashkent.
    const jazz = showcase({
      startsOn: '2026-12-05',
      endsOn: null,
      startsAt: '19:00',
      timeZone: 'Asia/Tashkent',
    });
    expect(formatEventDates(jazz, 'en', 'Asia/Tashkent')).toBe('5 Dec 2026, 19:00');
    expect(formatEventDates(jazz, 'en', 'Asia/Samarkand')).toBe('5 Dec 2026, 19:00');
    expect(formatEventDates(jazz, 'en', 'Europe/Berlin')).toBe('5 Dec 2026, 19:00 GMT+5');
    expect(formatEventDates({ ...jazz, timeZone: null }, 'en', 'Europe/Berlin')).toBe('5 Dec 2026, 19:00');
    expect(formatEventDates({ ...jazz, timeZone: 'Not/AZone' }, 'en', 'Europe/Berlin')).toBe('5 Dec 2026, 19:00');
    expect(formatEventDates({ ...jazz, endsOn: '2026-12-07' }, 'ru', 'Asia/Tashkent')).toBe('5–7 дек. 2026, 19:00');
    expect(formatEventDates({ ...jazz, startsAt: null }, 'en', 'Asia/Tashkent')).toBe('5 Dec 2026');
    expect(formatStartTime({ ...jazz, startsAt: '25:00' }, 'en', 'Asia/Tashkent')).toBeNull();
    expect(formatEventDates({ ...jazz, datePrecision: 'month', startsOn: '2026-12-01' }, 'en', 'Europe/Berlin')).toBe(
      'Dec 2026',
    );
  });

  it('uses the operator language for month names and wording', () => {
    expect(formatEventDates(showcase(), 'ru')).toBe('12–14 окт. 2026');
    expect(formatEventDates(showcase({ datePrecision: 'estimated', startsOn: '2027-04-01', endsOn: null }), 'ru')).toBe(
      '≈ апр. 2027 (ожидается)',
    );
    expect(formatEventDates(showcase({ datePrecision: 'unknown', startsOn: null, endsOn: null }), 'uz')).toBe(
      'Sanalar e’lon qilinmagan',
    );
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
    expect(groups.map((group) => group.key)).toEqual(['2026-10', '2026-11', '2027-04', 'undated']);
    expect(groups.map((group) => group.events.map((event) => event.id))).toEqual([
      ['oct-a', 'oct-b'],
      ['nov'],
      ['apr'],
      ['undated'],
    ]);
    expect(groupEventsByMonth([])).toEqual([]);
  });
});

describe('feed query parameters', () => {
  it('sends only the filters that are set, with from=today while past events are hidden', () => {
    const page = { limit: 200, offset: 0 };
    expect(
      eventListQuery({ query: '  ', scope: '', format: '', status: '', from: '2026-10-08' }, page).toString(),
    ).toBe('from=2026-10-08&limit=200&offset=0');
    expect(
      eventListQuery(
        { query: ' silk ', scope: 'international', format: 'trade-fair', status: 'candidate', from: null },
        page,
      ).toString(),
    ).toBe('query=silk&scope=international&format=trade-fair&status=candidate&limit=200&offset=0');
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
    expect([0, 1, 9, -3].map((days) => daysLeftLabel(days, 'en'))).toEqual([
      'Deadline today',
      '1 day left',
      '9 days left',
      'Deadline passed',
    ]);
  });
});

describe('event form validation', () => {
  it('mirrors the contract: end after start, required start, null dates when unknown', () => {
    const draft = {
      ...eventDraft(),
      title: 'Expo',
      datePrecision: 'day' as const,
      startDay: '2026-10-14',
      endDay: '2026-10-12',
    };
    expect(validateEventDraft(draft).problems).toContain('The end date cannot be before the start date.');
    expect(validateEventDraft({ ...draft, startDay: '', endDay: '' }).problems).toContain(
      'Enter the start date, or choose another date precision.',
    );
    expect(validateEventDraft({ ...draft, startDay: '1999-12-31', endDay: '' }).problems).toContain(
      'Dates must be between 2000-01-01 and 2100-12-31.',
    );
    const unknown = validateEventDraft({ ...draft, datePrecision: 'unknown' });
    expect(unknown.input).toMatchObject({
      datePrecision: 'unknown',
      startsOn: null,
      endsOn: null,
      status: 'shortlisted',
      priority: 'normal',
    });
  });

  it('stores the 1st for month pickers and normalizes blanks and country codes', () => {
    const draft = {
      ...eventDraft(),
      title: ' Expo ',
      datePrecision: 'estimated' as const,
      startMonth: '2027-04',
      endMonth: '',
      country: 'kz',
      cost: '  ',
      organizer: ' ',
    };
    expect(validateEventDraft(draft).input).toMatchObject({
      title: 'Expo',
      startsOn: '2027-04-01',
      endsOn: null,
      country: 'KZ',
      cost: null,
      organizer: null,
    });
    expect(validateEventDraft({ ...draft, endMonth: '2027-05' }).input).toMatchObject({
      startsOn: '2027-04-01',
      endsOn: '2027-05-31',
    });
    expect(validateEventDraft({ ...draft, startMonth: '2027-05', endMonth: '2027-04' }).problems).toContain(
      'The end date cannot be before the start date.',
    );
    expect(validateEventDraft({ ...draft, startMonth: '' }).problems).toContain(
      'Choose the month the event starts in.',
    );
    expect(validateEventDraft({ ...draft, website: 'javascript:alert(1)' }).problems).toContain(
      'Enter the website as a full http:// or https:// address.',
    );
  });

  it('mirrors the server limits for names and places, and the contract for the other text', () => {
    const draft = { ...eventDraft(), title: 'Expo', startDay: '2026-10-14' };
    expect(validateEventDraft({ ...draft, organizer: 'o'.repeat(200), city: 'c'.repeat(200) }).problems).toEqual([]);
    expect(validateEventDraft({ ...draft, organizer: 'o'.repeat(201) }).problems).toContain(
      'Organizer is limited to 200 characters.',
    );
    expect(validateEventDraft({ ...draft, city: 'c'.repeat(201) }).problems).toContain(
      'City is limited to 200 characters.',
    );
    expect(eventLimits).toMatchObject({ organizer: 200, city: 200, venue: 200, title: 200 });
  });

  it('round-trips the tracking of an existing event without changing any field', () => {
    for (const event of [
      showcase({ cost: '300 USD', participation: 'Application package', notes: 'Fit', priority: 'high' }),
      showcase({ status: 'applying', cost: null }),
      showcase({ datePrecision: 'unknown', startsOn: null, endsOn: null, applicationDeadline: '2026-10-20' }),
    ]) {
      expect(validateTrackingDraft(trackingDraft(event)).input).toEqual(trackingInput(event));
    }
  });

  it('checks the tracking text limits and trims what it sends', () => {
    const draft = trackingDraft(showcase());
    expect(validateTrackingDraft({ ...draft, participation: 'p'.repeat(2001) }).problems).toEqual([
      'How to take part is limited to 2000 characters.',
    ]);
    expect(validateTrackingDraft({ ...draft, cost: 'c'.repeat(501) }).problems).toEqual([
      'Cost is limited to 500 characters.',
    ]);
    expect(validateTrackingDraft({ ...draft, notes: 'n'.repeat(8001) }).problems).toEqual([
      'Notes are limited to 8000 characters.',
    ]);
    expect(validateTrackingDraft({ ...draft, cost: '  ', notes: ' Fit ', participation: ' Booth ' }).input).toEqual({
      status: 'shortlisted',
      priority: 'normal',
      participation: 'Booth',
      cost: null,
      notes: 'Fit',
    });
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

describe('title language', () => {
  it('tags only a title written in another known language than the interface', () => {
    expect(titleLanguageTag({ titleLanguage: 'ru-Cyrl' }, 'uz')).toBe('RU');
    expect(titleLanguageTag({ titleLanguage: 'uz-Latn' }, 'ru')).toBe('UZ');
    expect(titleLanguageTag({ titleLanguage: 'uz-Latn' }, 'uz')).toBeNull();
    expect(titleLanguageTag({ titleLanguage: 'und' }, 'uz')).toBeNull();
    expect(titleLanguageTag({ titleLanguage: null }, 'uz')).toBeNull();
    expect(titleLanguageTag({ titleLanguage: 'ky-Cyrl' }, 'en')).toBe('KY');
  });
});

describe('ticket prices', () => {
  const nbsp = '\u00a0';
  const tickets = (over: Partial<NonNullable<ShowcaseEvent['tickets']>>) => ({
    minPrice: null,
    maxPrice: null,
    currency: null,
    free: false,
    ...over,
  });

  it('groups thousands with a non-breaking space', () => {
    expect(formatAmount(150000)).toBe(`150${nbsp}000`);
    expect(formatAmount(999)).toBe('999');
    expect(formatAmount(1234567.5)).toBe(`1${nbsp}234${nbsp}567.5`);
  });

  it('words a price as exactly as the source gave it, and reads no price as null', () => {
    const price = (over: Partial<NonNullable<ShowcaseEvent['tickets']>>, locale: 'en' | 'ru' = 'en') =>
      ticketPriceLabel(tickets(over), locale);
    expect(price({ minPrice: 150000, currency: 'UZS' })).toBe(`from 150${nbsp}000${nbsp}UZS`);
    expect(price({ minPrice: 150000, maxPrice: 150000, currency: 'UZS' })).toBe(`150${nbsp}000${nbsp}UZS`);
    expect(price({ minPrice: 100000, maxPrice: 300000, currency: 'UZS' })).toBe(
      `100${nbsp}000${nbsp}–${nbsp}300${nbsp}000${nbsp}UZS`,
    );
    expect(price({ maxPrice: 300000, currency: 'UZS' })).toBe(`up to 300${nbsp}000${nbsp}UZS`);
    expect(price({ minPrice: 20 })).toBe('from 20');
    expect(price({ free: true })).toBe('Free');
    expect(price({ free: true }, 'ru')).toBe('Бесплатно');
    expect(price({})).toBeNull();
  });
});
