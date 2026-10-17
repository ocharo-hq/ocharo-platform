import { beforeEach, describe, expect, it, vi } from 'vitest';

import { downloadText } from '@wow-two-beta/fe/foundation/files';

import { createEventCalendar, downloadEventCalendar } from '@/application/workspace/EventCalendar';
import type { UiLocale } from '@/domain/shared/UiMessages';
import type { ShowcaseEvent } from '@/domain/workspace/Events';

vi.mock('@wow-two-beta/fe/foundation/files', () => ({ downloadText: vi.fn(() => true) }));

const now = new Date('2026-10-09T12:34:56.789Z');
const eventId = '22222222-2222-4222-8222-222222222222';

function showcase(overrides: Partial<ShowcaseEvent> = {}): ShowcaseEvent {
  return {
    id: eventId, key: null, title: 'Tashkent Fashion Week', organizer: 'Fashion Council', scope: 'local',
    format: 'runway', country: 'UZ', city: 'Tashkent', venue: 'Expo Hall', datePrecision: 'day',
    startsOn: '2026-10-12', endsOn: '2026-10-14', applicationDeadline: '2026-10-10',
    participation: 'Submit a portfolio.', cost: 'Fee to confirm', website: 'https://example.test/events',
    sourceUrl: 'https://example.test/announcement', status: 'shortlisted', priority: 'normal',
    notes: 'Internal decision notes stay in Desk.', revision: 2,
    createdAt: '2026-10-01T00:00:00Z', updatedAt: '2026-10-08T12:00:00Z', ...overrides,
  };
}

function calendarFor(event = showcase(), locale: UiLocale = 'en'): string {
  const result = createEventCalendar(event, locale, now);
  expect(result).toBeDefined();
  return result?.content.replace(/\r\n /g, '') ?? '';
}

describe('event calendar export', () => {
  beforeEach(() => vi.clearAllMocks());

  it('exports occurrence and deadline as independent all-day entries with exclusive ends', () => {
    const calendar = createEventCalendar(showcase(), 'en', now);
    const content = calendarFor();
    expect(calendar).toMatchObject({ hasOccurrence: true, hasDeadline: true, filename: `ocharo-event-${eventId}.ics` });
    expect(content.match(/BEGIN:VEVENT/g)).toHaveLength(2);
    expect(content).toContain('DTSTART;VALUE=DATE:20261012\r\nDTEND;VALUE=DATE:20261015');
    expect(content).toContain('DTSTART;VALUE=DATE:20261010\r\nDTEND;VALUE=DATE:20261011');
    expect(content).toContain('SUMMARY:Application deadline: Tashkent Fashion Week');
    expect(content).toContain('DTSTAMP:20261009T123456Z');
    expect(content).toContain('LAST-MODIFIED:20261008T120000Z');
    expect(content).toContain('SEQUENCE:2');
    expect(content).toContain('LOCATION:Expo Hall\\, Tashkent\\, Uzbekistan');
    expect(content).toContain('URL:https://example.test/events');
    expect(content).toContain('https://example.test/announcement');
    expect(content).not.toContain('Internal decision notes');
    expect(content).not.toMatch(/(?:TZID|METHOD|ATTENDEE|ORGANIZER|VALARM)[;:]/);
    expect(content.match(/TRANSP:TRANSPARENT/g)).toHaveLength(2);
    expect(content).toMatch(/^BEGIN:VCALENDAR\r\nVERSION:2.0\r\n/);
    expect(content).toMatch(/END:VCALENDAR\r\n$/);
  });

  it.each([
    ['2026-10-12', null, '20261013'],
    ['2026-10-12', '2026-10-12', '20261013'],
    ['2028-02-28', null, '20280229'],
    ['2028-02-29', null, '20280301'],
    ['2026-12-31', null, '20270101'],
  ])('keeps the inclusive %s / %s range in calendar dates', (startsOn, endsOn, exclusive) => {
    const content = calendarFor(showcase({ startsOn, endsOn, applicationDeadline: null }));
    expect(content.match(/BEGIN:VEVENT/g)).toHaveLength(1);
    expect(content).toContain(`DTEND;VALUE=DATE:${exclusive}`);
  });

  it.each(['month', 'estimated', 'unknown'] as const)('omits %s occurrence dates but retains an exact deadline', precision => {
    const event = showcase({ datePrecision: precision });
    const calendar = createEventCalendar(event, 'en', now);
    expect(calendar).toMatchObject({ hasOccurrence: false, hasDeadline: true });
    expect(calendarFor(event).match(/BEGIN:VEVENT/g)).toHaveLength(1);
    expect(calendarFor(event)).not.toContain('-occurrence@');
    expect(calendarFor(event)).toContain('-application@');
    expect(createEventCalendar({ ...event, applicationDeadline: null }, 'en', now)).toBeUndefined();
  });

  it.each([
    { startsOn: null },
    { startsOn: '2026-02-30' },
    { endsOn: '2026-10-11' },
    { endsOn: 'invalid' },
  ])('omits invalid occurrence ranges independently from the deadline: %j', overrides => {
    const event = showcase(overrides);
    expect(createEventCalendar(event, 'en', now)).toMatchObject({ hasOccurrence: false, hasDeadline: true });
    expect(createEventCalendar({ ...event, applicationDeadline: '2026-02-30' }, 'en', now)).toBeUndefined();
  });

  it('keeps each UID stable across revisions, rescheduling and interface language', () => {
    const original = calendarFor().match(/^UID:.+$/gm);
    const changed = calendarFor(showcase({
      title: 'Renamed event', revision: 8, startsOn: '2027-01-01', endsOn: '2027-01-03',
      applicationDeadline: '2026-12-15',
    }), 'uz');
    expect(original).toHaveLength(2);
    expect(new Set(original).size).toBe(2);
    expect(changed.match(/^UID:.+$/gm)).toEqual(original);
    expect(changed).toContain('SEQUENCE:8');
    expect(changed).toContain('SUMMARY:Ariza topshirishning oxirgi kuni: Renamed event');
    expect(calendarFor(showcase(), 'ru')).toContain('SUMMARY:Крайний срок подачи заявки:');
  });

  it('escapes user text and rejects unsafe URLs without inserting calendar properties', () => {
    const content = calendarFor(showcase({
      title: 'Silk, wool; thread\\trim\r\nATTENDEE:intruder',
      venue: 'Hall\nEND:VEVENT\rBEGIN:VEVENT',
      website: 'https://example.test/\r\nATTENDEE:intruder', sourceUrl: 'javascript:alert(1)',
    }));
    expect(content).toContain('SUMMARY:Silk\\, wool\\; thread\\\\trim\\nATTENDEE:intruder');
    expect(content).toContain('LOCATION:Hall\\nEND:VEVENT\\nBEGIN:VEVENT');
    expect(content.match(/^BEGIN:VEVENT$/gm)).toHaveLength(2);
    expect(content).not.toMatch(/^ATTENDEE:|^URL:/m);
    expect(content).not.toContain('javascript:');
    expect(content.replace(/\r\n/g, '')).not.toMatch(/[\r\n]/);
  });

  it('folds Unicode at 75 octets without splitting Cyrillic or emoji code points', () => {
    const title = 'Ткани 🧵 '.repeat(18);
    const calendar = createEventCalendar(showcase({ title }), 'ru', now);
    const content = calendar?.content ?? '';
    const encoder = new TextEncoder();
    expect(content).toContain('\r\n ');
    for (const line of content.split('\r\n')) expect(encoder.encode(line).length).toBeLessThanOrEqual(75);
    expect(content.replace(/\r\n /g, '')).toContain(`SUMMARY:${title}\r\n`);
    expect(content).not.toContain('\ufffd');
    expect(new TextDecoder().decode(encoder.encode(content))).toBe(content);
  });

  it('dispatches the ICS through the SDK without changing the event or calendar accounts', () => {
    const event = showcase();
    const original = structuredClone(event);
    const calendar = createEventCalendar(event, 'en', now);
    if (!calendar) throw new Error('Expected a calendar file');
    expect(downloadEventCalendar(calendar)).toBe(true);
    expect(downloadText).toHaveBeenCalledExactlyOnceWith(
      calendar.content, calendar.filename, 'text/calendar;charset=utf-8',
    );
    expect(event).toEqual(original);
    vi.mocked(downloadText).mockReturnValueOnce(false);
    expect(downloadEventCalendar(calendar)).toBe(false);
  });
});
