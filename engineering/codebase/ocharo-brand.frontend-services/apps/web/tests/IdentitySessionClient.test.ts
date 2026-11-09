import { describe, expect, it, vi } from 'vitest';
import { createIdentitySessionClient, signInUrl } from '../src/integration/common/IdentitySessionClient';

const session = (values: Record<string, unknown> = {}, person: Record<string, unknown> = {}) =>
  Response.json({
    data: {
      isAuthenticated: true,
      person: { sub: 'person-sub', name: null, picture: null, email: null, phoneNumber: null, ...person },
      organization: { id: 'b8c672a6-c2e4-4516-82ad-5b178e71ace1', name: 'Ocharo', role: 'owner', slug: 'ocharo' },
      permissions: ['brand:edit', 'brand:approve'],
      csrfToken: 'session-csrf',
      expiresAt: '2026-11-03T20:00:00Z',
      ...values,
    },
  });

describe('Ocharo Identity sessions', () => {
  it('reads the session route, its person, organization and permissions', async () => {
    const client = createIdentitySessionClient(
      vi
        .fn()
        .mockResolvedValue(session({}, { name: ' Malika ', phoneNumber: '+998901234567', picture: 'http://x/a.png' })),
    );
    await client.initialize();
    expect(client.current).toMatchObject({
      authenticated: true,
      person: { sub: 'person-sub', name: 'Malika', phoneNumber: '+998901234567', email: null, picture: null },
      organization: { id: 'b8c672a6-c2e4-4516-82ad-5b178e71ace1', role: 'owner' },
      permissions: ['brand:edit', 'brand:approve'],
      csrfToken: 'session-csrf',
    });
  });

  it('adds X-Csrf-Token only to same-origin unsafe API calls, including multipart uploads', async () => {
    const fetcher = vi.fn().mockResolvedValueOnce(session()).mockResolvedValue(new Response('{}'));
    const client = createIdentitySessionClient(fetcher);
    const body = new FormData();
    body.append('file', 'sample');
    await client.fetch('/api/brand/logos/on-light', { method: 'POST', body });
    expect(fetcher).toHaveBeenNthCalledWith(1, '/api/identity/session', expect.anything());
    const options = fetcher.mock.calls[1]![1] as RequestInit;
    expect(new Headers(options.headers).get('X-Csrf-Token')).toBe('session-csrf');
    expect(options.body).toBe(body);
    await client.fetch('/api/brand/draft');
    expect(new Headers((fetcher.mock.calls[2]![1] as RequestInit).headers).has('X-Csrf-Token')).toBe(false);
    await client.fetch('https://other.example/api/products', { method: 'POST', body });
    expect(fetcher.mock.calls[3]![1]).toEqual({ method: 'POST', body });
  });

  it('does not replay a failed mutation and reads a 401 as signed out', async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(session())
      .mockResolvedValueOnce(new Response('{}', { status: 401 }));
    const client = createIdentitySessionClient(fetcher);
    expect((await client.fetch('/api/brand/appearances', { method: 'POST' })).status).toBe(401);
    expect(client.current).toMatchObject({ authenticated: false, csrfToken: null, person: null, permissions: [] });
    expect((await client.fetch('/api/brand/appearances', { method: 'POST' })).status).toBe(401);
    expect(fetcher).toHaveBeenCalledTimes(2);
  });

  it('re-reads the session after a forbidden response without retrying the command', async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(session())
      .mockResolvedValueOnce(new Response('{}', { status: 403 }))
      .mockResolvedValueOnce(session({ permissions: [] }));
    const client = createIdentitySessionClient(fetcher);
    await client.fetch('/api/brand/appearances', { method: 'POST' });
    expect(client.current.permissions).toEqual([]);
    expect(client.current.organizationDenied).toBe(false);
    expect(fetcher).toHaveBeenCalledTimes(3);
  });

  it('marks a session whose organization Brand refuses', async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(session())
      .mockResolvedValueOnce(
        Response.json({ code: 'Forbidden', brandCode: 'organization_access_required' }, { status: 403 }),
      )
      .mockResolvedValueOnce(session());
    const client = createIdentitySessionClient(fetcher);
    await client.fetch('/api/brand/draft');
    expect(client.current.organizationDenied).toBe(true);
    expect((await client.fetch('/api/brand/appearances', { method: 'POST' })).status).toBe(403);
    expect(fetcher).toHaveBeenCalledTimes(3);
  });

  it('blocks writes without brand:edit and publication without brand:approve', async () => {
    const fetcher = vi.fn().mockResolvedValue(session({ permissions: ['brand:edit'] }));
    const client = createIdentitySessionClient(fetcher);
    for (const path of ['/api/brand/publish', '/api/brand/publish/', '/api/brand/1/withdraw/', '/api/brand/PUBLISH/'])
      expect((await client.fetch(path, { method: 'POST' })).status).toBe(403);
    expect(fetcher).toHaveBeenCalledTimes(1);
    const reader = createIdentitySessionClient(vi.fn().mockResolvedValue(session({ permissions: [] })));
    expect((await reader.fetch('/api/brand/identity', { method: 'PUT' })).status).toBe(403);
  });

  it('signs out with the CSRF token, then leaves through the end-session address', async () => {
    const navigate = vi.fn();
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(session({ permissions: [] }))
      .mockResolvedValueOnce(
        Response.json({ data: { endSessionUrl: 'https://identity.example.test/connect/endsession?x=1' } }),
      );
    const client = createIdentitySessionClient(fetcher, navigate);
    await client.initialize();
    expect(await client.signOut()).toBe(true);
    expect(fetcher).toHaveBeenLastCalledWith(
      '/api/identity/sign-out',
      expect.objectContaining({ method: 'POST', headers: expect.objectContaining({ 'X-Csrf-Token': 'session-csrf' }) }),
    );
    expect(client.current.authenticated).toBe(false);
    expect(navigate).toHaveBeenCalledWith('https://identity.example.test/connect/endsession?x=1');
  });

  it('keeps the session when sign-out fails, and never calls out without one', async () => {
    const navigate = vi.fn();
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(session())
      .mockResolvedValueOnce(new Response('{}', { status: 500 }));
    const client = createIdentitySessionClient(fetcher, navigate);
    expect(await client.signOut()).toBe(false);
    expect(fetcher).not.toHaveBeenCalled();
    await client.initialize();
    expect(await client.signOut()).toBe(false);
    expect(client.current.authenticated).toBe(true);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('reads an anonymous, unavailable or malformed session as signed out and fails closed', async () => {
    for (const response of [
      Response.json({ data: { isAuthenticated: false } }),
      new Response('{}', { status: 500 }),
      Response.json({ data: { isAuthenticated: true, person: { sub: '' }, csrfToken: 'x' } }),
      Response.json({ data: { isAuthenticated: true, person: { sub: 'a' } } }),
    ]) {
      const fetcher = vi.fn().mockResolvedValue(response);
      const client = createIdentitySessionClient(fetcher);
      await client.initialize();
      expect(client.current.authenticated).toBe(false);
      expect((await client.fetch('/api/brand/appearances', { method: 'POST' })).status).toBe(401);
      expect(fetcher).toHaveBeenCalledTimes(1);
    }
  });

  it('starts sign-in only towards a local return path', () => {
    expect(signInUrl('/logos?tab=1')).toBe('/api/identity/sign-in?returnUrl=%2Flogos%3Ftab%3D1');
    expect(signInUrl('https://evil.example/')).toBe('/api/identity/sign-in?returnUrl=%2F');
    expect(signInUrl('//evil.example/')).toBe('/api/identity/sign-in?returnUrl=%2F');
  });
});
