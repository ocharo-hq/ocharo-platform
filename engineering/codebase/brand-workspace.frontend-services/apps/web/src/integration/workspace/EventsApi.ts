import { createApiClient, ApiFailureFactory, type ApiDecoder } from '@wow-two-beta/fe/foundation/http';
import type { SessionTransport } from './WorkspaceApi';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';

import {
  isDatePrecision, isEventFormat, isEventPriority, isEventScope, isEventStatus, isIsoDate,
  type EventFilter, type EventInput, type EventSummary, type ShowcaseEvent,
} from '@/domain/workspace/Events';

/** @internal Checks an untrusted JSON object. */
const record = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null && !Array.isArray(v);
const strings = (v: Record<string, unknown>, keys: string[]) => keys.every(key => typeof v[key] === 'string');
const nullableStrings = (v: Record<string, unknown>, keys: string[]) => keys.every(key => v[key] === null || typeof v[key] === 'string');
const nullableDates = (v: Record<string, unknown>, keys: string[]) => keys.every(key => v[key] === null || isIsoDate(v[key]));
const count = (v: unknown) => Number.isSafeInteger(v) && Number(v) >= 0;
const revision = (v: unknown) => Number.isSafeInteger(v) && Number(v) > 0;
function decode<T>(guard: (v: unknown) => v is T): ApiDecoder<T> {
  return value => guard(value) ? ResultExtensions.ok(value) : ResultExtensions.fail(ApiFailureFactory.create('protocol'));
}
const list = <T>(guard: (v: unknown) => v is T) => (v: unknown): v is T[] => Array.isArray(v) && v.every(guard);

export const isShowcaseEvent = (v: unknown): v is ShowcaseEvent => record(v) &&
  strings(v, ['id', 'title', 'participation', 'notes', 'createdAt', 'updatedAt']) &&
  nullableStrings(v, ['key', 'organizer', 'country', 'city', 'venue', 'cost', 'website', 'sourceUrl']) &&
  nullableDates(v, ['startsOn', 'endsOn', 'applicationDeadline']) &&
  isEventScope(v.scope) && isEventFormat(v.format) && isDatePrecision(v.datePrecision) &&
  isEventStatus(v.status) && isEventPriority(v.priority) && revision(v.revision);
/** Every status key must be known; statuses with no events may be absent. */
export const isEventSummary = (v: unknown): v is EventSummary => record(v) && record(v.byStatus) &&
  Object.entries(v.byStatus).every(([status, value]) => isEventStatus(status) && count(value)) &&
  list(isShowcaseEvent)(v.upcomingDeadlines);
const isDeleted = (v: unknown): v is { id: string } => record(v) && typeof v.id === 'string';

/** Builds the feed query; blank filters are omitted so the server defaults apply. */
export function eventListQuery(filter: EventFilter, page: { limit: number; offset: number }): URLSearchParams {
  const query = new URLSearchParams();
  if (filter.query.trim()) query.set('query', filter.query.trim());
  if (filter.scope) query.set('scope', filter.scope);
  if (filter.format) query.set('format', filter.format);
  if (filter.status) query.set('status', filter.status);
  if (filter.from) query.set('from', filter.from);
  query.set('limit', String(page.limit));
  query.set('offset', String(page.offset));
  return query;
}

/** Creates the Events transport. It records where Ocharo stands; nothing here applies, registers or pays. */
export function createEventsApi(csrf: () => string, session: SessionTransport = {}) {
  const client = createApiClient({ credentials: 'same-origin', ...session, retry: false });
  const base = (o: string) => `/api/organizations/${encodeURIComponent(o)}/events`;
  const id = encodeURIComponent;
  const write = (signal: AbortSignal) => ({ signal, headers: { 'X-CSRF-TOKEN': csrf() } });
  return {
    listEvents: (o: string, filter: EventFilter & { limit: number; offset: number }, signal: AbortSignal) =>
      client.get(`${base(o)}?${eventListQuery(filter, filter)}`, { signal, decode: decode(list(isShowcaseEvent)) }),
    summary: (o: string, today: string, signal: AbortSignal) =>
      client.get(`${base(o)}/summary?${new URLSearchParams({ today })}`, { signal, decode: decode(isEventSummary) }),
    createEvent: (o: string, input: EventInput, signal: AbortSignal) =>
      client.post(base(o), { ...write(signal), body: input, decode: decode(isShowcaseEvent) }),
    getEvent: (o: string, eventId: string, signal: AbortSignal) =>
      client.get(`${base(o)}/${id(eventId)}`, { signal, decode: decode(isShowcaseEvent) }),
    updateEvent: (o: string, eventId: string, expectedRevision: number, input: EventInput, signal: AbortSignal) =>
      client.put(`${base(o)}/${id(eventId)}`, { ...write(signal), body: { expectedRevision, ...input }, decode: decode(isShowcaseEvent) }),
    deleteEvent: (o: string, eventId: string, expectedRevision: number, signal: AbortSignal) =>
      client.delete(`${base(o)}/${id(eventId)}?expectedRevision=${expectedRevision}`, { ...write(signal), decode: decode(isDeleted) }),
  };
}
export type EventsApi = ReturnType<typeof createEventsApi>;
