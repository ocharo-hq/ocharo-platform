import { computed, shallowRef } from 'vue';
import { AuthStatus, BffSessionExtensions, useBffAuth } from '@wow-two-beta/fe/auth';
import {
  CataloguePermission,
  identityBridge,
  identityStrategy,
  SignInErrorParameter,
} from '../../integration/common/IdentitySession';
import { useUiLocale } from '../shared/UseUiLocale';
import { notifyError } from './UseFeedback';

// The session as the bridge publishes it: the app root's provider settles it, every reader follows it.
const session = shallowRef(identityBridge.getSession());
identityBridge.subscribeSession((next) => {
  session.value = next;
});
const user = computed(() => (session.value.status === AuthStatus.Authenticated ? session.value.user : null));
/** Edit controls follow `catalogue:edit`; publication needs `catalogue:approve` beside it. The API checks both again. */
const canEdit = computed(() => BffSessionExtensions.hasPermission(user.value, CataloguePermission.Edit));
const canApprove = computed(
  () => canEdit.value && BffSessionExtensions.hasPermission(user.value, CataloguePermission.Approve),
);
const signingOut = shallowRef(false);

/**
 * The Ocharo session as Catalogue's frame and pages read it: who is signed in and what they may do. It reads the
 * session the app root's SDK `AuthProvider` resolves, through the bridge, so a page reads it the same way inside the
 * frame and on its own.
 */
export function useIdentitySession() {
  return {
    isAuthenticated: computed(() => user.value !== null),
    person: computed(() => user.value?.person ?? null),
    canEdit,
    canApprove,
    /** The sign-in address that comes back to `returnUrl`, a local path; anything else comes back to `/`. */
    signInHref: (returnUrl: string) => identityStrategy.signInUrl(returnUrl),
    /** Whether this page is where a failed sign-in came back to (`signInError`). */
    signInFailed: () => new URLSearchParams(globalThis.location.search).has(SignInErrorParameter),
  };
}

/** The session's commands, for components under the app root's `AuthProvider`: sign out and read the session again. */
export function useSessionActions() {
  const auth = useBffAuth();
  const { t } = useUiLocale();

  /** Ends the Ocharo sign-in, then leaves for Identity's end-session page; a failure is reported and the session read again. */
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
    signingOut,
    signOut,
    /** Re-reads the session, as after signing in again in another tab. */
    reconnect: () => auth.refresh(),
  };
}
