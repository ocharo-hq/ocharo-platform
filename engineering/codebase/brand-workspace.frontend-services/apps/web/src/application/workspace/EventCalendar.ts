import { downloadText } from '@wow-two-beta/ui-vue/foundation/files';

import { eventCalendarUiMessages } from '@/domain/shared/EventCalendarUiMessages';
import type { UiLocale } from '@/domain/shared/UiMessages';
import { countryName, isIsoDate, safeEventUrl, type ShowcaseEvent } from '@/domain/workspace/Events';

/** A local calendar file containing only independently known exact dates. */
export interface EventCalendar {
  readonly filename: string;
  readonly content: string;
  readonly hasOccurrence: boolean;
  readonly hasDeadline: boolean;
}

/** Escapes an RFC 5545 TEXT value without allowing additional content lines. @internal */
function calendarText(value: string): string {
  return value.replace(/\\/g, '\\\\').replace(/\r\n|\r|\n/g, '\\n').replace(/[,;]/g, '\\$&');
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
  return date.toISOString().replace(/[-:]/g, '').replace(/\.\d{3}Z$/, 'Z');
}

/** Creates independent all-day entries for the occurrence and application deadline, when exact. */
export function createEventCalendar(
  event: ShowcaseEvent,
  locale: UiLocale,
  now: Date = new Date(),
): EventCalendar | undefined {
  const hasOccurrence = event.datePrecision === 'day' && isIsoDate(event.startsOn)
    && (event.endsOn === null || (isIsoDate(event.endsOn) && event.endsOn >= event.startsOn));
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
  ].filter(Boolean).join('\n\n');
  const location = [event.venue, event.city, countryName(event.country, locale)].filter(Boolean).join(', ');
  const updated = new Date(event.updatedAt);
  const metadata = [
    `DTSTAMP:${calendarTimestamp(now)}`,
    `SEQUENCE:${Math.max(0, Math.trunc(event.revision))}`,
    ...(Number.isNaN(updated.getTime()) ? [] : [`LAST-MODIFIED:${calendarTimestamp(updated)}`]),
    'TRANSP:TRANSPARENT',
  ];
  const lines = ['BEGIN:VCALENDAR', 'VERSION:2.0', 'PRODID:-//Ocharo//Desk Events//EN', 'CALSCALE:GREGORIAN'];

  if (hasOccurrence && event.startsOn) {
    lines.push(
      'BEGIN:VEVENT',
      `UID:${calendarText(`event-${event.id}-occurrence@ocharo.local`)}`,
      ...metadata,
      `DTSTART;VALUE=DATE:${event.startsOn.replace(/-/g, '')}`,
      `DTEND;VALUE=DATE:${exclusiveEnd(event.endsOn ?? event.startsOn)}`,
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
