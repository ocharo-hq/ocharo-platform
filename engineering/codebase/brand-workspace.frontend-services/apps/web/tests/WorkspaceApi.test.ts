import { afterEach, describe, expect, it, vi } from 'vitest';

import { createWorkspaceApi } from '@/integration/common/WorkspaceApi';

const session = {
  authenticated: false,
  user: null,
  sessionId: null,
  csrfToken: '',
  organizationId: null,
  role: null,
  permissions: [],
};
afterEach(() => vi.unstubAllGlobals());

describe('published SDK workspace transport', () => {
  it('unwraps real data envelopes before validating session payloads', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json({ data: { isAuthenticated: false } })));
    const api = createWorkspaceApi(() => session.csrfToken);
    expect(await api.getSession()).toEqual({ ok: true, value: session });
  });

  it('rejects incomplete authenticated sessions', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json({ data: { isAuthenticated: true } })));
    const result = await createWorkspaceApi(() => '').getSession();
    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.failure.code).toBe('protocol');
  });

  it('sends the session-bound CSRF token to sign out and reads the end-session address', async () => {
    const fetch = vi.fn().mockResolvedValue(Response.json({ data: { endSessionUrl: 'https://localhost:8404/connect/endsession' } }));
    vi.stubGlobal('fetch', fetch);
    const result = await createWorkspaceApi(() => 'bound-token').logout();
    expect(result).toEqual({ ok: true, value: { endSessionUrl: 'https://localhost:8404/connect/endsession' } });
    const [input, init] = fetch.mock.calls[0]!;
    expect(String(input)).toContain('/api/identity/sign-out');
    expect(init.credentials).toBe('same-origin');
    expect(init.headers.get('X-Csrf-Token')).toBe('bound-token');
  });

  it('maps an authenticated BFF session onto the Desk session', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json({ data: {
      isAuthenticated: true,
      person: { sub: 'person', name: null, email: null, phoneNumber: '+998901234567', picture: 'https://localhost:8404/api/account/pictures/person?v=1' },
      organization: { id: 'org', role: 'owner' },
      permissions: ['desk:edit', 'desk:approve'],
      csrfToken: 'bound-token',
      expiresAt: '2026-11-07T00:00:00Z',
    } })));
    expect(await createWorkspaceApi(() => '').getSession()).toEqual({ ok: true, value: {
      authenticated: true,
      user: { id: 'person', email: '', displayName: '+998901234567', phone: '+998901234567', picture: 'https://localhost:8404/api/account/pictures/person?v=1' },
      sessionId: 'person',
      csrfToken: 'bound-token',
      organizationId: 'org',
      role: 'owner',
      permissions: ['desk:edit', 'desk:approve'],
    } });
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
    const fetch = vi.fn().mockResolvedValue(Response.json({ code: 'Conflict' }, { status: 409 }));
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
