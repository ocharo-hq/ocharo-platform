import { createApiClient, wowTwoEnvelope, type ApiClient } from '@wow-two-beta/fe/foundation/http';

/** The private API's transport over a mocked `fetch`: the SDK envelope and no retry, as the session's client sends. */
export function httpOver(fetch: typeof globalThis.fetch): ApiClient {
  return createApiClient({ envelope: wowTwoEnvelope, retry: false, fetch });
}
