import { computed, shallowRef } from 'vue';
import { ssoClient } from '../../integration/common/OcharoSsoClient';
import { useUiLocale } from '../shared/UseUiLocale';
import { notifyError } from './UseFeedback';

const session = shallowRef(ssoClient.current);
ssoClient.subscribe((value) => {
  session.value = value;
});
const canEdit = computed(
  () => !session.value.enabled || (session.value.authenticated && session.value.canEdit === true),
);
const canApprove = computed(
  () => !session.value.enabled || (session.value.authenticated && session.value.canApprove === true),
);
const signingOut = shallowRef(false);
/** Ends this app's Ocharo grant; a failure is reported and the session stays as it was. */
async function signOut(): Promise<void> {
  if (signingOut.value) return;
  signingOut.value = true;
  try {
    if (!(await ssoClient.signOut())) notifyError(useUiLocale().t('Could not sign out. Try again.'), 'sso:sign-out');
  } finally {
    signingOut.value = false;
  }
}
export function useSsoSession() {
  return { session, canEdit, canApprove, signingOut, signOut, reconnect: () => ssoClient.initialize(true) };
}
