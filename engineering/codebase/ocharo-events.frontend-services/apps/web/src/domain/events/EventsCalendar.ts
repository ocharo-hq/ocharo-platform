import { Temporal } from 'temporal-polyfill';
import type { TrackedEventModel } from '@wow-two-beta/fe-events/contracts';
import {
  eventTitle,
  readEventWhen,
  regionOfCountry,
  type EventWhenParts,
  type EventsLocale,
} from '@wow-two-beta/fe-events/kit';

import { ocharoClosedStages, ocharoRegions, type EventStatus } from '@/domain/events/Events';

/**
 * The calendar's model: which days of events one visible range needs, and how an event Ocharo tracks becomes a block on
 * the calendar. The calendar draws only what an event says honestly: a day it is known to start on, never a month or a
 * year standing in for one.
 */

/**
 * The views the calendar viewer offers, the same names as the fe sdk's `EventCalendarViewerView`: the pinned release has
 * month, week, day and agenda. `calendarWindow` also reads `'year'`, which the viewer's next release adds.
 */
export type CalendarView = 'month' | 'week' | 'day' | 'agenda';

/** The days one read covers, inclusive, as `YYYY-MM-DD`: an event matches when it has not ended before `from` and starts on or before `to`. */
export interface CalendarWindow {
  readonly from: string;
  readonly to: string;
}

/**
 * The days a visible range needs, from the focused day. The month grid always draws 42 days from the week of the 1st, so
 * day, week and month views read the month with a week before it and two after (a month that ends early in the grid's
 * last row shows up to two more weeks); the year view reads the whole year, and the agenda the 31 days it lists.
 */
export function calendarWindow(view: string, focus: Temporal.PlainDate): CalendarWindow {
  if (view === 'year')
    return {
      from: Temporal.PlainDate.from({ year: focus.year, month: 1, day: 1 }).toString(),
      to: Temporal.PlainDate.from({ year: focus.year, month: 12, day: 31 }).toString(),
    };
  if (view === 'agenda') return { from: focus.toString(), to: focus.add({ days: 31 }).toString() };
  const first = focus.with({ day: 1 });
  const last = focus.with({ day: focus.daysInMonth });
  return { from: first.subtract({ days: 7 }).toString(), to: last.add({ days: 14 }).toString() };
}

/** How the calendar sets an event apart by where Ocharo stands with it. */
export type CalendarTone = 'pursuing' | 'participating' | 'neutral';

/**
 * Ocharo's stage as a tone: events being pursued (shortlisted, applying, applied) are one colour, events Ocharo is
 * confirmed at or attended another, and an event it has not tracked yet (`candidate`) neutral. Skipped and rejected
 * events never reach the calendar, as they stay out of the feed.
 */
export const calendarTones: Readonly<Record<EventStatus, CalendarTone>> = {
  candidate: 'neutral',
  shortlisted: 'pursuing',
  applying: 'pursuing',
  applied: 'pursuing',
  confirmed: 'participating',
  attended: 'participating',
  skipped: 'neutral',
  rejected: 'neutral',
};

/**
 * The theme colour behind each tone, built only from theme variables (never a hex value). The viewer cannot read a
 * variable to pick a text colour, so each block keeps the page's own text, and every background stays one that text
 * reads on in both colour modes: a pursued event is the theme's primary mixed into the card surface, strong enough to
 * stand out, a confirmed or attended one the soft success surface, and an event not yet tracked a quiet tint of the info
 * colour, visible against the card where the muted surface all but vanished on the dark calendar.
 */
export const calendarToneColors: Readonly<Record<CalendarTone, string>> = {
  pursuing: 'color-mix(in srgb, var(--color-primary) 55%, var(--color-card))',
  participating: 'var(--color-success-soft)',
  neutral: 'color-mix(in srgb, var(--color-info) 35%, var(--color-card))',
};

/** The stage an event without tracking reads as. */
export const untrackedStage: EventStatus = 'candidate';

/** The tone of a stage; a stage the table does not know (a workflow stored with others) reads as neutral. */
export function calendarTone(stage: string | null | undefined): CalendarTone {
  return calendarTones[(stage ?? untrackedStage) as EventStatus] ?? 'neutral';
}

/** One block on the calendar, shaped as the fe sdk's `EventCalendarViewerEvent`. */
export interface EventsCalendarEntry {
  readonly id: string;
  /** The title in the reader's language; absent for an event with none, which the calendar names itself. */
  readonly title: string | undefined;
  readonly start: Temporal.ZonedDateTime;
  /** Exclusive for an all-day entry: midnight after its last day. */
  readonly end: Temporal.ZonedDateTime;
  readonly isAllDay: boolean;
  readonly color: string;
}

/** How to place the events: the reader's language and the time zone the calendar shows. */
export interface EventsCalendarOptions {
  readonly locale: EventsLocale;
  /** The browser's IANA time zone. */
  readonly zone: string;
}

const plainDate = (parts: EventWhenParts) =>
  Temporal.PlainDate.from({ year: parts.year, month: (parts.month ?? 0) + 1, day: parts.day ?? 1 });
const plainDateTime = (parts: EventWhenParts) =>
  Temporal.PlainDateTime.from({
    year: parts.year,
    month: (parts.month ?? 0) + 1,
    day: parts.day ?? 1,
    hour: parts.hour ?? 0,
    minute: parts.minute ?? 0,
  });

/** The event's own zone when the browser knows it, else the zone shown. */
function eventZone(named: string | null | undefined, shown: string): string {
  if (!named?.trim()) return shown;
  try {
    Temporal.Now.zonedDateTimeISO(named.trim());
    return named.trim();
  } catch {
    return shown;
  }
}

/**
 * Places one event, or none when it has no day to stand on. A start known to its day becomes an all-day block from that
 * day to the end's day, both included (a one-day event when the end is missing, unreadable or before the start); a
 * start with a time becomes a timed block from that time, read in the event's zone and shown in the browser's, to the
 * end when it has one after the start (a day-only end closes at that day's end) and otherwise a point. A start known
 * only to its month or year, an estimated date and an event with no start are left out: the calendar never picks a day.
 */
function place(
  event: TrackedEventModel['event'],
  zone: string,
): Pick<EventsCalendarEntry, 'start' | 'end' | 'isAllDay'> | null {
  const start = readEventWhen(event.facts.starts);
  if (!start || start.day === undefined) return null;
  const end = readEventWhen(event.facts.ends);
  const endsOnADay = end !== null && end.day !== undefined;
  if (start.precision === 'minute') {
    const inZone = eventZone(event.facts.timeZone, zone);
    const from = plainDateTime(start).toZonedDateTime(inZone);
    let to = from;
    if (end && endsOnADay) {
      const closes =
        end.precision === 'minute'
          ? plainDateTime(end).toZonedDateTime(inZone)
          : plainDate(end).add({ days: 1 }).toZonedDateTime(inZone);
      if (Temporal.ZonedDateTime.compare(closes, from) > 0) to = closes;
    }
    return { start: from.withTimeZone(zone), end: to.withTimeZone(zone), isAllDay: false };
  }
  const first = plainDate(start);
  let last = first;
  if (end && endsOnADay && Temporal.PlainDate.compare(plainDate(end), first) > 0) last = plainDate(end);
  return {
    start: first.toZonedDateTime(zone),
    end: last.add({ days: 1 }).toZonedDateTime(zone),
    isAllDay: true,
  };
}

/**
 * Places one tracked event as a block, or none: a closed stage (`skipped`, `rejected`) is left out as the feed leaves it,
 * and so is an event with no day to stand on. The block is coloured by where Ocharo stands with the event.
 */
export function ocharoCalendarEntry(
  item: TrackedEventModel,
  options: EventsCalendarOptions,
): EventsCalendarEntry | null {
  const stage = item.tracking?.stage ?? untrackedStage;
  if ((ocharoClosedStages as readonly string[]).includes(stage)) return null;
  const placed = place(item.event, options.zone);
  if (!placed) return null;
  return {
    id: item.event.id,
    title: eventTitle(item.event.facts, options.locale) || undefined,
    ...placed,
    color: calendarToneColors[calendarTone(stage)],
  };
}

/**
 * The scope an event stands in: the key of Ocharo's region its country belongs to (`national`, `central-asia`,
 * `international`), by the rule the feed's scope chips use (`regionOfCountry`). An event with no country, or one that is
 * not a two-letter code, stands in none: the feed reads it as "Scope unknown" and no chip selects it, so a chosen
 * region never keeps it (`CalendarFilters`).
 */
export function calendarScope(item: TrackedEventModel): string | null {
  return regionOfCountry(item.event.facts.place?.country, ocharoRegions())?.key ?? null;
}
