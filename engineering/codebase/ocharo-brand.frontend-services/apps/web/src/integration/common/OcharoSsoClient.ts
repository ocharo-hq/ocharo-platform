/** App-local session transport. Credentials stay in secure cookies; writes are never replayed. */
export interface SsoSession {
  enabled: boolean;
  authenticated: boolean;
  canEdit?: boolean;
  canApprove?: boolean;
  csrfToken?: string | null;
  accountUrl?: string | null;
  /** The Desk operator's current display name while authenticated; null when Desk has none. */
  name?: string | null;
  /** The Desk operator's current email while authenticated; null when Desk has none. */
  email?: string | null;
}

export function createSsoClient(fetchImplementation: typeof fetch = globalThis.fetch) {
  let current: SsoSession = { enabled: false, authenticated: false };
  let pending: Promise<SsoSession> | undefined;
  const listeners = new Set<(session: SsoSession) => void>();
  function update(value: SsoSession) {
    current = value;
    for (const listener of listeners) listener(value);
    return value;
  }
  function initialize(force = false): Promise<SsoSession> {
    if (pending && !force) return pending;
    pending = (async () => {
      try {
        const response = await fetchImplementation('/api/sso/session', {
          credentials: 'same-origin',
          cache: 'no-store',
        });
        if (!response.ok) throw new Error('Session unavailable');
        const value = (await response.json())?.data as SsoSession | undefined;
        if (!value || typeof value.enabled !== 'boolean' || typeof value.authenticated !== 'boolean')
          throw new Error('Invalid session');
        return update({
          enabled: value.enabled,
          authenticated: value.authenticated,
          canEdit: value.canEdit === true,
          canApprove: value.canApprove === true,
          accountUrl: safeAccountUrl(value.accountUrl),
          csrfToken: typeof value.csrfToken === 'string' ? value.csrfToken : null,
          name: value.authenticated ? identityText(value.name) : null,
          email: value.authenticated ? identityText(value.email) : null,
        });
      } catch {
        return update({ enabled: true, authenticated: false, canEdit: false, canApprove: false });
      }
    })();
    return pending;
  }
  const sessionFetch: typeof fetch = async (input, init) => {
    const raw = input instanceof Request ? input.url : String(input);
    const url = new URL(raw, globalThis.location.origin);
    if (
      url.origin !== globalThis.location.origin ||
      !url.pathname.startsWith('/api/') ||
      url.pathname.startsWith('/api/public/')
    )
      return fetchImplementation(input, init);
    await initialize();
    const session = current;
    const method = (init?.method ?? (input instanceof Request ? input.method : 'GET')).toUpperCase();
    const writing = !['GET', 'HEAD', 'OPTIONS'].includes(method);
    const headers = new Headers(init?.headers ?? (input instanceof Request ? input.headers : undefined));
    if (session.enabled && writing) {
      if (!session.authenticated) return problem(401, 'sso_sign_in_required');
      if (!session.canEdit || (/\/(publish|withdraw)\/*$/i.test(url.pathname) && !session.canApprove))
        return problem(403, 'sso_permission_required');
      if (!session.csrfToken) return problem(403, 'sso_csrf_invalid');
      headers.set('X-CSRF-Token', session.csrfToken);
    }
    const response = await fetchImplementation(input, { ...init, headers, credentials: 'same-origin' });
    if (session.enabled && response.status === 401) update({ ...current, ...signedOut });
    else if (session.enabled && response.status === 403) await initialize(true);
    return response;
  };
  function problem(status: number, code: string) {
    return new Response(JSON.stringify({ type: 'about:blank', title: 'Ocharo access required', status, code }), {
      status,
      headers: { 'content-type': 'application/problem+json' },
    });
  }
  /**
   * Ends this app's Desk grant (`POST /api/sso/logout`); the Desk session and other apps stay signed in. Readers may
   * sign out too, so this bypasses the write gate of `sessionFetch` and sends only the CSRF token. Resolves `true` once
   * the grant is revoked; the session then reads as signed out without reloading the tab.
   */
  async function signOut(): Promise<boolean> {
    const session = current;
    if (!session.enabled || !session.authenticated || !session.csrfToken) return false;
    try {
      const response = await fetchImplementation('/api/sso/logout', {
        method: 'POST',
        credentials: 'same-origin',
        cache: 'no-store',
        headers: { 'X-CSRF-Token': session.csrfToken },
      });
      if (!response.ok) return false;
      update({ ...current, ...signedOut });
      return true;
    } catch {
      return false;
    }
  }
  return {
    initialize,
    fetch: sessionFetch,
    signOut,
    get current() {
      return current;
    },
    subscribe(listener: (session: SsoSession) => void) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
  };
}

/** What a session loses when its grant ends: the capabilities, the token and who held it. */
const signedOut = {
  authenticated: false,
  canEdit: false,
  canApprove: false,
  csrfToken: null,
  name: null,
  email: null,
} as const satisfies Partial<SsoSession>;

/** A name or email Desk sent, trimmed; anything else, or a blank, reads as none. */
function identityText(value: unknown): string | null {
  return typeof value === 'string' && value.trim() !== '' ? value.trim() : null;
}

function safeAccountUrl(value: unknown): string | null {
  try {
    const url = new URL(String(value));
    return url.protocol === 'https:' &&
      !url.username &&
      !url.password &&
      !url.search &&
      !url.hash &&
      url.pathname === '/'
      ? url.href
      : null;
  } catch {
    return null;
  }
}

export const ssoClient = createSsoClient();
