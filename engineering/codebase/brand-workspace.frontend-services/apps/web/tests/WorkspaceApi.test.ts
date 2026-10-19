import { afterEach, describe, expect, it, vi } from 'vitest';

import { createWorkspaceApi } from '@/integration/workspace/WorkspaceApi';

const session = {
  authenticated: false,
  user: null,
  sessionId: null,
  csrfToken: 'anonymous-token',
  setupRequired: false,
};
afterEach(() => vi.unstubAllGlobals());

describe('published SDK workspace transport', () => {
  it('unwraps real data envelopes before validating session payloads', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json({ data: session })));
    const api = createWorkspaceApi(() => session.csrfToken);
    expect(await api.getSession()).toEqual({ ok: true, value: session });
  });

  it('rejects incomplete authenticated sessions', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json({ data: { ...session, authenticated: true } })));
    const result = await createWorkspaceApi(() => '').getSession();
    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.failure.code).toBe('protocol');
  });

  it('sends session-bound CSRF for login and logout and supports empty logout responses', async () => {
    const fetch = vi
      .fn()
      .mockResolvedValueOnce(Response.json({ data: { authenticated: true } }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetch);
    const api = createWorkspaceApi(() => 'bound-token');
    expect(
      (
        await api.login(
          { email: 'operator@example.test', password: 'not-a-real-password' },
          new AbortController().signal,
        )
      ).ok,
    ).toBe(true);
    expect((await api.logout()).ok).toBe(true);
    for (const [, init] of fetch.mock.calls) {
      expect(init.credentials).toBe('same-origin');
      expect(init.headers.get('X-CSRF-TOKEN')).toBe('bound-token');
    }
  });

  it('preserves multipart bytes and leaves the boundary to the browser', async () => {
    const file = new File(['fixture'], 'image.png', { type: 'image/png' });
    const fetch = vi.fn().mockResolvedValue(
      Response.json({
        data: {
          id: 'image',
          organizationId: 'org',
          fileName: 'image.png',
          contentType: 'image/png',
          byteLength: 7,
          sha256: 'hash',
          provenance: 'Photograph supplied for a test',
          createdAtUtc: '2026-09-26T00:00:00Z',
        },
      }),
    );
    vi.stubGlobal('fetch', fetch);
    const result = await createWorkspaceApi(() => 'token').uploadMedia(
      'org',
      file,
      'original',
      new AbortController().signal,
    );
    expect(result.ok).toBe(true);
    const [url, init] = fetch.mock.calls[0]!;
    expect(url).toBe('/api/organizations/org/media');
    expect(init.body).toBeInstanceOf(FormData);
    expect(init.body.get('file').name).toBe('image.png');
    expect(init.body.get('provenance')).toBe('original');
    expect(init.headers.get('Content-Type')).toBeNull();
    expect(init.headers.get('X-CSRF-TOKEN')).toBe('token');
  });

  it('preserves expectedRevision and export idempotency on the tenant route', async () => {
    const fetch = vi.fn().mockResolvedValue(Response.json({ code: 'conflict' }, { status: 409 }));
    vi.stubGlobal('fetch', fetch);
    const result = await createWorkspaceApi(() => 'token').createExport(
      'org',
      'draft',
      3,
      'stable-retry-key',
      new AbortController().signal,
    );
    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.failure.status).toBe(409);
    expect(fetch.mock.calls[0]![0]).toBe('/api/organizations/org/drafts/draft/exports');
    expect(JSON.parse(fetch.mock.calls[0]![1].body)).toEqual({
      expectedRevision: 3,
      idempotencyKey: 'stable-retry-key',
    });
  });
});
