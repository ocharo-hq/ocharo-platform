import {
  AuthStatus,
  BffSessionExtensions,
  createAuthBridge,
  createBffStrategy,
  type AuthStrategyHost,
  type BffStrategy,
  type BffUser,
} from '@wow-two-beta/fe/auth';
import { createApiClient, wowTwoEnvelope, type ApiClient } from '@wow-two-beta/fe/foundation/http';

/** The permissions Ocharo's Identity grants Catalogue's client, as the session route lists them. */
export const CataloguePermission = { Edit: 'catalogue:edit', Approve: 'catalogue:approve' } as const;

/** The query a failed sign-in comes back with (`signInError=callback_failed`). */
export const SignInErrorParameter = 'signInError';

/** The ASP.NET Core refusal of a write whose CSRF token went stale (`code` of the 400 problem). */
const AntiforgeryFailedCode = 'AntiforgeryValidationFailed';

/**
 * Catalogue's session hub. A signed-in session's first 401 expires it once; a new person or organization drops every
 * request and cached read the old one started, while a refreshed session of the same person keeps them.
 */
export const identityBridge = createAuthBridge<BffUser>({ getIdentity: BffSessionExtensions.identity });

const bff = createBffStrategy();
let sessionHost: AuthStrategyHost<BffUser> | undefined;
let reachable = true;

/**
 * Catalogue's Identity BFF session at `/api/identity` (`GET /session`, `GET /sign-in?returnUrl=`, `POST /sign-out`): the
 * SDK strategy as it ships, with tab sync and the refresh before `expiresAt`. It also notes whether the last session
 * read reached the API, and keeps the provider's silent re-read, which {@link revalidateSession} runs after a refusal.
 */
export const identityStrategy: BffStrategy = {
  ...bff,
  async resolveUser(context) {
    const outcome = await bff.resolveUser(context);
    if (outcome.ok) reachable = true;
    else if (outcome.failure.type !== 'cancelled') reachable = false;
    return outcome;
  },
  connect(host) {
    sessionHost = host;
    const disconnect = bff.connect?.(host);
    return () => {
      if (sessionHost === host) sessionHost = undefined;
      disconnect?.();
    };
  },
};

/** Whether the last session read reached the API; a visit it did not reach opens and offers sign-in instead. */
export function sessionReachable(): boolean {
  return reachable;
}

/** Re-reads the session without leaving it, so the permissions follow a refusal; a no-op before the app mounts. */
export async function revalidateSession(): Promise<void> {
  await sessionHost?.revalidate();
}

/**
 * Settles the session and answers whether it is signed in. A visit that opened on the published reader never read it,
 * so entering the operator frame from there reads it first.
 */
export async function ensureSession(): Promise<boolean> {
  if (identityBridge.getSession().status === AuthStatus.Unknown) void revalidateSession();
  return identityBridge.isAuthenticated();
}

/** Reads a refusal's problem `code` without consuming the response the transport decodes. */
async function problemCode(response: Response): Promise<unknown> {
  try {
    const body: unknown = await response.clone().json();
    return typeof body === 'object' && body !== null ? (body as { code?: unknown }).code : undefined;
  } catch {
    return undefined;
  }
}

/**
 * The browser transport under {@link catalogueHttp}: a 403, or a write refused for a stale CSRF token, re-reads the
 * session, so the controls follow the person's permissions and the next write carries the current token. The refused
 * write is never replayed. It reads the global `fetch` per call.
 */
const refusalAwareFetch: typeof fetch = async (input, init) => {
  const response = await globalThis.fetch(input, init);
  if (response.status === 403 || (response.status === 400 && (await problemCode(response)) === AntiforgeryFailedCode))
    void revalidateSession();
  return response;
};

/**
 * The transport every private Catalogue call rides: the SDK envelope, no automatic retry, the session's CSRF token on
 * each unsafe request, and the session's request scope, so a sign-out or an expiry cancels what the old session started.
 */
export const catalogueHttp: ApiClient = createApiClient({
  envelope: wowTwoEnvelope,
  retry: false,
  scope: identityBridge.scope,
  onUnauthorized: identityBridge.onUnauthorized,
  antiforgery: identityStrategy.antiforgery,
  fetch: refusalAwareFetch,
});
