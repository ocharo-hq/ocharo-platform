import { describe, expect, it, vi } from 'vitest';
import type { ApiClient, ApiDecoder } from '@wow-two-beta/fe/foundation/http';

import { createEventsApi } from '@/integration/events/EventsApi';

const ORG = '11111111-1111-4111-8111-111111111111';
const EVENT = '22222222-2222-4222-8222-222222222222';
const signal = new AbortController().signal;
const filter = { query: '', scope: '', format: '', status: '', from: null, limit: 200, offset: 0 } as const;
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
    await api.updateEvent(ORG, EVENT, 1, input, signal);
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
});
