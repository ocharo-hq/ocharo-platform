import { computed, shallowRef } from 'vue';
import {
  BrandPermission,
  hasPermission,
  identitySession,
  signInUrl,
} from '../../integration/common/IdentitySessionClient';
import { useUiLocale } from '../shared/UseUiLocale';
import { notifyError } from './UseFeedback';

const session = shallowRef(identitySession.current);
identitySession.subscribe((value) => {
  session.value = value;
});
/** Edit controls follow `brand:edit`, publication `brand:approve`; the API checks both again. */
const canEdit = computed(() => hasPermission(session.value, BrandPermission.Edit));
const canApprove = computed(() => hasPermission(session.value, BrandPermission.Approve));
const signingOut = shallowRef(false);
/** Ends the Brand session and leaves through Identity's sign-out; a failure is reported and the session stays. */
async function signOut(): Promise<void> {
  if (signingOut.value) return;
  signingOut.value = true;
  try {
    if (!(await identitySession.signOut()))
      notifyError(useUiLocale().t('Could not sign out. Try again.'), 'identity:sign-out');
  } finally {
    signingOut.value = false;
  }
}
/** The sign-in address that comes back to `returnUrl`, by default this page without a failed sign-in's marker. */
function signInHref(returnUrl = currentPath()): string {
  return signInUrl(returnUrl);
}
function currentPath(): string {
  const query = new URLSearchParams(globalThis.location.search);
  query.delete('signInError');
  const search = query.toString();
  return globalThis.location.pathname + (search ? `?${search}` : '');
}
/** Whether this page is where a failed sign-in came back to (`signInError`). */
function signInFailed(): boolean {
  return new URLSearchParams(globalThis.location.search).has('signInError');
}
export function useIdentitySession() {
  return {
    session,
    canEdit,
    canApprove,
    signingOut,
    signOut,
    signInHref,
    signInFailed,
    reconnect: () => identitySession.initialize(true),
  };
}
