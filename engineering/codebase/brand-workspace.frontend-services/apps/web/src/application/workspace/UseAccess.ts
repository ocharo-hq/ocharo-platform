import { signInDestination } from '@/domain/workspace/SignInDestination';
import { requestConfirmation, resolveConfirmation, notifyError } from '@/application/shared/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { computed, inject, onScopeDispose, ref, shallowRef } from 'vue';
import { routerKey, type Router } from 'vue-router';

import { createAuthBridge, type AuthStrategy } from '@wow-two-beta/fe/auth';
import { createSyncChannel } from '@wow-two-beta/fe/foundation/channels';
import type { ApiFailure } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions, type Result } from '@wow-two-beta/fe/foundation/results';

import { readWorkspaceLocation, type Session } from '@/domain/workspace/Models';
import { safeWorkspaceDestination } from '@/domain/workspace/EditorNavigation';
import { createWorkspaceApi, type SessionTransport } from '@/integration/workspace/WorkspaceApi';

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
  /** Moves between sections of the same organization without reloading; absent, every move replaces the document. */
  router?: Router | null;
}

/** The runtime navigation: the document for context changes, the app router for moves inside one organization. */
function documentNavigation(): DocumentNavigation {
  return {
    assign: (path) => window.location.assign(path),
    reload: () => window.location.reload(),
    router: inject(routerKey, null),
  };
}

/** Owns the document's immutable session context and whole-document context transitions. */
export function useAccess(navigation: DocumentNavigation = documentNavigation()) {
  const { t } = useUiLocale();
  const session = shallowRef<Session | null>(null);
  const sessionError = ref<string | null>(null);
  const checking = ref(false);
  const invalidated = ref(false);
  const pendingWrites = ref(0);
  const dirty = ref(false);
  const channel = createSyncChannel<string>('brand-workspace.auth');
  const controller = new AbortController();
  let disposed = false;
  /* The SDK session bridge: a signed-in session's first current 401 expires it once, cancels that session's requests,
     then replaces the document. Sign-in renders in place, so the reloaded address is the return location. */
  const bridge = createAuthBridge<Session>({ getIdentity: (user) => user.sessionId ?? user.user?.id });
  const unsubscribeExpired = bridge.subscribeExpired(() => invalidate());
  const transport: SessionTransport = { scope: bridge.scope, onUnauthorized: bridge.onUnauthorized };
  const api = createWorkspaceApi(() => session.value?.csrfToken ?? '', transport);
  const busy = computed(
    () => pendingWrites.value > 0 || checking.value || invalidated.value || sessionError.value !== null,
  );
  const strategy: AuthStrategy<Session> = {
    async resolveUser({ signal }) {
      sessionError.value = null;
      const result = await api.getSession(signal);
      if (!result.ok) {
        sessionError.value = failureMessage(result.failure);
        if (result.failure.code !== 'cancelled') notifyError(sessionError.value);
        return result;
      }
      session.value = result.value;
      return ResultExtensions.ok(result.value.authenticated ? result.value : null);
    },
  };

  /** Invalidates visible private state before replacing the document. */
  function invalidate(): void {
    resolveConfirmation(false);
    invalidated.value = true;
    dirty.value = false;
    controller.abort();
    navigation.reload();
  }
  /** The organization this document loaded; its reads and caches stay bound to it until the document is replaced. */
  const documentOrganizationId = readWorkspaceLocation(window.location.pathname).organizationId?.toLowerCase() ?? null;
  /** Whether a path stays in this document: the same organization, or the organization-free diagnostics. */
  function staysInDocument(path: string): boolean {
    const target = readWorkspaceLocation(new URL(path, window.location.origin).pathname);
    return target.section === 'system' || (target.organizationId?.toLowerCase() ?? null) === documentOrganizationId;
  }
  /** Prevents accidental loss of edits; moves inside the organization in place and replaces the document otherwise. */
  async function navigate(path: string): Promise<void> {
    if (busy.value || !path.startsWith('/') || path.startsWith('//')) return;
    if (dirty.value && !await requestConfirmation(t('Discard unsaved changes and leave this page?'), {confirmLabel:t('Discard changes')})) return;
    if (busy.value || disposed) return;
    dirty.value = false;
    if (navigation.router && staysInDocument(path)) await navigation.router.push(path);
    else navigation.assign(path);
  }
  /* Back, forward and in-app links pass the same checks: pending writes hold the page, unsaved edits ask first, and a
     move to another organization replaces the document so no private state crosses it. */
  const removeGuard = navigation.router?.beforeEach(async (to, from) => {
    if (!from.matched.length || to.fullPath === from.fullPath) return true;
    if (busy.value || disposed) return false;
    if (dirty.value && !await requestConfirmation(t('Discard unsaved changes and leave this page?'), {confirmLabel:t('Discard changes')})) return false;
    dirty.value = false;
    if (staysInDocument(to.fullPath)) return true;
    navigation.assign(to.fullPath);
    return false;
  });
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
  function authenticated(destination = '/'): void {
    dirty.value = false;
    channel.post('changed');
    navigation.assign(signInDestination(new URL(destination, window.location.origin).search, safeWorkspaceDestination(destination)));
  }
  /** Exchanges local operator credentials using the anonymous document's antiforgery token. */
  function login(input: { email: string; password: string }, signal: AbortSignal) {
    return write((requestSignal) => api.login(input, requestSignal), signal);
  }
  /** Confirms the server logout before replacing the current document. */
  async function logout(): Promise<void> {
    if (busy.value || (dirty.value && !await requestConfirmation(t('Discard unsaved changes and sign out?'), {confirmLabel:t('Discard changes')}))) return;
    if (busy.value || disposed) return;
    const result = await write((signal) => api.logout(signal));
    if (result.ok) authenticated();
    else { sessionError.value = failureMessage(result.failure); notifyError(sessionError.value); }
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
      if (result.failure.code !== 'cancelled') notifyError(sessionError.value);
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
    unsubscribeExpired();
    removeGuard?.();
    resolveConfirmation(false);
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
    bridge,
    transport,
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
