import { downloadText } from '@wow-two-beta/fe/foundation/files';

import { eventCalendarUiMessages } from '@/domain/shared/EventCalendarUiMessages';
import type { UiLocale } from '@/domain/shared/UiMessages';
import {
  countryName,
  isIsoDate,
  isLocalTime,
  safeEventUrl,
  zonedInstant,
  zoneOffsetMinutes,
  type ShowcaseEvent,
} from '@/domain/events/Events';

/** A local calendar file containing only independently known exact dates. */
export interface EventCalendar {
  readonly filename: string;
  readonly content: string;
  readonly hasOccurrence: boolean;
  readonly hasDeadline: boolean;
}

/** Escapes an RFC 5545 TEXT value without allowing additional content lines. @internal */
function calendarText(value: string): string {
  return value
    .replace(/\\/g, '\\\\')
    .replace(/\r\n|\r|\n/g, '\\n')
    .replace(/[,;]/g, '\\$&');
}

/** Folds lines at 75 UTF-8 octets, including each continuation's leading space. @internal */
function foldLine(value: string): string {
  const encoder = new TextEncoder();
  let line = '';
  let size = 0;
  let result = '';
  for (const character of value) {
    const bytes = encoder.encode(character).length;
    if (size + bytes > 75) {
      result += `${line}\r\n`;
      line = ' ';
      size = 1;
    }
    line += character;
    size += bytes;
  }
  return result + line;
}

/** Converts an inclusive date into the exclusive all-day calendar end. @internal */
function exclusiveEnd(day: string): string {
  const date = new Date(`${day}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + 1);
  return date.toISOString().slice(0, 10).replace(/-/g, '');
}

/** Formats an RFC 5545 UTC timestamp. @internal */
function calendarTimestamp(date: Date): string {
  return date
    .toISOString()
    .replace(/[-:]/g, '')
    .replace(/\.\d{3}Z$/, 'Z');
}

/** Formats a local RFC 5545 date-time, `20261205T190000`. @internal */
function localDateTime(day: string, time: string): string {
  return `${day.replace(/-/g, '')}T${time.replace(':', '')}00`;
}

/** Formats a UTC offset as RFC 5545 writes it, `+0500`. @internal */
function calendarOffset(minutes: number): string {
  const sign = minutes < 0 ? '-' : '+';
  const value = Math.abs(minutes);
  return `${sign}${String(Math.floor(value / 60)).padStart(2, '0')}${String(value % 60).padStart(2, '0')}`;
}

/**
 * The occurrence's start and end lines: all-day dates, or, when the source gave a start time, a timed start in the
 * event's zone (`DTSTART;TZID=Asia/Tashkent:20261205T190000`, with that zone's offset at the start as its `VTIMEZONE`),
 * or floating local time without a zone; a timed event of several days ends as its last day does. @internal
 */
function occurrenceTimes(event: ShowcaseEvent, startsOn: string): { lines: string[]; zone: string[] } {
  const endsOn = event.endsOn ?? startsOn;
  if (!isLocalTime(event.startsAt))
    return {
      lines: [`DTSTART;VALUE=DATE:${startsOn.replace(/-/g, '')}`, `DTEND;VALUE=DATE:${exclusiveEnd(endsOn)}`],
      zone: [],
    };
  const instant = event.timeZone ? zonedInstant(startsOn, event.startsAt, event.timeZone) : null;
  const offset = event.timeZone && instant !== null ? zoneOffsetMinutes(event.timeZone, instant) : null;
  const zone = offset === null ? null : event.timeZone;
  const property = (name: string) => (zone ? `${name};TZID=${zone}` : name);
  const lines = [`${property('DTSTART')}:${localDateTime(startsOn, event.startsAt)}`];
  if (endsOn > startsOn) lines.push(`${property('DTEND')}:${exclusiveEnd(endsOn)}T000000`);
  if (!zone || offset === null) return { lines, zone: [] };
  return {
    lines,
    zone: [
      'BEGIN:VTIMEZONE',
      `TZID:${zone}`,
      'BEGIN:STANDARD',
      'DTSTART:19700101T000000',
      `TZOFFSETFROM:${calendarOffset(offset)}`,
      `TZOFFSETTO:${calendarOffset(offset)}`,
      'END:STANDARD',
      'END:VTIMEZONE',
    ],
  };
}

/**
 * Creates independent entries for the occurrence and application deadline, when exact: all-day, except an occurrence the
 * source gave a start time for, which starts at that time in its zone.
 */
export function createEventCalendar(
  event: ShowcaseEvent,
  locale: UiLocale,
  now: Date = new Date(),
): EventCalendar | undefined {
  const hasOccurrence =
    event.datePrecision === 'day' &&
    isIsoDate(event.startsOn) &&
    (event.endsOn === null || (isIsoDate(event.endsOn) && event.endsOn >= event.startsOn));
  const hasDeadline = isIsoDate(event.applicationDeadline);
  if (!hasOccurrence && !hasDeadline) return undefined;

  const messages = eventCalendarUiMessages[locale];
  const website = safeEventUrl(event.website);
  const source = safeEventUrl(event.sourceUrl);
  const description = [
    event.organizer && `${messages.organizer}: ${event.organizer}`,
    event.participation && `${messages.participation}: ${event.participation}`,
    event.cost && `${messages.cost}: ${event.cost}`,
    website && `${messages.website}: ${website}`,
    source && `${messages.source}: ${source}`,
  ]
    .filter(Boolean)
    .join('\n\n');
  const location = [event.venue, event.address, event.city, countryName(event.country, locale)]
    .filter(Boolean)
    .join(', ');
  const updated = new Date(event.updatedAt);
  const metadata = [
    `DTSTAMP:${calendarTimestamp(now)}`,
    `SEQUENCE:${Math.max(0, Math.trunc(event.revision))}`,
    ...(Number.isNaN(updated.getTime()) ? [] : [`LAST-MODIFIED:${calendarTimestamp(updated)}`]),
    'TRANSP:TRANSPARENT',
  ];
  const lines = ['BEGIN:VCALENDAR', 'VERSION:2.0', 'PRODID:-//Ocharo//Ocharo Events//EN', 'CALSCALE:GREGORIAN'];

  if (hasOccurrence && event.startsOn) {
    const times = occurrenceTimes(event, event.startsOn);
    lines.push(
      ...times.zone,
      'BEGIN:VEVENT',
      `UID:${calendarText(`event-${event.id}-occurrence@ocharo.local`)}`,
      ...metadata,
      ...times.lines,
      `SUMMARY:${calendarText(event.title)}`,
      ...(location ? [`LOCATION:${calendarText(location)}`] : []),
      ...(description ? [`DESCRIPTION:${calendarText(description)}`] : []),
      ...(website ? [`URL:${website}`] : []),
      'END:VEVENT',
    );
  }
  if (hasDeadline && event.applicationDeadline) {
    lines.push(
      'BEGIN:VEVENT',
      `UID:${calendarText(`event-${event.id}-application@ocharo.local`)}`,
      ...metadata,
      `DTSTART;VALUE=DATE:${event.applicationDeadline.replace(/-/g, '')}`,
      `DTEND;VALUE=DATE:${exclusiveEnd(event.applicationDeadline)}`,
      `SUMMARY:${calendarText(`${messages.deadline}: ${event.title}`)}`,
      `DESCRIPTION:${calendarText([messages.deadlineNote, description].filter(Boolean).join('\n\n'))}`,
      ...(website ? [`URL:${website}`] : []),
      'END:VEVENT',
    );
  }
  lines.push('END:VCALENDAR');
  return {
    filename: `ocharo-event-${event.id}.ics`,
    content: `${lines.map(foldLine).join('\r\n')}\r\n`,
    hasOccurrence,
    hasDeadline,
  };
}

/** Downloads a local file without connecting to or changing a calendar account. */
export function downloadEventCalendar(calendar: EventCalendar): boolean {
  return downloadText(calendar.content, calendar.filename, 'text/calendar;charset=utf-8');
}
