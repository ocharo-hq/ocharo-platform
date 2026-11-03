import { computed, shallowRef } from 'vue';
import {
  canApprove as sessionCanApprove,
  canEdit as sessionCanEdit,
  identityClient,
  signInPath,
} from '../../integration/common/IdentitySessionClient';
import { useUiLocale } from '../shared/UseUiLocale';
import { notifyError } from './UseFeedback';

export type { IdentitySession } from '../../integration/common/IdentitySessionClient';

/** The query parameter the BFF adds to the return address when a sign-in callback failed. */
const SignInErrorParameter = 'signInError';

const session = shallowRef(identityClient.current);
identityClient.subscribe((value) => {
  session.value = value;
});
/** Edit and approval controls follow the session's `catalogue:edit` and `catalogue:approve` permissions. */
const canEdit = computed(() => sessionCanEdit(session.value));
const canApprove = computed(() => sessionCanApprove(session.value));
const signingOut = shallowRef(false);

/** Ends the Ocharo sign-in, then leaves for Identity's end-session page; a failure is reported and the tab stays. */
async function signOut(): Promise<void> {
  if (signingOut.value) return;
  signingOut.value = true;
  try {
    if (!(await identityClient.signOut()))
      notifyError(useUiLocale().t('Could not sign out. Try again.'), 'identity:sign-out');
  } finally {
    signingOut.value = false;
  }
}

/** The sign-in address that returns to `returnUrl`, a local path. */
export function signInUrl(returnUrl: string): string {
  return signInPath(returnUrl.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/');
}

/** Whether `search` carries the BFF's mark of a failed sign-in callback. */
export function hasSignInError(search: string): boolean {
  return new URLSearchParams(search).has(SignInErrorParameter);
}

/**
 * Reads the session at startup; a browser the API answered as signed out goes to sign in and returns here, unless the
 * last sign-in failed moments ago or the API did not answer, when the app opens and offers sign-in instead. Resolves
 * `false` when the browser is leaving for sign-in.
 */
export async function startSession(location: Pick<Location, 'pathname' | 'search' | 'replace'> = window.location) {
  const value = await identityClient.initialize();
  if (value.isAuthenticated || !value.reachable || hasSignInError(location.search)) return true;
  location.replace(signInUrl(location.pathname + location.search));
  return false;
}

export function useIdentitySession() {
  return { session, canEdit, canApprove, signingOut, signOut, reconnect: () => identityClient.initialize(true) };
}
