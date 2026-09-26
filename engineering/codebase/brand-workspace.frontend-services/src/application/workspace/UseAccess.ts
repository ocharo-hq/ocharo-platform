import { computed, onScopeDispose, ref, shallowRef } from 'vue';

import type { AuthStrategy } from '@wow-two-beta/ui-vue/auth';
import { createSyncChannel } from '@wow-two-beta/ui-vue/foundation/channels';
import type { ApiFailure } from '@wow-two-beta/ui-vue/foundation/http';
import { ResultExtensions, type Result } from '@wow-two-beta/ui-vue/foundation/results';

import type { Session } from '@/domain/workspace/Models';
import { createWorkspaceApi } from '@/integration/workspace/WorkspaceApi';

/** Produces safe product messages without rendering server diagnostic text. */
export function failureMessage(error: ApiFailure): string {
  if (error.problem?.code === 'https_required')
    return 'Sign-in requires HTTPS. Open the secure application URL, then retry.';
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

/** Defines the document navigation seam used by the runtime and isolated lifecycle tests. */
export interface DocumentNavigation {
  assign(path: string): void;
  reload(): void;
}

/** Owns the document's immutable session context and whole-document context transitions. */
export function useAccess(
  navigation: DocumentNavigation = {
    assign: (path) => window.location.assign(path),
    reload: () => window.location.reload(),
  },
) {
  const session = shallowRef<Session | null>(null);
  const sessionError = ref<string | null>(null);
  const checking = ref(false);
  const invalidated = ref(false);
  const pendingWrites = ref(0);
  const dirty = ref(false);
  const channel = createSyncChannel<string>('brand-workspace.auth');
  const controller = new AbortController();
  let disposed = false;
  const api = createWorkspaceApi(
    () => session.value?.csrfToken ?? '',
    () => {
      if (session.value?.authenticated) invalidate();
    },
  );
  const busy = computed(
    () => pendingWrites.value > 0 || checking.value || invalidated.value || sessionError.value !== null,
  );
  const strategy: AuthStrategy<Session> = {
    async resolveUser({ signal }) {
      sessionError.value = null;
      const result = await api.getSession(signal);
      if (!result.ok) {
        sessionError.value = failureMessage(result.failure);
        return result;
      }
      session.value = result.value;
      return ResultExtensions.ok(result.value.authenticated ? result.value : null);
    },
  };

  /** Invalidates visible private state before replacing the document. */
  function invalidate(): void {
    invalidated.value = true;
    dirty.value = false;
    controller.abort();
    navigation.reload();
  }
  /** Prevents accidental loss of edits and switches context only with no pending writes. */
  function navigate(path: string): void {
    if (busy.value || !path.startsWith('/') || path.startsWith('//')) return;
    if (dirty.value && !window.confirm('Discard unsaved changes and leave this page?')) return;
    dirty.value = false;
    navigation.assign(path);
  }
  /** Counts writes across all active forms; session cancellation reaches the HTTP transport. */
  async function write<T>(operation: (signal: AbortSignal) => Promise<Result<T, ApiFailure>>, signal?: AbortSignal) {
    pendingWrites.value += 1;
    try {
      const result = await operation(AbortSignal.any([controller.signal, ...(signal ? [signal] : [])]));
      if (!result.ok && result.failure.problem?.code === 'csrf_validation_failed') invalidate();
      return result;
    } finally {
      pendingWrites.value -= 1;
    }
  }
  /** Announces cookie changes without sharing tokens or user data between tabs. */
  function authenticated(): void {
    dirty.value = false;
    channel.post('changed');
    navigation.assign('/');
  }
  /** Exchanges local operator credentials using the anonymous document's antiforgery token. */
  function login(input: { email: string; password: string }, signal: AbortSignal) {
    return write((requestSignal) => api.login(input, requestSignal), signal);
  }
  /** Confirms the server logout before replacing the current document. */
  async function logout(): Promise<void> {
    if (busy.value || (dirty.value && !window.confirm('Discard unsaved changes and sign out?'))) return;
    const result = await write((signal) => api.logout(signal));
    if (result.ok) authenticated();
    else sessionError.value = failureMessage(result.failure);
  }
  /** Revalidates cookies after tab resumption while preventing writes with unchecked authority. */
  async function recheck(): Promise<void> {
    if (checking.value || invalidated.value || !session.value) return;
    checking.value = true;
    const result = await api.getSession(controller.signal);
    if (disposed) return;
    checking.value = false;
    if (!result.ok) {
      sessionError.value = 'Session verification is unavailable. Retry before continuing.';
      return;
    }
    if (
      result.value.sessionId !== session.value.sessionId ||
      result.value.authenticated !== session.value.authenticated
    )
      invalidate();
    else {
      session.value = result.value;
      sessionError.value = null;
    }
  }
  function beforeUnload(event: BeforeUnloadEvent): void {
    if (dirty.value || pendingWrites.value) {
      event.preventDefault();
      event.returnValue = '';
    }
  }
  function pageshow(event: PageTransitionEvent): void {
    if (event.persisted) invalidate();
  }
  function visibility(): void {
    if (document.visibilityState === 'visible') void recheck();
  }
  const unsubscribe = channel.subscribe((message) => {
    if (message === 'changed') invalidate();
  });
  window.addEventListener('beforeunload', beforeUnload);
  window.addEventListener('pageshow', pageshow);
  document.addEventListener('visibilitychange', visibility);
  window.addEventListener('focus', recheck);
  onScopeDispose(() => {
    disposed = true;
    controller.abort();
    unsubscribe();
    channel.close();
    window.removeEventListener('beforeunload', beforeUnload);
    window.removeEventListener('pageshow', pageshow);
    document.removeEventListener('visibilitychange', visibility);
    window.removeEventListener('focus', recheck);
  });
  function setDirty(value: boolean): void {
    dirty.value = value;
  }
  return {
    session,
    sessionError,
    checking,
    invalidated,
    busy,
    pendingWrites,
    dirty,
    strategy,
    api,
    navigate,
    write,
    login,
    authenticated,
    logout,
    recheck,
    setDirty,
  };
}
export type AccessControls = ReturnType<typeof useAccess>;
