import { translateUi, type UiLocale } from '@/domain/shared/UiMessages';

/** Events slice 1: every event where Ocharo could show its products, and where Ocharo stands with each. */
export const eventScopes = ['local', 'regional', 'international'] as const;
export const eventFormats = ['runway', 'trade-fair', 'market', 'exhibition', 'competition', 'festival', 'conference', 'other'] as const;
export const eventStatuses = ['candidate', 'shortlisted', 'applying', 'applied', 'confirmed', 'attended', 'skipped', 'rejected'] as const;
export const datePrecisions = ['day', 'month', 'estimated', 'unknown'] as const;
export const eventPriorities = ['high', 'normal', 'low'] as const;
export type EventScope = typeof eventScopes[number];
export type EventFormat = typeof eventFormats[number];
export type EventStatus = typeof eventStatuses[number];
export type DatePrecision = typeof datePrecisions[number];
export type EventPriority = typeof eventPriorities[number];

export interface ShowcaseEvent {
  id: string; key: string | null; title: string; organizer: string | null;
  scope: EventScope; format: EventFormat; country: string | null; city: string | null; venue: string | null;
  startsOn: string | null; endsOn: string | null; datePrecision: DatePrecision; applicationDeadline: string | null;
  participation: string; cost: string | null; website: string | null; sourceUrl: string | null;
  status: EventStatus; priority: EventPriority; notes: string;
  revision: number; createdAt: string; updatedAt: string;
}
/** Every mutable field; the import `key` is never edited in Desk. */
export type EventInput = Omit<ShowcaseEvent, 'id' | 'key' | 'revision' | 'createdAt' | 'updatedAt'>;
/** Counts may omit statuses with no events; a missing count reads as zero. */
export interface EventSummary { byStatus: Partial<Record<EventStatus, number>>; upcomingDeadlines: ShowcaseEvent[] }
/** `from` hides events that ended before it; null shows past events too. */
export interface EventFilter { query: string; scope: EventScope | ''; format: EventFormat | ''; status: EventStatus | ''; from: string | null }

/** Contract limits mirrored for early feedback; the server remains authoritative. */
export const eventLimits = {
  title: 200, organizer: 160, city: 120, venue: 200, participation: 2000, cost: 500, url: 2048, notes: 8000,
  page: 200, minDate: '2000-01-01', maxDate: '2100-12-31', urgentDays: 14,
} as const;

export const scopeLabels: Record<EventScope, string> = { local: 'Local', regional: 'Regional', international: 'International' };
export const formatLabels: Record<EventFormat, string> = {
  runway: 'Runway show', 'trade-fair': 'Trade fair', market: 'Market', exhibition: 'Exhibition',
  competition: 'Competition', festival: 'Festival', conference: 'Conference', other: 'Other',
};
export const statusLabels: Record<EventStatus, string> = {
  candidate: 'Candidate', shortlisted: 'Shortlisted', applying: 'Applying', applied: 'Applied',
  confirmed: 'Confirmed', attended: 'Attended', skipped: 'Skipped', rejected: 'Rejected',
};
export const precisionLabels: Record<DatePrecision, string> = {
  day: 'Exact dates', month: 'Month only', estimated: 'Estimated month', unknown: 'Not announced',
};
export const priorityLabels: Record<EventPriority, string> = { high: 'High', normal: 'Normal', low: 'Low' };
export type EventTone = 'neutral' | 'brand' | 'success' | 'warning' | 'danger' | 'info' | 'outline';
export const statusTones: Record<EventStatus, EventTone> = {
  candidate: 'outline', shortlisted: 'brand', applying: 'warning', applied: 'info',
  confirmed: 'success', attended: 'success', skipped: 'neutral', rejected: 'danger',
};

export const isEventScope = (value: unknown): value is EventScope => eventScopes.includes(value as EventScope);
export const isEventFormat = (value: unknown): value is EventFormat => eventFormats.includes(value as EventFormat);
export const isEventStatus = (value: unknown): value is EventStatus => eventStatuses.includes(value as EventStatus);
export const isDatePrecision = (value: unknown): value is DatePrecision => datePrecisions.includes(value as DatePrecision);
export const isEventPriority = (value: unknown): value is EventPriority => eventPriorities.includes(value as EventPriority);

const isoDatePattern = /^(\d{4})-(\d{2})-(\d{2})$/;
/** A real calendar date written as `YYYY-MM-DD`. */
export function isIsoDate(value: unknown): value is string {
  if (typeof value !== 'string') return false;
  const match = isoDatePattern.exec(value);
  if (!match) return false;
  const date = new Date(Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3])));
  return date.getUTCFullYear() === Number(match[1]) && date.getUTCMonth() === Number(match[2]) - 1 && date.getUTCDate() === Number(match[3]);
}
const pad = (value: number) => String(value).padStart(2, '0');
/** The browser's own calendar date, not the UTC one, so "today" matches the operator's wall clock. */
export function localIsoDate(now: Date = new Date()): string {
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}
const utcDay = (date: string) => Date.UTC(Number(date.slice(0, 4)), Number(date.slice(5, 7)) - 1, Number(date.slice(8, 10)));
/** Whole calendar days from `today` to `date`; negative once the date has passed. */
export function daysUntil(date: string, today: string): number {
  return Math.round((utcDay(date) - utcDay(today)) / 86_400_000);
}
export type DeadlineTone = 'urgent' | 'upcoming' | 'passed';
/** Deadlines within two weeks are urgent; passed ones are kept for the record. */
export function deadlineTone(days: number): DeadlineTone {
  if (days < 0) return 'passed';
  return days <= eventLimits.urgentDays ? 'urgent' : 'upcoming';
}
/** "Today", "1 day left", "{count} days left" or "Deadline passed", in the operator language. */
export function daysLeftLabel(days: number, locale: UiLocale): string {
  if (days < 0) return translateUi('Deadline passed', locale);
  if (days === 0) return translateUi('Deadline today', locale);
  return days === 1 ? translateUi('1 day left', locale) : translateUi('{count} days left', locale, { count: days });
}

interface MonthNames { short: readonly string[]; standalone: readonly string[]; long: readonly string[] }
const english = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
const englishShort = english.map(name => name.slice(0, 3));
const uzbekShort = ['yan', 'fev', 'mar', 'apr', 'may', 'iyn', 'iyl', 'avg', 'sen', 'okt', 'noy', 'dek'];
/** Fixed tables keep dates identical in every browser, whatever its ICU data. */
const monthNames: Record<UiLocale, MonthNames> = {
  en: { short: englishShort, standalone: englishShort, long: english },
  uz: {
    short: uzbekShort, standalone: uzbekShort,
    long: ['Yanvar', 'Fevral', 'Mart', 'Aprel', 'May', 'Iyun', 'Iyul', 'Avgust', 'Sentabr', 'Oktabr', 'Noyabr', 'Dekabr'],
  },
  ru: {
    short: ['янв.', 'февр.', 'мар.', 'апр.', 'мая', 'июн.', 'июл.', 'авг.', 'сент.', 'окт.', 'нояб.', 'дек.'],
    standalone: ['янв.', 'февр.', 'март', 'апр.', 'май', 'июнь', 'июль', 'авг.', 'сент.', 'окт.', 'нояб.', 'дек.'],
    long: ['Январь', 'Февраль', 'Март', 'Апрель', 'Май', 'Июнь', 'Июль', 'Август', 'Сентябрь', 'Октябрь', 'Ноябрь', 'Декабрь'],
  },
};
interface DateParts { year: number; month: number; day: number }
const parts = (date: string): DateParts => ({ year: Number(date.slice(0, 4)), month: Number(date.slice(5, 7)) - 1, day: Number(date.slice(8, 10)) });

/** "12 Oct 2026". */
export function formatDay(date: string, locale: UiLocale): string {
  const value = parts(date);
  return `${value.day} ${monthNames[locale].short[value.month]} ${value.year}`;
}
/** "October 2026" for `YYYY-MM` or a full date. */
export function monthHeading(month: string, locale: UiLocale): string {
  const value = parts(`${month.slice(0, 7)}-01`);
  return `${monthNames[locale].long[value.month]} ${value.year}`;
}

/** Renders the dates exactly as precisely as they were announced. */
export function formatEventDates(event: Pick<ShowcaseEvent, 'startsOn' | 'endsOn' | 'datePrecision'>, locale: UiLocale): string {
  if (event.datePrecision === 'unknown' || !event.startsOn) return translateUi('Dates not announced', locale);
  const names = monthNames[locale];
  const start = parts(event.startsOn);
  const end = event.endsOn && event.endsOn > event.startsOn ? parts(event.endsOn) : null;
  const sameYear = end?.year === start.year;
  const sameMonth = sameYear && end?.month === start.month;
  if (event.datePrecision === 'day') {
    if (!end || (sameMonth && end.day === start.day)) return formatDay(event.startsOn, locale);
    if (sameMonth) return `${start.day}–${end.day} ${names.short[start.month]} ${start.year}`;
    if (sameYear) return `${start.day} ${names.short[start.month]} – ${end.day} ${names.short[end.month]} ${start.year}`;
    return `${formatDay(event.startsOn, locale)} – ${formatDay(event.endsOn!, locale)}`;
  }
  const month = (value: DateParts) => `${names.standalone[value.month]} ${value.year}`;
  const span = !end || sameMonth ? month(start)
    : sameYear ? `${names.standalone[start.month]} – ${names.standalone[end.month]} ${start.year}`
      : `${month(start)} – ${month(end)}`;
  return event.datePrecision === 'estimated' ? translateUi('≈ {date} (expected)', locale, { date: span }) : span;
}

export interface EventMonthGroup { key: string; events: ShowcaseEvent[] }
export const undatedGroupKey = 'undated';
/** Groups by the month events start in, oldest first; undated events close the list. */
export function groupEventsByMonth(events: readonly ShowcaseEvent[]): EventMonthGroup[] {
  const groups = new Map<string, ShowcaseEvent[]>();
  for (const event of events) {
    const key = event.datePrecision !== 'unknown' && event.startsOn ? event.startsOn.slice(0, 7) : undatedGroupKey;
    const group = groups.get(key);
    if (group) group.push(event); else groups.set(key, [event]);
  }
  return [...groups.entries()]
    .sort(([a], [b]) => a === undatedGroupKey ? 1 : b === undatedGroupKey ? -1 : a.localeCompare(b))
    .map(([key, items]) => ({ key, events: items }));
}

/** Returns a normalized http(s) URL, or null for every other scheme (javascript:, data:, file: …). */
export function safeEventUrl(value: string | null | undefined): string | null {
  const text = value?.trim();
  if (!text || /[\u0000- \u007f]/.test(text) || !/^https?:\/\//i.test(text)) return null;
  try {
    const url = new URL(text);
    return (url.protocol === 'https:' || url.protocol === 'http:') && url.hostname ? url.href : null;
  } catch { return null; }
}
/** Blank optional strings normalize to null, as the server does. */
export function optionalEventText(value: string): string | null {
  const text = value.trim();
  return text ? text : null;
}
/** The fields an edit or a quick status change sends back unchanged. */
export function eventInput(event: ShowcaseEvent): EventInput {
  return {
    title: event.title, organizer: event.organizer, scope: event.scope, format: event.format,
    country: event.country, city: event.city, venue: event.venue,
    startsOn: event.startsOn, endsOn: event.endsOn, datePrecision: event.datePrecision, applicationDeadline: event.applicationDeadline,
    participation: event.participation, cost: event.cost, website: event.website, sourceUrl: event.sourceUrl,
    status: event.status, priority: event.priority, notes: event.notes,
  };
}

/** The draft an event form edits: plain strings, with month pickers as `YYYY-MM`. */
export interface EventDraft {
  title: string; organizer: string; scope: EventScope; format: EventFormat; country: string; city: string; venue: string;
  datePrecision: DatePrecision; startDay: string; endDay: string; startMonth: string; endMonth: string;
  applicationDeadline: string; participation: string; cost: string; website: string; sourceUrl: string;
  status: EventStatus; priority: EventPriority; notes: string;
}
export function eventDraft(event?: ShowcaseEvent | null): EventDraft {
  const precision = event?.datePrecision ?? 'day';
  const monthly = precision === 'month' || precision === 'estimated';
  return {
    title: event?.title ?? '', organizer: event?.organizer ?? '', scope: event?.scope ?? 'local', format: event?.format ?? 'exhibition',
    country: event?.country ?? '', city: event?.city ?? '', venue: event?.venue ?? '', datePrecision: precision,
    startDay: !monthly ? event?.startsOn ?? '' : '', endDay: !monthly ? event?.endsOn ?? '' : '',
    startMonth: monthly ? event?.startsOn?.slice(0, 7) ?? '' : '', endMonth: monthly ? event?.endsOn?.slice(0, 7) ?? '' : '',
    applicationDeadline: event?.applicationDeadline ?? '', participation: event?.participation ?? '', cost: event?.cost ?? '',
    website: event?.website ?? '', sourceUrl: event?.sourceUrl ?? '',
    status: event?.status ?? 'shortlisted', priority: event?.priority ?? 'normal', notes: event?.notes ?? '',
  };
}
const monthPattern = /^\d{4}-\d{2}$/;
const lastDayOfMonth = (month: string) => {
  const day = new Date(Date.UTC(Number(month.slice(0, 4)), Number(month.slice(5, 7)), 0)).getUTCDate();
  return `${month}-${pad(day)}`;
};
const inRange = (date: string) => date >= eventLimits.minDate && date <= eventLimits.maxDate;

/**
 * Mirrors the contract's validation; returns English messages for `t()`, or the input to send.
 * Month pickers store the 1st as the start and the last day of the end month as the end; an end
 * the operator did not move keeps its stored day.
 */
export function validateEventDraft(draft: EventDraft, previous?: Pick<ShowcaseEvent, 'endsOn'> | null): { input: EventInput | null; problems: string[] } {
  const problems: string[] = [];
  const title = draft.title.trim();
  const country = draft.country.trim().toUpperCase();
  const website = draft.website.trim();
  const sourceUrl = draft.sourceUrl.trim();
  if (!title || title.length > eventLimits.title) problems.push('Enter a title of up to 200 characters.');
  if (draft.organizer.trim().length > eventLimits.organizer) problems.push('Organizer is limited to 160 characters.');
  if (country && !/^[A-Z]{2}$/.test(country)) problems.push('Use a two-letter country code, for example UZ or KZ.');
  if (draft.city.trim().length > eventLimits.city) problems.push('City is limited to 120 characters.');
  if (draft.venue.trim().length > eventLimits.venue) problems.push('Venue is limited to 200 characters.');

  let startsOn: string | null = null;
  let endsOn: string | null = null;
  if (draft.datePrecision === 'day') {
    startsOn = draft.startDay.trim() || null;
    endsOn = draft.endDay.trim() || null;
    if (!startsOn) problems.push('Enter the start date, or choose another date precision.');
    if ((startsOn && (!isIsoDate(startsOn) || !inRange(startsOn))) || (endsOn && (!isIsoDate(endsOn) || !inRange(endsOn))))
      problems.push('Dates must be between 2000-01-01 and 2100-12-31.');
  } else if (draft.datePrecision === 'month' || draft.datePrecision === 'estimated') {
    const startMonth = draft.startMonth.trim();
    const endMonth = draft.endMonth.trim();
    if (!startMonth) problems.push('Choose the month the event starts in.');
    else if (!monthPattern.test(startMonth) || !isIsoDate(`${startMonth}-01`)) problems.push('Dates must be between 2000-01-01 and 2100-12-31.');
    else startsOn = `${startMonth}-01`;
    if (endMonth && (!monthPattern.test(endMonth) || !isIsoDate(`${endMonth}-01`))) problems.push('Dates must be between 2000-01-01 and 2100-12-31.');
    else if (endMonth) endsOn = previous?.endsOn?.startsWith(endMonth) ? previous.endsOn : endMonth === startMonth ? null : lastDayOfMonth(endMonth);
    if ((startsOn && !inRange(startsOn)) || (endsOn && !inRange(endsOn))) problems.push('Dates must be between 2000-01-01 and 2100-12-31.');
  }
  if (startsOn && endsOn && endsOn < startsOn) problems.push('The end date cannot be before the start date.');

  const deadline = draft.applicationDeadline.trim() || null;
  if (deadline && (!isIsoDate(deadline) || !inRange(deadline))) problems.push('The application deadline must be between 2000-01-01 and 2100-12-31.');
  if (draft.participation.length > eventLimits.participation) problems.push('How to take part is limited to 2000 characters.');
  if (draft.cost.trim().length > eventLimits.cost) problems.push('Cost is limited to 500 characters.');
  if (website && (!safeEventUrl(website) || website.length > eventLimits.url)) problems.push('Enter the website as a full http:// or https:// address.');
  if (sourceUrl && (!safeEventUrl(sourceUrl) || sourceUrl.length > eventLimits.url)) problems.push('Enter the source as a full http:// or https:// address.');
  if (draft.notes.length > eventLimits.notes) problems.push('Notes are limited to 8000 characters.');
  const unique = [...new Set(problems)];
  if (unique.length) return { input: null, problems: unique };
  return {
    problems: [],
    input: {
      title, organizer: optionalEventText(draft.organizer), scope: draft.scope, format: draft.format,
      country: country || null, city: optionalEventText(draft.city), venue: optionalEventText(draft.venue),
      startsOn, endsOn, datePrecision: draft.datePrecision, applicationDeadline: deadline,
      participation: draft.participation.trim(), cost: optionalEventText(draft.cost),
      website: website || null, sourceUrl: sourceUrl || null,
      status: draft.status, priority: draft.priority, notes: draft.notes.trim(),
    },
  };
}

const identifier = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export interface EventsLocation { view: 'feed' | 'event'; eventId: string | null }
/** Reads the segments after `/organizations/{id}/events`; anything but one event id falls back to the feed. */
export function readEventsLocation(segments: readonly string[]): EventsLocation {
  if (segments.length === 1 && identifier.test(segments[0]!)) return { view: 'event', eventId: segments[0]!.toLowerCase() };
  return { view: 'feed', eventId: null };
}
export function eventsPath(organizationId: string, location: EventsLocation): string {
  if (!identifier.test(organizationId)) throw new Error('Invalid workspace destination.');
  const base = `/organizations/${organizationId}/events`;
  return location.view === 'event' && location.eventId && identifier.test(location.eventId) ? `${base}/${location.eventId}` : base;
}
export function isEventIdentifier(value: string): boolean {
  return identifier.test(value);
}

/** The country's name in the operator language, falling back to its code where the browser has no name. */
export function countryName(code: string | null, locale: UiLocale): string | null {
  if (!code) return null;
  try { return new Intl.DisplayNames([locale], { type: 'region' }).of(code) ?? code; } catch { return code; }
}
/** "Tashkent · Uzbekistan": city then country, whichever are known. */
export function eventPlace(event: Pick<ShowcaseEvent, 'city' | 'country'>, locale: UiLocale): string {
  return [event.city, countryName(event.country, locale)].filter(Boolean).join(' · ');
}
