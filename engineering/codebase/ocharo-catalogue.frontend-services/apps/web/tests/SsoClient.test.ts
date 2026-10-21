import { describe, expect, it, vi } from 'vitest';
import { createSsoClient } from '../src/integration/common/OcharoSsoClient';

const session = (values = {}) =>
  new Response(
    JSON.stringify({
      data: {
        enabled: true,
        authenticated: true,
        canEdit: true,
        canApprove: true,
        csrfToken: 'session-csrf',
        ...values,
      },
    }),
    { status: 200 },
  );

describe('Ocharo browser sessions', () => {
  it('adds CSRF only to same-origin writes, including multipart uploads', async () => {
    const fetcher = vi.fn().mockResolvedValueOnce(session()).mockResolvedValue(new Response('{}'));
    const client = createSsoClient(fetcher);
    const body = new FormData();
    body.append('file', 'sample');
    await client.fetch('/api/catalogue/products/1/media', { method: 'POST', body });
    expect(fetcher).toHaveBeenCalledTimes(2);
    const options = fetcher.mock.calls[1]![1] as RequestInit;
    expect(new Headers(options.headers).get('X-CSRF-Token')).toBe('session-csrf');
    expect(options.body).toBe(body);
    await client.fetch('https://other.example/api/products', { method: 'POST', body });
    expect(fetcher.mock.calls[2]![1]).toEqual({ method: 'POST', body });
  });
  it('does not replay a failed mutation or redirect away from unsaved drafts', async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(session())
      .mockResolvedValueOnce(new Response('{}', { status: 401 }));
    const client = createSsoClient(fetcher);
    expect((await client.fetch('/api/products', { method: 'POST' })).status).toBe(401);
    expect(client.current.authenticated).toBe(false);
    expect((await client.fetch('/api/products', { method: 'POST' })).status).toBe(401);
    expect(fetcher).toHaveBeenCalledTimes(2);
  });
  it('refreshes permissions after a forbidden response without retrying the command', async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(session())
      .mockResolvedValueOnce(new Response('{}', { status: 403 }))
      .mockResolvedValueOnce(session({ canEdit: false }));
    const client = createSsoClient(fetcher);
    await client.fetch('/api/products', { method: 'POST' });
    expect(client.current.canEdit).toBe(false);
    expect(fetcher).toHaveBeenCalledTimes(3);
  });
  it('blocks writes and publication without the corresponding server capability', async () => {
    const fetcher = vi.fn().mockResolvedValue(session({ canApprove: false }));
    const client = createSsoClient(fetcher);
    expect((await client.fetch('/api/designs/1/publish', { method: 'POST' })).status).toBe(403);
    for (const path of ['/api/designs/1/publish/', '/api/designs/1/withdraw/', '/api/designs/1/PUBLISH/'])
      expect((await client.fetch(path, { method: 'POST' })).status).toBe(403);
    expect(fetcher).toHaveBeenCalledTimes(1);
  });
  it('keeps the disabled pilot working without CSRF headers', async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(session({ enabled: false, authenticated: false }))
      .mockResolvedValue(new Response('{}'));
    const client = createSsoClient(fetcher);
    await client.fetch('/api/products', { method: 'POST' });
    expect(new Headers(fetcher.mock.calls[1]![1].headers).has('X-CSRF-Token')).toBe(false);
  });
  it('carries the Desk operator name and email while signed in, and drops them when the grant ends', async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(session({ name: ' Malika Operator ', email: 'operator@example.test' }))
      .mockResolvedValueOnce(new Response('{}', { status: 401 }));
    const client = createSsoClient(fetcher);
    await client.initialize();
    expect(client.current).toMatchObject({ name: 'Malika Operator', email: 'operator@example.test' });
    await client.fetch('/api/products');
    expect(client.current).toMatchObject({ authenticated: false, csrfToken: null, name: null, email: null });
  });
  it('reads a Desk without names, blank or non-text values, and an anonymous session as naming no one', async () => {
    for (const values of [
      {},
      { name: null, email: null },
      { name: '  ', email: 42 },
      { authenticated: false, name: 'Stale', email: 'stale@example.test' },
    ]) {
      const client = createSsoClient(vi.fn().mockResolvedValue(session(values)));
      await client.initialize();
      expect(client.current).toMatchObject({ name: null, email: null });
    }
  });
  it('fails closed when the session response is unavailable or malformed', async () => {
    const fetcher = vi.fn().mockResolvedValue(new Response('{}'));
    const client = createSsoClient(fetcher);
    expect((await client.fetch('/api/products', { method: 'POST' })).status).toBe(401);
    expect(fetcher).toHaveBeenCalledTimes(1);
  });
});
