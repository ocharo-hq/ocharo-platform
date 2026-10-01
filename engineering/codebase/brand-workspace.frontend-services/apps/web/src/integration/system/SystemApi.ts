import { ApiFailureFactory, createApiClient, type ApiFailure } from '@wow-two-beta/ui-vue/foundation/http';
import { ResultExtensions, type Result } from '@wow-two-beta/ui-vue/foundation/results';

import type { SystemStatus } from '@/domain/system/SystemStatus';

/** @internal The shared SDK transport; this public foundation endpoint has no user credentials. */
const client = createApiClient({ credentials: 'omit', retry: false });

/** @internal Narrows an untrusted JSON object before reading its fields. */
function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/** @internal Validates the foundation wire contract and rejects unsupported or incoherent states. */
function decodeStatus(value: unknown): Result<SystemStatus, ApiFailure> {
  if (isRecord(value) && value.service === 'brand-workspace' && value.stage === 'foundation' &&
      value.persistence === 'PostgreSQL' && isRecord(value.worker)) {
    const { state, lastSeenAtUtc } = value.worker;
    const validTimestamp = typeof lastSeenAtUtc === 'string' &&
      /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|\+00:00)$/.test(lastSeenAtUtc) &&
      Number.isFinite(Date.parse(lastSeenAtUtc));
    if ((state === 'waiting' && lastSeenAtUtc === null) ||
        ((state === 'healthy' || state === 'stale') && validTimestamp)) {
      return ResultExtensions.ok({
        service: value.service,
        stage: value.stage,
        persistence: value.persistence,
        worker: { state, lastSeenAtUtc: lastSeenAtUtc as string | null },
      });
    }
  }
  return ResultExtensions.fail(ApiFailureFactory.create('protocol'));
}

/** Provides the read-only system endpoint. */
export const systemApi = {
  /** Reads GET /api/system/status with cancellation and a bounded request lifetime. */
  getStatus(signal: AbortSignal): Promise<Result<SystemStatus, ApiFailure>> {
    return client.get('/api/system/status', {
      signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]),
      unwrap: false,
      decode: decodeStatus,
    });
  },
};
