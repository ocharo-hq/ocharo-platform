import { computed, shallowRef } from 'vue';
import { ssoClient } from '../../integration/common/OcharoSsoClient';

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
export function useSsoSession() {
  return { session, canEdit, canApprove, reconnect: () => ssoClient.initialize(true) };
}
