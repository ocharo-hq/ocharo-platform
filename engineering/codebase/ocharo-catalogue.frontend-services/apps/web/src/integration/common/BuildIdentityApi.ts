import { ApiFailureFactory, createApiClient, type ApiFailure } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions, type Result } from '@wow-two-beta/fe/foundation/results';

/** How long the account menu waits for the API's version before calling it unavailable. */
export const BuildIdentityTimeoutMs = 5_000;

/** @internal The public status never carries the session or an access ticket. */
const client = createApiClient({ credentials: 'omit', retry: false });

/** @internal Accepts the status body only when it names a text `version`; anything else is a protocol failure. */
function decodeVersion(value: unknown): Result<string, ApiFailure> {
  return typeof value === 'object' && value !== null && 'version' in value && typeof value.version === 'string'
    ? ResultExtensions.ok(value.version)
    : ResultExtensions.fail(ApiFailureFactory.create('protocol'));
}

/** `GET /api/system/status`, read bare: the API's informational version, with cancellation and a bounded wait. */
export function readBuildIdentity(signal: AbortSignal): Promise<Result<string, ApiFailure>> {
  return client.get('/api/system/status', {
    signal: AbortSignal.any([signal, AbortSignal.timeout(BuildIdentityTimeoutMs)]),
    unwrap: false,
    decode: decodeVersion,
  });
}
