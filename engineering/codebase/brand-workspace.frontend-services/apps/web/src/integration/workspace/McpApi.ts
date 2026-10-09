import { createApiClient, ApiFailureFactory, type ApiDecoder } from '@wow-two-beta/ui-vue/foundation/http';
import { ResultExtensions } from '@wow-two-beta/ui-vue/foundation/results';

/** Describes the server's explicitly enabled, read-only assistant surface. */
export interface McpCapabilities {
  enabled: boolean;
  endpoint: '/mcp';
  audience: 'ocharo-desk-mcp';
  tokenLifetimeSeconds: 300;
  readOnly: true;
  disabledReason: null | 'disabled' | 'signing-key-missing';
}

/** Carries one short-lived credential; it must never enter query caches or browser storage. */
export interface McpTicket { token: string; expiresAt: string }

/** @internal Checks an untrusted payload without accepting arrays as records. */
const record = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);

/** Rejects unknown endpoints, audiences and authority rather than copying them into client configuration. */
export function isMcpCapabilities(value: unknown): value is McpCapabilities {
  return record(value) && typeof value.enabled === 'boolean' && value.endpoint === '/mcp'
    && value.audience === 'ocharo-desk-mcp' && value.tokenLifetimeSeconds === 300 && value.readOnly === true
    && (value.enabled ? value.disabledReason === null
      : value.disabledReason === 'disabled' || value.disabledReason === 'signing-key-missing');
}

/** Rejects malformed credentials and dates at the transport boundary. */
export function isMcpTicket(value: unknown): value is McpTicket {
  return record(value) && typeof value.token === 'string' && value.token.length > 0 && value.token.length <= 8192
    && !/[\r\n]/.test(value.token) && typeof value.expiresAt === 'string'
    && Number.isFinite(Date.parse(value.expiresAt));
}

/** @internal Converts payload validation failures into the shared protocol failure. */
function decode<T>(guard: (value: unknown) => value is T): ApiDecoder<T> {
  return value => guard(value) ? ResultExtensions.ok(value)
    : ResultExtensions.fail(ApiFailureFactory.create('protocol'));
}

/** Creates same-origin requests; delegated credentials are issued only through the current CSRF session. */
export function createMcpApi(csrf: () => string, unauthorized: () => void) {
  const client = createApiClient({ credentials: 'same-origin', onUnauthorized: unauthorized, retry: false });
  const base = (organization: string) => `/api/organizations/${encodeURIComponent(organization)}`;
  return {
    /** GET /api/organizations/{organization}/integrations/mcp. */
    capabilities: (organization: string, signal: AbortSignal) => client.get(`${base(organization)}/integrations/mcp`,
      { signal, decode: decode(isMcpCapabilities) }),
    /** POST /api/organizations/{organization}/integration-tickets. */
    issue: (organization: string, signal: AbortSignal) => client.post(`${base(organization)}/integration-tickets`,
      { signal, headers: { 'X-CSRF-TOKEN': csrf() }, body: { audience: 'ocharo-desk-mcp' },
        decode: decode(isMcpTicket) }),
  };
}
