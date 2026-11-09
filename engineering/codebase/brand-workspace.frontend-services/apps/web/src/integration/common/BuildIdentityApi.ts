import { ApiFailureFactory, createApiClient, type ApiFailure } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions, type Result } from '@wow-two-beta/fe/foundation/results';

/** How long the About panel waits for the API's version. @internal */
const BuildIdentityTimeoutMs = 5_000;

/** @internal The shared SDK transport; the public status never carries a session or access ticket. */
const client = createApiClient({ credentials: 'omit', retry: false });

/** @internal Reads the informational version off the status; any other shape is a protocol failure. */
function decodeVersion(value: unknown): Result<string, ApiFailure> {
  return typeof value === 'object' && value !== null && 'version' in value && typeof value.version === 'string'
    ? ResultExtensions.ok(value.version)
    : ResultExtensions.fail(ApiFailureFactory.create('protocol'));
}

/** Provides the API's build identity for the About panel. */
export const buildIdentityApi = {
  /** Reads GET /api/system/status for the API's informational version, with cancellation and a bounded lifetime. */
  getVersion(signal: AbortSignal): Promise<Result<string, ApiFailure>> {
    return client.get('/api/system/status', {
      signal: AbortSignal.any([signal, AbortSignal.timeout(BuildIdentityTimeoutMs)]),
      unwrap: false,
      decode: decodeVersion,
    });
  },
};
