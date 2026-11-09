import {
  BffSessionExtensions,
  createAuthBridge,
  createBffStrategy,
  type AuthStrategyHost,
  type BffStrategy,
  type BffUser,
} from '@wow-two-beta/fe/auth';
import { createApiClient, wowTwoEnvelope, type ApiClient } from '@wow-two-beta/fe/foundation/http';

/** The permissions Brand's API checks in the session's organization. */
export const BrandPermission = { Edit: 'brand:edit', Approve: 'brand:approve' } as const;

/** The Brand refusal (`brandCode`) of a session whose organization is not the one Brand serves. */
export const OrganizationAccessRequired = 'organization_access_required';

/** The query a failed sign-in comes back with (`signInError=callback_failed`). */
export const SignInErrorParameter = 'signInError';

/**
 * Brand's session hub. A signed-in session's first 401 expires it once; a new person or organization drops every
 * request and cached read the old one started, while a refreshed session of the same person keeps them.
 */
export const identityBridge = createAuthBridge<BffUser>({ getIdentity: BffSessionExtensions.identity });

const bff = createBffStrategy();
let sessionHost: AuthStrategyHost<BffUser> | undefined;

/**
 * Brand's Identity BFF session at `/api/identity` (`GET /session`, `GET /sign-in?returnUrl=`, `POST /sign-out`): the SDK
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
 * The transport every private Brand call rides: the SDK envelope, no automatic retry, the session's CSRF token on each
 * unsafe request, and the session's request scope, so a sign-out or an expiry cancels what the old session started.
 */
export const brandHttp: ApiClient = createApiClient({
  envelope: wowTwoEnvelope,
  retry: false,
  scope: identityBridge.scope,
  onUnauthorized: identityBridge.onUnauthorized,
  antiforgery: identityStrategy.antiforgery,
});
