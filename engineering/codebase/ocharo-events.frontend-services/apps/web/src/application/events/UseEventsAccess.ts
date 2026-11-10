import { computed, onScopeDispose, ref } from 'vue';
import { onBeforeRouteLeave } from 'vue-router';

import type { ApiFailure } from '@wow-two-beta/fe/foundation/http';
import type { Result } from '@wow-two-beta/fe/foundation/results';

import { noteRefusal, useIdentitySession } from '@/application/common/UseIdentitySession';
import { requestConfirmation } from '@/application/common/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';

/** Produces safe product messages without rendering server diagnostic text. */
export function failureMessage(error: ApiFailure): string {
  if (error.status === 409)
    return 'This record changed. Your edits are still here. Reload the latest version before trying again.';
  if (error.status === 400) return 'Check the entered values and try again.';
  if (error.status === 401) return 'Your session is no longer valid. Sign in again.';
  if (error.status === 403)
    return 'This action is not permitted, or your session changed. Reload to check your access.';
  if (error.status === 404) return 'This record is unavailable in this organization.';
  if (error.code === 'protocol') return 'The service returned an unexpected response. Please retry.';
  if (error.code === 'cancelled') return 'The request was cancelled. Your edits have been retained.';
  return 'The service could not complete this request. Your edits are still here; try again.';
}

/**
 * What the Events page needs of the session: whether a write is running, whether a form holds unsaved edits, and a
 * `write` that counts a request, cancels it with the page and re-reads the session after a refusal. The sign-in,
 * the session scope and the CSRF token live in the transport (`eventsHttp`), so the page carries none of them.
 */
export function useEventsAccess() {
  const { t } = useUiLocale();
  const { signingOut } = useIdentitySession();
  const pendingWrites = ref(0);
  const dirty = ref(false);
  const controller = new AbortController();
  const busy = computed(() => pendingWrites.value > 0 || signingOut.value);

  /** Counts writes across every active form; leaving the page cancels what is still running. */
  async function write<T>(operation: (signal: AbortSignal) => Promise<Result<T, ApiFailure>>, signal?: AbortSignal) {
    pendingWrites.value += 1;
    try {
      const result = await operation(AbortSignal.any([controller.signal, ...(signal ? [signal] : [])]));
      if (!result.ok) await noteRefusal(result.failure);
      return result;
    } finally {
      pendingWrites.value -= 1;
    }
  }
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
  /** Prevents closing the tab with unsaved edits or a write in flight. */
  function beforeUnload(event: BeforeUnloadEvent): void {
    if (dirty.value || pendingWrites.value) {
      event.preventDefault();
      event.returnValue = '';
    }
  }
  window.addEventListener('beforeunload', beforeUnload);
  onScopeDispose(() => {
    window.removeEventListener('beforeunload', beforeUnload);
    controller.abort();
  });
  return { busy, dirty, setDirty, write };
}
export type EventsAccess = ReturnType<typeof useEventsAccess>;

/** The organization the events belong to and whether the person may propose or track in it. */
export interface EventsWorkspace {
  readonly organization: { readonly value: { readonly id: string; readonly name: string | null } | null };
  readonly canEdit: { readonly value: boolean };
}
