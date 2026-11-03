/**
 * The Identity BFF session transport. Sign-in happens at Ocharo's Identity instance; the API keeps the tokens and the
 * browser only a secure session cookie. Every unsafe API call echoes the session's CSRF token in `X-Csrf-Token`, and a
 * write is never replayed.
 */

/** The permissions Ocharo's Identity grants Catalogue's client, as the session route lists them. */
export const CataloguePermission = { Edit: 'catalogue:edit', Approve: 'catalogue:approve' } as const;

/** The signed-in person, from Identity's ID token; a blank or non-text value reads as none. */
export interface IdentityPerson {
  readonly sub: string;
  readonly name: string | null;
  /** An absolute HTTPS link to the person's picture, or null. */
  readonly picture: string | null;
  readonly email: string | null;
  readonly phoneNumber: string | null;
}

/** The session's active organization and the person's role there. */
export interface IdentityOrganization {
  readonly id: string;
  readonly name: string | null;
  readonly role: string | null;
  readonly slug: string | null;
}

/** What `GET /api/identity/session` answered, or the signed-out session when it did not answer. */
export interface IdentitySession {
  /** Whether the API answered the last session read; false until it has, and after a failed read. */
  readonly reachable: boolean;
  readonly isAuthenticated: boolean;
  readonly person: IdentityPerson | null;
  readonly organization: IdentityOrganization | null;
  /** The active organization's permissions, such as `catalogue:edit`. */
  readonly permissions: readonly string[];
  readonly csrfToken: string | null;
  readonly expiresAt: string | null;
}

export interface IdentityClientOptions {
  readonly fetch?: typeof fetch;
  /** Sends the browser to another address: Identity's end-session page after sign-out. */
  readonly navigate?: (url: string) => void;
  /** A session already known, served until a forced read replaces it. */
  readonly initial?: IdentitySession;
}

/** The session before any read, and after one fails or ends. */
export const SignedOutSession: IdentitySession = Object.freeze({
  reachable: false,
  isAuthenticated: false,
  person: null,
  organization: null,
  permissions: [],
  csrfToken: null,
  expiresAt: null,
});

/** The BFF route that starts sign-in at Ocharo's Identity instance and returns to a local path. */
export const signInPath = (returnUrl: string) => `/api/identity/sign-in?returnUrl=${encodeURIComponent(returnUrl)}`;

/** Whether `session` may edit the catalogue. */
export const canEdit = (session: IdentitySession) =>
  session.isAuthenticated && session.permissions.includes(CataloguePermission.Edit);

/** Whether `session` may publish and withdraw catalogue releases. */
export const canApprove = (session: IdentitySession) =>
  canEdit(session) && session.permissions.includes(CataloguePermission.Approve);

/** Publication actions, which need `catalogue:approve` besides `catalogue:edit`. */
const PublicationPath = /\/(publish|withdraw)\/*$/i;

export function createIdentityClient(options: IdentityClientOptions = {}) {
  // Read the global fetch per call, so a page (or a test) that replaces it is honored.
  const fetchImplementation: typeof fetch = options.fetch ?? ((input, init) => globalThis.fetch(input, init));
  const navigate = options.navigate ?? ((url: string) => globalThis.location.assign(url));
  let current: IdentitySession = options.initial ?? SignedOutSession;
  let pending: Promise<IdentitySession> | undefined = options.initial ? Promise.resolve(options.initial) : undefined;
  const listeners = new Set<(session: IdentitySession) => void>();
  function update(value: IdentitySession) {
    current = value;
    for (const listener of listeners) listener(value);
    return value;
  }
  /** Reads `GET /api/identity/session` once; `force` reads again. A failed or malformed answer reads as signed out. */
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
        return update(SignedOutSession);
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
      url.pathname.startsWith('/api/public/') ||
      url.pathname.startsWith('/api/identity/')
    )
      return fetchImplementation(input, init);
    await initialize();
    const session = current;
    const method = (init?.method ?? (input instanceof Request ? input.method : 'GET')).toUpperCase();
    const writing = !['GET', 'HEAD', 'OPTIONS'].includes(method);
    const headers = new Headers(init?.headers ?? (input instanceof Request ? input.headers : undefined));
    if (writing) {
      if (!session.isAuthenticated || !session.csrfToken) return problem(401, 'SignInRequired');
      if (!canEdit(session) || (PublicationPath.test(url.pathname) && !canApprove(session)))
        return problem(403, 'AccessDenied');
      headers.set('X-Csrf-Token', session.csrfToken);
    }
    const response = await fetchImplementation(input, { ...init, headers, credentials: 'same-origin' });
    if (response.status === 401) update({ ...SignedOutSession, reachable: true });
    else if (
      response.status === 403 ||
      (response.status === 400 && (await problemCode(response)) === 'AntiforgeryValidationFailed')
    )
      await initialize(true);
    return response;
  };
  /**
   * Ends the session here (`POST /api/identity/sign-out` with its CSRF token), then sends the browser to Identity's
   * end-session page, which ends the Ocharo sign-in for every app and returns here. Resolves `false` when there was no
   * session to end or the API refused.
   */
  async function signOut(): Promise<boolean> {
    const session = current;
    if (!session.isAuthenticated || !session.csrfToken) return false;
    try {
      const response = await fetchImplementation('/api/identity/sign-out', {
        method: 'POST',
        credentials: 'same-origin',
        cache: 'no-store',
        headers: { 'X-Csrf-Token': session.csrfToken },
      });
      if (!response.ok) return false;
      const endSessionUrl = safeDestination((await response.json().catch(() => null))?.data?.endSessionUrl);
      update({ ...SignedOutSession, reachable: true });
      navigate(endSessionUrl ?? '/');
      return true;
    } catch {
      return false;
    }
  }
  return {
    initialize,
    signOut,
    fetch: sessionFetch,
    get current() {
      return current;
    },
    subscribe(listener: (session: IdentitySession) => void) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
  };
}

/** Maps the session route's `data` into a session; anything malformed fails closed as signed out. */
function readSession(value: unknown): IdentitySession {
  if (!record(value) || typeof value.isAuthenticated !== 'boolean') throw new Error('Invalid session');
  if (!value.isAuthenticated) return { ...SignedOutSession, reachable: true };
  const person = value.person;
  if (
    !record(person) ||
    typeof person.sub !== 'string' ||
    person.sub === '' ||
    typeof value.csrfToken !== 'string' ||
    value.csrfToken === '' ||
    !Array.isArray(value.permissions)
  )
    throw new Error('Invalid session');
  const organization =
    record(value.organization) && typeof value.organization.id === 'string' ? value.organization : null;
  return {
    reachable: true,
    isAuthenticated: true,
    person: {
      sub: person.sub,
      name: text(person.name),
      picture: httpsUrl(person.picture),
      email: text(person.email),
      phoneNumber: text(person.phoneNumber),
    },
    organization: organization
      ? {
          id: String(organization.id),
          name: text(organization.name),
          role: text(organization.role),
          slug: text(organization.slug),
        }
      : null,
    permissions: value.permissions.filter((permission): permission is string => typeof permission === 'string'),
    csrfToken: value.csrfToken,
    expiresAt: text(value.expiresAt),
  };
}

function problem(status: number, code: string) {
  return new Response(JSON.stringify({ type: 'about:blank', title: 'Ocharo access required', status, code }), {
    status,
    headers: { 'content-type': 'application/problem+json' },
  });
}

async function problemCode(response: Response): Promise<unknown> {
  try {
    return (await response.clone().json())?.code;
  } catch {
    return undefined;
  }
}

function record(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/** A value Identity sent, trimmed; anything else, or a blank, reads as none. */
function text(value: unknown): string | null {
  return typeof value === 'string' && value.trim() !== '' ? value.trim() : null;
}

function httpsUrl(value: unknown): string | null {
  const link = text(value);
  if (!link) return null;
  try {
    return new URL(link).protocol === 'https:' ? link : null;
  } catch {
    return null;
  }
}

/** Identity's end-session address (absolute HTTPS) or a local path; anything else is dropped. */
function safeDestination(value: unknown): string | null {
  const link = text(value);
  if (!link) return null;
  if (link.startsWith('/') && !link.startsWith('//')) return link;
  return httpsUrl(link);
}

export const identityClient = createIdentityClient();
