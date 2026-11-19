import { describe, expect, it, vi } from 'vitest';
import type { ApiClient, ApiDecoder } from '@wow-two-beta/fe/foundation/http';

import { createEventsApi } from '@/integration/events/EventsApi';

const ORG = '11111111-1111-4111-8111-111111111111';
const EVENT = '22222222-2222-4222-8222-222222222222';
const signal = new AbortController().signal;
const filter = {
  query: '',
  scope: '',
  format: '',
  status: '',
  from: null,
  includeClosed: false,
  limit: 200,
  offset: 0,
} as const;
const wire = {
  id: EVENT,
  title: 'Moda haftaligi',
  scope: 'local',
  format: 'runway',
  eventStatus: 'cancelled',
  attendance: 'online',
  datePrecision: 'unknown',
  participation: '',
  status: 'candidate',
  priority: 'normal',
  notes: '',
  revision: 1,
  eventRevision: 1,
  createdAt: '2026-10-01T00:00:00Z',
  updatedAt: '2026-10-01T00:00:00Z',
};

interface Call {
  path: string;
  init: { headers?: Record<string, string>; body?: unknown; decode: ApiDecoder<unknown> };
}
/** A client that records each call and answers with a decoded `value` only the decoder can judge. */
function recordingClient() {
  const calls: Call[] = [];
  const answer = vi.fn((path: string, init: Call['init']) => {
    calls.push({ path, init });
    return Promise.resolve({ ok: true, value: undefined });
  });
  const client = { get: answer, post: answer, put: answer, delete: answer } as unknown as ApiClient;
  return { client, calls };
}

describe('events transport', () => {
  it('names the operator language in Accept-Language on every call, read when the request starts', async () => {
    let language = 'uz';
    const { client, calls } = recordingClient();
    const api = createEventsApi(client, () => language);
    const input = { title: 'Expo' } as never;

    await api.listEvents(ORG, filter, signal);
    await api.summary(ORG, '2026-10-08', signal);
    await api.getEvent(ORG, EVENT, signal);
    await api.createEvent(ORG, input, signal);
    await api.updateTracking(ORG, EVENT, 1, input, signal);
    expect(calls.map((call) => call.init.headers)).toEqual(Array(5).fill({ 'Accept-Language': 'uz' }));

    language = 'ru';
    await api.getEvent(ORG, EVENT, signal);
    expect(calls.at(-1)!.init.headers).toEqual({ 'Accept-Language': 'ru' });
  });

  it('decodes an event without the members the wire leaves out when empty', async () => {
    const { client, calls } = recordingClient();
    await createEventsApi(client).getEvent(ORG, EVENT, signal);
    const decoded = calls[0]!.init.decode(wire);
    expect(decoded.ok).toBe(true);
    expect(decoded.ok && decoded.value).toMatchObject({ titleLanguage: null, languages: [] });
    expect(calls[0]!.init.decode({ ...wire, languages: [3] }).ok).toBe(false);
  });

  it('writes where Ocharo stands to the tracking route, carrying only the tracking fields and the revision', async () => {
    const { client, calls } = recordingClient();
    const input = {
      status: 'shortlisted',
      priority: 'high',
      participation: 'Booth',
      cost: null,
      notes: 'Fits.',
    } as const;

    await createEventsApi(client, () => 'ru').updateTracking(ORG, EVENT, 4, input, signal);

    expect(calls[0]!.path).toBe(`/api/organizations/${ORG}/events/${EVENT}/tracking`);
    expect(calls[0]!.init.body).toEqual({ expectedRevision: 4, ...input });
    expect(calls[0]!.init.headers).toEqual({ 'Accept-Language': 'ru' });
    expect(calls[0]!.init.decode({ ...wire, status: 'shortlisted' }).ok).toBe(true);
    expect(calls[0]!.init.decode({ ...wire, eventRevision: 0 }).ok).toBe(false);
  });

  it('decodes ticket prices with the members the wire leaves out, and refuses a malformed one', async () => {
    const { client, calls } = recordingClient();
    await createEventsApi(client).getEvent(ORG, EVENT, signal);
    const { decode } = calls[0]!.init;
    const value = (input: unknown) => {
      const decoded = decode(input);
      return decoded.ok ? decoded.value : decoded;
    };
    expect(value(wire)).toMatchObject({ tickets: null, ticketUrl: null });
    expect(
      value({
        ...wire,
        tickets: { minPrice: 150000, currency: 'UZS', free: false },
        ticketUrl: 'https://t.example.test/',
      }),
    ).toMatchObject({
      tickets: { minPrice: 150000, maxPrice: null, currency: 'UZS', free: false },
      ticketUrl: 'https://t.example.test/',
    });
    expect(decode({ ...wire, tickets: { minPrice: '150000', free: false } }).ok).toBe(false);
    expect(decode({ ...wire, tickets: { minPrice: 1 } }).ok).toBe(false);
  });

  it('decodes the start time, zone, address and organizer site, and reads a malformed time as none', async () => {
    const { client, calls } = recordingClient();
    await createEventsApi(client).getEvent(ORG, EVENT, signal);
    const { decode } = calls[0]!.init;
    const value = (input: unknown) => {
      const decoded = decode(input);
      return decoded.ok ? decoded.value : decoded;
    };
    expect(value(wire)).toMatchObject({ startsAt: null, timeZone: null, address: null, organizerWebsite: null });
    expect(
      value({
        ...wire,
        startsAt: '19:00',
        timeZone: 'Asia/Tashkent',
        address: 'Pakhtakor street 5',
        organizerWebsite: 'https://jazz.example.test/',
      }),
    ).toMatchObject({
      startsAt: '19:00',
      timeZone: 'Asia/Tashkent',
      address: 'Pakhtakor street 5',
      organizerWebsite: 'https://jazz.example.test/',
    });
    expect(value({ ...wire, startsAt: '7pm' })).toMatchObject({ startsAt: null });
    expect(decode({ ...wire, address: 5 }).ok).toBe(false);
  });

  it('decodes each source and its listing state, reading an unknown state as none and no sources as empty', async () => {
    const { client, calls } = recordingClient();
    await createEventsApi(client).getEvent(ORG, EVENT, signal);
    const { decode } = calls[0]!.init;
    const value = (input: unknown) => {
      const decoded = decode(input);
      return decoded.ok ? decoded.value : decoded;
    };
    expect(value(wire)).toMatchObject({ sources: [], sourceUpdatedAt: null });
    expect(
      value({
        ...wire,
        sources: [
          { name: 'Afisha Uzbekistan', url: 'https://afisha.example.test/jazz', state: 'gone' },
          { state: 'vanished' },
        ],
        sourceUpdatedAt: '2026-10-07T09:30:00Z',
      }),
    ).toMatchObject({
      sources: [
        { name: 'Afisha Uzbekistan', url: 'https://afisha.example.test/jazz', state: 'gone' },
        { name: null, url: null, state: null },
      ],
      sourceUpdatedAt: '2026-10-07T09:30:00Z',
    });
    expect(decode({ ...wire, sources: [{ name: 5 }] }).ok).toBe(false);
    expect(decode({ ...wire, sources: 'Afisha' }).ok).toBe(false);
  });

  it('keeps the event state and attendance, and reads an unknown or missing one as the plain case', async () => {
    const { client, calls } = recordingClient();
    await createEventsApi(client).getEvent(ORG, EVENT, signal);
    const { decode } = calls[0]!.init;
    const value = (input: unknown) => {
      const decoded = decode(input);
      return decoded.ok ? decoded.value : decoded;
    };
    expect(value(wire)).toMatchObject({ eventStatus: 'cancelled', attendance: 'online' });
    expect(value({ ...wire, eventStatus: 'vanished', attendance: 'hologram' })).toMatchObject({
      eventStatus: 'scheduled',
      attendance: 'offline',
    });
    const older: Record<string, unknown> = { ...wire };
    delete older.eventStatus;
    delete older.attendance;
    expect(value(older)).toMatchObject({ eventStatus: 'scheduled', attendance: 'offline' });
  });

  it('keeps a derived scope, and reads an event with no country, or a scope it does not know, as unknown', async () => {
    const { client, calls } = recordingClient();
    await createEventsApi(client).getEvent(ORG, EVENT, signal);
    const { decode } = calls[0]!.init;
    const scope = (value: unknown) => {
      const decoded = decode({ ...wire, scope: value });
      return decoded.ok ? (decoded.value as { scope: string }).scope : null;
    };
    expect(scope('regional')).toBe('regional');
    expect(scope('unknown')).toBe('unknown');
    expect(scope('galactic')).toBe('unknown');
  });
});
