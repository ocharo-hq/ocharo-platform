import { computed, onScopeDispose, ref } from 'vue';
import { onBeforeRouteLeave } from 'vue-router';

import { useIdentitySession } from '@/application/common/UseIdentitySession';
import { requestConfirmation } from '@/application/common/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';

/**
 * What the Events page needs of the session: whether it is busy signing out, and whether the tracking form holds unsaved
 * edits, asking before a move or a closed tab discards them. The sign-in, the CSRF token and the session re-read after a
 * refusal live in the `fe-events` client (`createOcharoEventsClient`), so the page carries none of them.
 */
export function useEventsAccess() {
  const { t } = useUiLocale();
  const { signingOut } = useIdentitySession();
  const dirty = ref(false);
  const busy = computed(() => signingOut.value);

  function setDirty(value: boolean): void {
    dirty.value = value;
  }
  /** Asks before a router move discards unsaved edits; the page's own moves ask in `useEventsNavigation`. */
  onBeforeRouteLeave(async () => {
    if (busy.value) return false;
    if (
      dirty.value &&
      !(await requestConfirmation(t('Discard unsaved changes and leave this page?'), {
        confirmLabel: t('Discard changes'),
      }))
    )
      return false;
    dirty.value = false;
    return true;
  });
  /** Prevents closing the tab with unsaved edits. */
  function beforeUnload(event: BeforeUnloadEvent): void {
    if (dirty.value) {
      event.preventDefault();
      event.returnValue = '';
    }
  }
  window.addEventListener('beforeunload', beforeUnload);
  onScopeDispose(() => window.removeEventListener('beforeunload', beforeUnload));
  return { busy, dirty, setDirty };
}
export type EventsAccess = ReturnType<typeof useEventsAccess>;

/** The organization the events belong to and whether the person may propose or track in it. */
export interface EventsWorkspace {
  readonly organization: { readonly value: { readonly id: string; readonly name: string | null } | null };
  readonly canEdit: { readonly value: boolean };
}
