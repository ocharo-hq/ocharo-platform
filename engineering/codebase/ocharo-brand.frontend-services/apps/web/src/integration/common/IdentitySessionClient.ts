/** The permissions Brand's API checks in the session's organization. */
export const BrandPermission = { Edit: 'brand:edit', Approve: 'brand:approve' } as const;

/** The signed-in person Identity names; each text is null when the token carries none. */
export interface IdentityPerson {
  readonly sub: string;
  readonly name: string | null;
  readonly email: string | null;
  readonly phoneNumber: string | null;
  /** An HTTPS picture address, or null. */
  readonly picture: string | null;
}

/** The session's active organization and the person's role in it. */
export interface IdentityOrganization {
  readonly id: string;
  readonly name: string | null;
  readonly role: string | null;
  readonly slug: string | null;
}

/**
 * Brand's Identity BFF session (`GET /api/identity/session`). Tokens stay server-side; the browser holds only the session
 * cookie, and unsafe requests echo `csrfToken` in `X-Csrf-Token`. Signed out, every member but `authenticated` is empty.
 */
export interface IdentitySession {
  readonly authenticated: boolean;
  readonly person: IdentityPerson | null;
  readonly organization: IdentityOrganization | null;
  /** The person's permissions in the active organization, such as `brand:edit`. */
  readonly permissions: readonly string[];
  readonly csrfToken: string | null;
  readonly expiresAt: string | null;
  /** True once Brand's API refused this session's organization: it is not the one Brand serves. */
  readonly organizationDenied: boolean;
}

const signedOut: IdentitySession = {
  authenticated: false,
  person: null,
  organization: null,
  permissions: [],
  csrfToken: null,
  expiresAt: null,
  organizationDenied: false,
};

/** The BFF route that starts sign-in at Ocharo's Identity instance and comes back to the local `returnUrl`. */
export function signInUrl(returnUrl: string): string {
  const local = returnUrl.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/';
  return `/api/identity/sign-in?returnUrl=${encodeURIComponent(local)}`;
}

/**
 * The session client: reads the session, adds `X-Csrf-Token` to same-origin unsafe API calls and refuses them locally when
 * the session lacks the permission, and signs out. Writes are never replayed; a 401 reads as signed out, a 403 re-reads
 * the session.
 */
export function createIdentitySessionClient(
  fetchImplementation: typeof fetch = globalThis.fetch,
  navigate: (url: string) => void = (url) => globalThis.location.assign(url),
) {
  let current: IdentitySession = signedOut;
  let pending: Promise<IdentitySession> | undefined;
  const listeners = new Set<(session: IdentitySession) => void>();
  function update(value: IdentitySession) {
    current = value;
    for (const listener of listeners) listener(value);
    return value;
  }
  function initialize(force = false): Promise<IdentitySession> {
    if (pending && !force) return pending;
    pending = (async () => {
      try {
        const response = await fetchImplementation('/api/identity/session', {
          credentials: 'same-origin',
          cache: 'no-store',
        });
        if (!response.ok) throw new Error('Session unavailable');
        return update(readSession((await response.json())?.data));
      } catch {
        return update(signedOut);
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
    if (writing) {
      if (!session.authenticated || !session.csrfToken) return problem(401, 'sign_in_required');
      if (!hasPermission(session, BrandPermission.Edit)) return problem(403, 'edit_permission_required');
      if (/\/(publish|withdraw)\/*$/i.test(url.pathname) && !hasPermission(session, BrandPermission.Approve))
        return problem(403, 'approval_permission_required');
      headers.set('X-Csrf-Token', session.csrfToken);
    }
    const response = await fetchImplementation(input, { ...init, headers, credentials: 'same-origin' });
    if (response.status === 401) update(signedOut);
    else if (response.status === 403) {
      const denied = (await problemCode(response)) === 'organization_access_required';
      const refreshed = await initialize(true);
      if (denied && refreshed.authenticated) update({ ...refreshed, organizationDenied: true });
    }
    return response;
  };
  function problem(status: number, code: string) {
    return new Response(JSON.stringify({ type: 'about:blank', title: 'Ocharo access required', status, code }), {
      status,
      headers: { 'content-type': 'application/problem+json' },
    });
  }
  /**
   * Ends the Brand session (`POST /api/identity/sign-out`, any member may) and sends the browser to Identity's end-session
   * address the answer names. Resolves `false`, keeping the session, when the sign-out fails or there is none to end.
   */
  async function signOut(): Promise<boolean> {
    const session = current;
    if (!session.authenticated || !session.csrfToken) return false;
    try {
      const response = await fetchImplementation('/api/identity/sign-out', {
        method: 'POST',
        credentials: 'same-origin',
        cache: 'no-store',
        headers: { 'X-Csrf-Token': session.csrfToken, 'Content-Type': 'application/json' },
        body: '{}',
      });
      if (!response.ok) return false;
      const destination = endSessionUrl((await response.json().catch(() => null))?.data?.endSessionUrl);
      update(signedOut);
      navigate(destination);
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
    subscribe(listener: (session: IdentitySession) => void) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
  };
}

/** Whether the session holds `permission` in its organization and Brand did not refuse that organization. */
export function hasPermission(session: IdentitySession, permission: string): boolean {
  return session.authenticated && !session.organizationDenied && session.permissions.includes(permission);
}

/** Reads the session route's `data`; anything malformed reads as signed out. */
function readSession(value: unknown): IdentitySession {
  if (!isRecord(value) || value.isAuthenticated !== true || !isRecord(value.person)) return signedOut;
  const person = value.person;
  if (
    typeof person.sub !== 'string' ||
    person.sub === '' ||
    typeof value.csrfToken !== 'string' ||
    value.csrfToken === ''
  )
    return signedOut;
  const organization =
    isRecord(value.organization) && typeof value.organization.id === 'string' ? value.organization : null;
  return {
    authenticated: true,
    person: {
      sub: person.sub,
      name: text(person.name),
      email: text(person.email),
      phoneNumber: text(person.phoneNumber),
      picture: httpsUrl(person.picture),
    },
    organization: organization
      ? {
          id: String(organization.id),
          name: text(organization.name),
          role: text(organization.role),
          slug: text(organization.slug),
        }
      : null,
    permissions: Array.isArray(value.permissions)
      ? value.permissions.filter((item): item is string => typeof item === 'string')
      : [],
    csrfToken: value.csrfToken,
    expiresAt: typeof value.expiresAt === 'string' ? value.expiresAt : null,
    organizationDenied: false,
  };
}

async function problemCode(response: Response): Promise<string | null> {
  try {
    const parsed: unknown = await response.clone().json();
    return isRecord(parsed) && typeof parsed.code === 'string' ? parsed.code : null;
  } catch {
    return null;
  }
}

/** Identity's end-session address over HTTPS, or a same-origin one; anything else lands on Brand's own start page. */
function endSessionUrl(value: unknown): string {
  try {
    const url = new URL(String(value), globalThis.location.origin);
    return url.protocol === 'https:' || url.origin === globalThis.location.origin ? url.href : '/';
  } catch {
    return '/';
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/** A text Identity sent, trimmed; anything else, or a blank, reads as none. */
function text(value: unknown): string | null {
  return typeof value === 'string' && value.trim() !== '' ? value.trim() : null;
}

function httpsUrl(value: unknown): string | null {
  if (typeof value !== 'string') return null;
  try {
    return new URL(value).protocol === 'https:' ? value : null;
  } catch {
    return null;
  }
}

/** The app's session client; it reads the global fetch on every call. */
export const identitySession = createIdentitySessionClient((input, init) => globalThis.fetch(input, init));
