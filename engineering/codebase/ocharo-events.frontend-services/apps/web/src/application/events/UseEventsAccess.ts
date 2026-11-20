import { computed } from 'vue';
import { onBeforeRouteLeave } from 'vue-router';

import { useIdentitySession } from '@/application/common/UseIdentitySession';

/**
 * What the Events page needs of the session: whether it is busy signing out, which holds the page where it is. The
 * sign-in, the CSRF token and the session re-read after a refusal live in the `fe-events` client
 * (`createOcharoEventsClient`), so the page carries none of them. The page holds no form, so it has no unsaved edits to
 * guard.
 */
export function useEventsAccess() {
  const { signingOut } = useIdentitySession();
  const busy = computed(() => signingOut.value);

  /** Holds a router move while the session signs out. */
  onBeforeRouteLeave(() => !busy.value);
  return { busy };
}
export type EventsAccess = ReturnType<typeof useEventsAccess>;

/** The organization the events belong to and whether the person may propose or track in it. */
export interface EventsWorkspace {
  readonly organization: { readonly value: { readonly id: string; readonly name: string | null } | null };
  readonly canEdit: { readonly value: boolean };
}
