import { computed, shallowRef } from 'vue';
import { useBffAuth } from '@wow-two-beta/fe/auth';
import type { ApiFailure } from '@wow-two-beta/fe/foundation/http';
import {
  EventsPermission,
  identityStrategy,
  revalidateSession,
  SignInErrorParameter,
} from '../../integration/common/IdentitySession';
import { useUiLocale } from '../shared/UseUiLocale';
import { notifyError } from './UseFeedback';

const signingOut = shallowRef(false);

/** Reads a refusal from the Events API: a 403 re-reads the session, so the controls follow the person's permissions. */
export async function noteRefusal(failure: ApiFailure): Promise<void> {
  if (failure.status !== 403) return;
  await revalidateSession();
}

/** The page's own address without a failed sign-in's marker. */
function currentPath(): string {
  const query = new URLSearchParams(globalThis.location.search);
  query.delete(SignInErrorParameter);
  const search = query.toString();
  return globalThis.location.pathname + (search ? `?${search}` : '');
}

/**
 * The Ocharo session as Events' frame and pages read it, over the SDK BFF session the app root provides. Reading follows
 * `events:read` and proposing or tracking `events:track`; the API checks both again.
 */
export function useIdentitySession() {
  const auth = useBffAuth();
  const { t } = useUiLocale();
  const isAuthenticated = computed(() => auth.isAuthenticated);
  const person = computed(() => auth.person);
  const can = (permission: string) => auth.hasPermission(permission);
  /** A signed-in person whose organization grants no `events:read`: the API answers 404 to every route. */
  const organizationDenied = computed(() => auth.user !== null && !can(EventsPermission.Read));

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
    /** The session's organization; `null` for a person acting in none. */
    organization: computed(() => auth.organization),
    canRead: computed(() => can(EventsPermission.Read)),
    canEdit: computed(() => can(EventsPermission.Track)),
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
