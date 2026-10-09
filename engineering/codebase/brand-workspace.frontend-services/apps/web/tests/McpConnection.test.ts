import { computed, defineComponent, ref } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createQueryClient, queryPlugin } from '@wow-two-beta/ui-vue/query';
import { DefaultRetryPolicy } from '@wow-two-beta/ui-vue/foundation/resilience';
import { ResultExtensions } from '@wow-two-beta/ui-vue/foundation/results';
import { ApiFailureFactory } from '@wow-two-beta/ui-vue/foundation/http';

import { useMcpConnection } from '@/application/workspace/UseMcpConnection';
import type { AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
import { isMcpCapabilities, isMcpTicket } from '@/integration/workspace/McpApi';

const api = vi.hoisted(() => ({ capabilities: vi.fn(), issue: vi.fn() }));
vi.mock('@/integration/workspace/McpApi', async importOriginal => ({
  ...await importOriginal<typeof import('@/integration/workspace/McpApi')>(), createMcpApi: () => api,
}));
const enabled = { enabled: true, endpoint: '/mcp', audience: 'ocharo-desk-mcp',
  tokenLifetimeSeconds: 300, readOnly: true, disabledReason: null };
let wrapper: VueWrapper | undefined;
let client: ReturnType<typeof createQueryClient>;
let connection: ReturnType<typeof useMcpConnection>;
const ticket = () => ({ token: 'private-ticket', expiresAt: new Date(Date.now() + 300_000).toISOString() });

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(new Date('2026-10-09T10:00:00Z'));
  api.capabilities.mockResolvedValue(ResultExtensions.ok(enabled));
  api.issue.mockResolvedValue(ResultExtensions.ok(ticket()));
});
afterEach(() => { wrapper?.unmount(); client?.clear(); vi.useRealTimers(); vi.clearAllMocks(); });

function render() {
  client = createQueryClient({ retry: { ...DefaultRetryPolicy, maxRetries: 0 } });
  const organization = ref({ id: 'org-a' });
  const session = ref({ sessionId: 'session-a', csrfToken: 'csrf-a' });
  const access = { session, busy: ref(false), recheck: vi.fn(),
    write: vi.fn((operation: (signal: AbortSignal) => unknown, signal: AbortSignal) => operation(signal)) };
  wrapper = mount(defineComponent({ setup() {
    connection = useMcpConnection(access as unknown as AccessControls,
      { organization, canEdit: computed(() => false) } as unknown as WorkspaceControls);
    return () => null;
  } }), { global: { plugins: [queryPlugin(client)] } });
  return { organization, session, access };
}

describe('short-lived MCP access', () => {
  it('never issues automatically, caches no credential, and clears it on expiry', async () => {
    const stored = vi.spyOn(Storage.prototype, 'setItem');
    render(); await flushPromises();
    expect(api.issue).not.toHaveBeenCalled();
    await connection.issue();
    expect(connection.ticket.value?.token).toBe('private-ticket');
    expect(stored).not.toHaveBeenCalled();
    expect(JSON.stringify(client.getQueryCache().getAll().map(query => query.state.data)))
      .not.toContain('private-ticket');
    expect(client.getMutationCache().getAll()).toHaveLength(0);
    stored.mockRestore();
    await vi.advanceTimersByTimeAsync(300_000);
    expect(connection.ticket.value).toBeNull();
  });
  it('ignores late credentials after an organization change', async () => {
    const { organization } = render(); await flushPromises();
    let finish!: (value: ReturnType<typeof ResultExtensions.ok<ReturnType<typeof ticket>>>) => void;
    api.issue.mockImplementation(() => new Promise(resolve => { finish = resolve; }));
    const pending = connection.issue();
    organization.value = { id: 'org-b' };
    finish(ResultExtensions.ok(ticket())); await pending;
    expect(connection.ticket.value).toBeNull();
  });
  it('clears a displayed credential when identity changes or the scope unmounts', async () => {
    const { session } = render(); await flushPromises(); await connection.issue();
    session.value = { sessionId: 'session-b', csrfToken: 'csrf-b' };
    expect(connection.ticket.value).toBeNull();
    await flushPromises(); await connection.issue();
    wrapper!.unmount(); wrapper = undefined;
    expect(connection.ticket.value).toBeNull();
  });
  it('blocks overlapping issuance and keeps a valid token when renewal fails', async () => {
    render(); await flushPromises(); await connection.issue();
    api.issue.mockResolvedValue(ResultExtensions.fail(ApiFailureFactory.create('transport')));
    const first = connection.issue();
    const second = connection.issue();
    await Promise.all([first, second]);
    expect(api.issue).toHaveBeenCalledTimes(2);
    expect(connection.ticket.value?.token).toBe('private-ticket');
  });
  it('refuses credentials when the server integration is disabled', async () => {
    api.capabilities.mockResolvedValue(ResultExtensions.ok({ ...enabled, enabled: false, disabledReason: 'disabled' }));
    render(); await flushPromises(); await connection.issue();
    expect(api.issue).not.toHaveBeenCalled();
  });
  it('hides credentials and cancels a pending renewal display', async () => {
    render(); await flushPromises(); await connection.issue();
    let finish!: (value: ReturnType<typeof ResultExtensions.ok<ReturnType<typeof ticket>>>) => void;
    api.issue.mockImplementation(() => new Promise(resolve => { finish = resolve; }));
    const pending = connection.issue();
    connection.hide();
    finish(ResultExtensions.ok(ticket())); await pending;
    expect(connection.ticket.value).toBeNull();
    expect(connection.issuing.value).toBe(false);
  });
  it('keeps explicit refresh pending until it settles and ignores overlapping clicks', async () => {
    render(); await flushPromises();
    let finish!: (value: ReturnType<typeof ResultExtensions.ok<typeof enabled>>) => void;
    api.capabilities.mockImplementation(() => new Promise(resolve => { finish = resolve; }));
    const pending = connection.refresh();
    await connection.refresh();
    expect(connection.refreshing.value).toBe(true);
    expect(api.capabilities).toHaveBeenCalledTimes(2);
    finish(ResultExtensions.ok(enabled)); await pending;
    expect(connection.refreshing.value).toBe(false);
  });
  it('rejects malformed authority and credentials', () => {
    expect(isMcpCapabilities(enabled)).toBe(true);
    expect(isMcpCapabilities({ ...enabled, endpoint: 'https://other.test/mcp' })).toBe(false);
    expect(isMcpCapabilities({ ...enabled, readOnly: false })).toBe(false);
    expect(isMcpTicket({ token: 'token\r\nother-header', expiresAt: ticket().expiresAt })).toBe(false);
    expect(isMcpTicket({ token: 'token', expiresAt: 'tomorrow' })).toBe(false);
  });
});
