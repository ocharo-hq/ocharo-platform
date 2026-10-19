import { computed, onScopeDispose, readonly, ref, shallowRef, watch } from 'vue';
import { useAppQuery } from '@wow-two-beta/fe/query';

import { notifyError, useErrorToast } from '@/application/shared/UseFeedback';
import { createMcpApi, type McpTicket } from '@/integration/workspace/McpApi';
import { failureMessage, type AccessControls } from './UseAccess';
import type { WorkspaceControls } from './UseWorkspace';

/** Owns a short assistant session; expiry and identity changes clear the credential from memory. */
export function useMcpConnection(access: AccessControls, workspace: WorkspaceControls) {
  const api = createMcpApi(() => access.session.value?.csrfToken ?? '', access.transport);
  const organizationId = () => workspace.organization.value?.id ?? '';
  const identity = () => `${access.session.value?.sessionId ?? ''}:${organizationId()}`;
  const query = useAppQuery({
    key: () => [access.session.value?.sessionId, organizationId(), 'mcp-capabilities'],
    enabled: () => Boolean(organizationId() && access.session.value?.sessionId),
    queryFn: ({ signal }) => api.capabilities(organizationId(), signal),
  });
  const error = computed(() => query.error.value ? failureMessage(query.error.value) : null);
  useErrorToast(error);
  const ticket = shallowRef<McpTicket | null>(null);
  const issuing = ref(false);
  const refreshing = ref(false);
  let controller = new AbortController();
  let expiry: ReturnType<typeof setTimeout> | undefined;
  let generation = 0;

  /** Clears display state without claiming revocation of an already copied credential. */
  function clearTicket(): void {
    if (expiry !== undefined) clearTimeout(expiry);
    expiry = undefined;
    ticket.value = null;
  }
  /** Cancels old-context requests before another session can display their result. */
  function reset(): void {
    generation += 1;
    controller.abort();
    controller = new AbortController();
    issuing.value = false;
    clearTicket();
  }
  watch(identity, reset, { flush: 'sync' });
  onScopeDispose(reset);

  /** Issues access only after explicit activation and displays only a current confirmed response. */
  async function issue(): Promise<void> {
    if (issuing.value || access.busy.value || !query.data.value?.enabled || !organizationId()) return;
    const requestGeneration = generation;
    const context = identity();
    issuing.value = true;
    try {
      const result = await access.write(signal => api.issue(organizationId(), signal), controller.signal);
      if (generation !== requestGeneration || identity() !== context) return;
      if (!result.ok) { notifyError(failureMessage(result.failure)); return; }
      const remaining = Date.parse(result.value.expiresAt) - Date.now();
      if (remaining <= 0 || remaining > 305_000) {
        notifyError('The service returned an unexpected response. Please retry.');
        return;
      }
      clearTicket();
      ticket.value = result.value;
      expiry = setTimeout(clearTicket, remaining);
    } finally {
      if (generation === requestGeneration) issuing.value = false;
    }
  }
  /** Gives an explicitly requested refresh its own pending state without overlapping requests. */
  async function refresh(): Promise<void> {
    if (refreshing.value || query.loading.value) return;
    refreshing.value = true;
    try { await query.refetch(); }
    finally { refreshing.value = false; }
  }
  return { capabilities: query.data, loading: query.loading, refreshing: readonly(refreshing), refresh,
    ticket: readonly(ticket), issuing: readonly(issuing), issue, hide: reset };
}
