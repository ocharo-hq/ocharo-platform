import { computed, shallowRef } from 'vue';
import { AuthStatus, BffSessionExtensions, useBffAuth } from '@wow-two-beta/fe/auth';
import type { ApiFailure } from '@wow-two-beta/fe/foundation/http';
import {
  BrandPermission,
  identityBridge,
  identityStrategy,
  OrganizationAccessRequired,
  revalidateSession,
  SignInErrorParameter,
} from '../../integration/common/IdentitySession';
import { useUiLocale } from '../shared/UseUiLocale';
import { notifyError } from './UseFeedback';

/** The person and organization whose organization Brand's API refused; another session reads as not refused. */
const deniedIdentity = shallowRef<string | null>(null);
const signingOut = shallowRef(false);

/**
 * Reads a refusal from Brand's API: a 403 re-reads the session, so the controls follow the person's permissions, and
 * one naming the organization (`organization_access_required`) marks this session's organization as refused.
 */
export async function noteRefusal(failure: ApiFailure): Promise<void> {
  if (failure.status !== 403) return;
  await revalidateSession();
  const { status, user } = identityBridge.getSession();
  if (failure.problem?.['brandCode'] === OrganizationAccessRequired && status === AuthStatus.Authenticated && user)
    deniedIdentity.value = BffSessionExtensions.identity(user);
}

/** The page's own address without a failed sign-in's marker. */
function currentPath(): string {
  const query = new URLSearchParams(globalThis.location.search);
  query.delete(SignInErrorParameter);
  const search = query.toString();
  return globalThis.location.pathname + (search ? `?${search}` : '');
}

/**
 * The Ocharo session as Brand's frame and pages read it, over the SDK BFF session the app root provides. Edit controls
 * follow `brand:edit`, publication `brand:approve`; the API checks both again.
 */
export function useIdentitySession() {
  const auth = useBffAuth();
  const { t } = useUiLocale();
  const isAuthenticated = computed(() => auth.isAuthenticated);
  const person = computed(() => auth.person);
  const organizationDenied = computed(
    () => auth.user !== null && deniedIdentity.value === BffSessionExtensions.identity(auth.user),
  );
  const can = (permission: string) => !organizationDenied.value && auth.hasPermission(permission);

  /** Ends the session and leaves through Identity's sign-out; a failure is reported and the session read again. */
  async function signOut(): Promise<void> {
    if (signingOut.value) return;
    signingOut.value = true;
    try {
      const outcome = await auth.signOut();
      if (!outcome.ok && outcome.failure.type !== 'cancelled') {
        notifyError(t('Could not sign out. Try again.'), 'identity:sign-out');
        await auth.refresh();
      }
    } finally {
      signingOut.value = false;
    }
  }

  return {
    isAuthenticated,
    person,
    organizationDenied,
    canEdit: computed(() => can(BrandPermission.Edit)),
    canApprove: computed(() => can(BrandPermission.Approve)),
    signingOut,
    signOut,
    /** The sign-in address that comes back to `returnUrl`, by default this page without a failed sign-in's marker. */
    signInHref: (returnUrl = currentPath()) => identityStrategy.signInUrl(returnUrl),
    /** Whether this page is where a failed sign-in came back to (`signInError`). */
    signInFailed: () => new URLSearchParams(globalThis.location.search).has(SignInErrorParameter),
    /** Re-reads the session, as after signing in again in another tab. */
    reconnect: () => auth.refresh(),
  };
}
