import { afterEach, describe, expect, it, vi } from 'vitest';

import { systemApi } from '@/integration/system/SystemApi';

/** @internal The smallest valid foundation response. */
const waiting = {
  service: 'brand-workspace', stage: 'foundation', persistence: 'PostgreSQL',
  worker: { state: 'waiting', lastSeenAtUtc: null },
};

afterEach(() => vi.unstubAllGlobals());

describe('system status transport', () => {
  it('reads only the public status route with a validated response', async () => {
    const fetch = vi.fn().mockResolvedValue(Response.json(waiting));
    vi.stubGlobal('fetch', fetch);

    expect(await systemApi.getStatus(new AbortController().signal)).toEqual({ ok: true, value: waiting });
    expect(fetch).toHaveBeenCalledWith('/api/system/status', expect.objectContaining({ method: 'GET', credentials: 'omit' }));
  });

  it.each([
    ['HTML fallback', () => new Response('<html>SPA</html>', { headers: { 'Content-Type': 'text/html' } })],
    ['malformed JSON', () => new Response('{', { headers: { 'Content-Type': 'application/json' } })],
    ['unknown state', () => Response.json({ ...waiting, worker: { state: 'publishing', lastSeenAtUtc: null } })],
    ['missing heartbeat', () => Response.json({ ...waiting, worker: { state: 'healthy', lastSeenAtUtc: null } })],
    ['invalid timestamp', () => Response.json({ ...waiting, worker: { state: 'stale', lastSeenAtUtc: 'yesterday' } })],
  ])('rejects %s without pretending the service is ready', async (_label, response) => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response()));
    const result = await systemApi.getStatus(new AbortController().signal);
    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.failure.code).toBe('protocol');
  });

  it('keeps network and HTTP failures distinguishable', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('offline')));
    const offline = await systemApi.getStatus(new AbortController().signal);
    expect(offline.ok).toBe(false);
    if (!offline.ok) expect(offline.failure.code).toBe('transport');

    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json({ detail: 'private diagnostics' }, { status: 503 })));
    const unavailable = await systemApi.getStatus(new AbortController().signal);
    expect(unavailable.ok).toBe(false);
    if (!unavailable.ok) expect(unavailable.failure.code).toBe('http');
  });

  it('cancels before fetching when the owning query is disposed', async () => {
    const fetch = vi.fn();
    vi.stubGlobal('fetch', fetch);
    const controller = new AbortController();
    controller.abort();
    const result = await systemApi.getStatus(controller.signal);
    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.failure.code).toBe('cancelled');
    expect(fetch).not.toHaveBeenCalled();
  });
});
