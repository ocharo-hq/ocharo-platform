import {
  BffSessionExtensions,
  createAuthBridge,
  createBffStrategy,
  type AuthStrategyHost,
  type BffStrategy,
  type BffUser,
} from '@wow-two-beta/fe/auth';
import { createApiClient, type ApiClient } from '@wow-two-beta/fe/foundation/http';

/** The permissions the Events API checks in the session's organization: reading, and proposing or tracking events. */
export const EventsPermission = { Read: 'events:read', Track: 'events:track' } as const;

/** The query a failed sign-in comes back with (`signInError=callback_failed`). */
export const SignInErrorParameter = 'signInError';

/**
 * The Events session hub. A signed-in session's first 401 expires it once; a new person or organization drops every
 * request and cached read the old one started, while a refreshed session of the same person keeps them.
 */
export const identityBridge = createAuthBridge<BffUser>({ getIdentity: BffSessionExtensions.identity });

const bff = createBffStrategy();
let sessionHost: AuthStrategyHost<BffUser> | undefined;

/**
 * Ocharo's Identity BFF session at `/api/identity` (`GET /session`, `GET /sign-in?returnUrl=`, `POST /sign-out`): the SDK
 * strategy as it ships, with tab sync and the refresh before `expiresAt`. It also keeps the provider's silent re-read,
 * which {@link revalidateSession} runs after a refusal.
 */
export const identityStrategy: BffStrategy = {
  ...bff,
  connect(host) {
    sessionHost = host;
    const disconnect = bff.connect?.(host);
    return () => {
      if (sessionHost === host) sessionHost = undefined;
      disconnect?.();
    };
  },
};

/** Re-reads the session without leaving it, so the permissions follow a refusal; a no-op before the app mounts. */
export async function revalidateSession(): Promise<void> {
  await sessionHost?.revalidate();
}

/**
 * The transport every private Events call rides: no automatic retry, the session's CSRF token on each unsafe request,
 * and the session's request scope, so a sign-out or an expiry cancels what the old session started. The events routes
 * answer in the SDK envelope (`{ data }`), as Desk's did, and the client unwraps it.
 */
export const eventsHttp: ApiClient = createApiClient({
  retry: false,
  scope: identityBridge.scope,
  onUnauthorized: identityBridge.onUnauthorized,
  antiforgery: identityStrategy.antiforgery,
});
