import { describe, expect, it, vi } from 'vitest';
import { createIdentityClient, signInPath } from '../src/integration/common/IdentitySessionClient';
import { hasSignInError, signInUrl, startSession } from '../src/application/common/UseIdentitySession';
import { identityClient } from '../src/integration/common/IdentitySessionClient';
import { OwnerSessionData } from './helpers/IdentitySessions';

const session = (values: Record<string, unknown> = {}) =>
  new Response(JSON.stringify({ data: { ...OwnerSessionData, ...values } }), { status: 200 });

describe('Identity BFF sessions', () => {
  it('reads the session route once, and again only when forced', async () => {
    const fetcher = vi.fn().mockImplementation(() => Promise.resolve(session()));
    const client = createIdentityClient({ fetch: fetcher });
    await client.initialize();
    await client.initialize();
    expect(fetcher).toHaveBeenCalledTimes(1);
    expect(fetcher.mock.calls[0]![0]).toBe('/api/identity/session');
    expect(client.current).toMatchObject({
      reachable: true,
      isAuthenticated: true,
      csrfToken: 'csrf-token',
      permissions: ['catalogue:edit', 'catalogue:approve'],
      organization: { id: OwnerSessionData.organization.id, role: 'owner' },
    });
    await client.initialize(true);
    expect(fetcher).toHaveBeenCalledTimes(2);
  });

  it('adds X-Csrf-Token only to same-origin API writes, including multipart uploads', async () => {
    const fetcher = vi.fn().mockResolvedValueOnce(session()).mockResolvedValue(new Response('{}'));
    const client = createIdentityClient({ fetch: fetcher });
    const body = new FormData();
    body.append('file', 'sample');
    await client.fetch('/api/catalogue/products/1/media', { method: 'POST', body });
    expect(fetcher).toHaveBeenCalledTimes(2);
    const options = fetcher.mock.calls[1]![1] as RequestInit;
    expect(new Headers(options.headers).get('X-Csrf-Token')).toBe('csrf-token');
    expect(options.body).toBe(body);
    await client.fetch('/api/catalogue/products');
    expect(new Headers((fetcher.mock.calls[2]![1] as RequestInit).headers).has('X-Csrf-Token')).toBe(false);
    await client.fetch('https://other.example/api/products', { method: 'POST', body });
    expect(fetcher.mock.calls[3]![1]).toEqual({ method: 'POST', body });
  });

  it('leaves the public feed and the session routes to themselves', async () => {
    const fetcher = vi.fn().mockResolvedValue(new Response('{}'));
    const client = createIdentityClient({ fetch: fetcher });
    await client.fetch('/api/public/catalogue');
    await client.fetch('/api/identity/session');
    expect(fetcher.mock.calls.map(([input]) => input)).toEqual(['/api/public/catalogue', '/api/identity/session']);
  });

  it('blocks writes without a session, without catalogue:edit, and publication without catalogue:approve', async () => {
    const signedOut = createIdentityClient({ fetch: vi.fn().mockResolvedValue(session({ isAuthenticated: false })) });
    expect((await signedOut.fetch('/api/catalogue/products', { method: 'POST' })).status).toBe(401);
    const viewer = createIdentityClient({ fetch: vi.fn().mockResolvedValue(session({ permissions: [] })) });
    expect((await viewer.fetch('/api/catalogue/products', { method: 'POST' })).status).toBe(403);
    const fetcher = vi.fn().mockResolvedValue(session({ permissions: ['catalogue:edit'] }));
    const editor = createIdentityClient({ fetch: fetcher });
    for (const path of [
      '/api/designs/1/publish',
      '/api/designs/1/publish/',
      '/api/designs/1/withdraw/',
      '/api/designs/1/PUBLISH/',
    ])
      expect((await editor.fetch(path, { method: 'POST' })).status).toBe(403);
    expect(fetcher).toHaveBeenCalledTimes(1);
  });

  it('does not replay a failed write, and reads a 401 as the session ending', async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(session({ person: { ...OwnerSessionData.person, name: ' Malika Operator ' } }))
      .mockResolvedValueOnce(new Response('{}', { status: 401 }));
    const client = createIdentityClient({ fetch: fetcher });
    await client.initialize();
    expect(client.current.person?.name).toBe('Malika Operator');
    expect((await client.fetch('/api/products', { method: 'POST' })).status).toBe(401);
    expect(client.current).toMatchObject({ isAuthenticated: false, csrfToken: null, person: null, reachable: true });
    expect((await client.fetch('/api/products', { method: 'POST' })).status).toBe(401);
    expect(fetcher).toHaveBeenCalledTimes(2);
  });

  it('reads the session again after a 403 or a stale CSRF token, without retrying the write', async () => {
    const stale = new Response(JSON.stringify({ status: 400, code: 'AntiforgeryValidationFailed' }), { status: 400 });
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(session())
      .mockResolvedValueOnce(new Response('{}', { status: 403 }))
      .mockResolvedValueOnce(session({ permissions: ['catalogue:edit'] }))
      .mockResolvedValueOnce(stale)
      .mockResolvedValueOnce(session({ csrfToken: 'fresh-token' }));
    const client = createIdentityClient({ fetch: fetcher });
    await client.fetch('/api/products', { method: 'POST' });
    expect(client.current.permissions).toEqual(['catalogue:edit']);
    await client.fetch('/api/products', { method: 'POST' });
    expect(client.current.csrfToken).toBe('fresh-token');
    expect(fetcher).toHaveBeenCalledTimes(5);
  });

  it('fails closed when the session response is unavailable or malformed', async () => {
    for (const answer of [
      new Response('{}'),
      new Response('{}', { status: 503 }),
      session({ csrfToken: '' }),
      session({ person: { sub: '' } }),
      session({ permissions: 'catalogue:edit' }),
    ]) {
      const fetcher = vi.fn().mockResolvedValue(answer);
      const client = createIdentityClient({ fetch: fetcher });
      expect((await client.fetch('/api/products', { method: 'POST' })).status).toBe(401);
      expect(client.current).toMatchObject({ isAuthenticated: false, reachable: false });
      expect(fetcher).toHaveBeenCalledTimes(1);
    }
  });

  it('drops a picture that is not an HTTPS link, and blanks, from the person', async () => {
    const client = createIdentityClient({
      fetch: vi.fn().mockResolvedValue(
        session({
          person: { sub: 'p', name: '  ', email: 42, phoneNumber: '+998901234567', picture: 'http://x.test/a.png' },
        }),
      ),
    });
    await client.initialize();
    expect(client.current.person).toEqual({
      sub: 'p',
      name: null,
      email: null,
      phoneNumber: '+998901234567',
      picture: null,
    });
  });

  it('signs out with the CSRF token, then sends the browser to the end-session address', async () => {
    const navigate = vi.fn();
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(session())
      .mockResolvedValueOnce(
        Response.json({ data: { endSessionUrl: 'https://identity.example.test/connect/endsession?x=1' } }),
      );
    const client = createIdentityClient({ fetch: fetcher, navigate });
    await client.initialize();
    expect(await client.signOut()).toBe(true);
    expect(fetcher.mock.calls[1]).toEqual([
      '/api/identity/sign-out',
      expect.objectContaining({ method: 'POST', headers: { 'X-Csrf-Token': 'csrf-token' } }),
    ]);
    expect(navigate).toHaveBeenCalledWith('https://identity.example.test/connect/endsession?x=1');
    expect(client.current.isAuthenticated).toBe(false);
  });

  it('keeps the session when sign-out is refused, and never navigates to an unsafe address', async () => {
    const navigate = vi.fn();
    const refused = createIdentityClient({
      fetch: vi
        .fn()
        .mockResolvedValueOnce(session())
        .mockResolvedValueOnce(new Response('{}', { status: 400 })),
      navigate,
    });
    await refused.initialize();
    expect(await refused.signOut()).toBe(false);
    expect(refused.current.isAuthenticated).toBe(true);
    const unsafe = createIdentityClient({
      fetch: vi
        .fn()
        .mockResolvedValueOnce(session())
        .mockResolvedValueOnce(Response.json({ data: { endSessionUrl: 'javascript:alert(1)' } })),
      navigate,
    });
    await unsafe.initialize();
    expect(await unsafe.signOut()).toBe(true);
    expect(navigate).toHaveBeenCalledWith('/');
  });
});

describe('Catalogue sign-in start', () => {
  it('builds sign-in addresses that return to a local path only', () => {
    expect(signInPath('/catalogues?x=1')).toBe('/api/identity/sign-in?returnUrl=%2Fcatalogues%3Fx%3D1');
    expect(signInUrl('//evil.example/')).toBe('/api/identity/sign-in?returnUrl=%2F');
    expect(hasSignInError('?signInError=callback_failed')).toBe(true);
    expect(hasSignInError('?locale=en')).toBe(false);
  });

  it('sends a signed-out browser to sign in, unless the last sign-in failed or the API did not answer', async () => {
    const location = { pathname: '/stock', search: '?view=all', replace: vi.fn() };
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(session({ isAuthenticated: false })));
    await identityClient.initialize(true);
    expect(await startSession(location)).toBe(false);
    expect(location.replace).toHaveBeenCalledWith('/api/identity/sign-in?returnUrl=%2Fstock%3Fview%3Dall');

    location.replace.mockClear();
    expect(await startSession({ ...location, search: '?signInError=callback_failed' })).toBe(true);
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('{}', { status: 502 })));
    await identityClient.initialize(true);
    expect(await startSession(location)).toBe(true);
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(session()));
    await identityClient.initialize(true);
    expect(await startSession(location)).toBe(true);
    expect(location.replace).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
  });
});
