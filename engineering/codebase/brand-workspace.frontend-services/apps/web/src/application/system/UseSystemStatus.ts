import { computed, ref } from 'vue';

import { ApiFailureFactory, type ApiFailure } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions, type Result } from '@wow-two-beta/fe/foundation/results';
import { useAppQuery } from '@wow-two-beta/fe/query';

import type { SystemStatus } from '@/domain/system/SystemStatus';
import { systemApi } from '@/integration/system/SystemApi';

/** Owns the live foundation query, with separate refresh and cached-data state. */
export function useSystemStatus() {
  const query = useAppQuery({ key: ['system', 'status'], queryFn: ({ signal }) => systemApi.getStatus(signal) });
  const refreshing = ref(false);

  /** The active request state, including explicit refreshes of cached status. */
  const busy = computed(() => query.loading.value || refreshing.value);
  /** The display-safe request failure; provider diagnostics never reach this message. */
  const failureMessage = computed(() => {
    switch (query.error.value?.code) {
      case 'transport': return 'The foundation API could not be reached. Start the service, then try again.';
      case 'timeout': return 'The status check timed out. Try again when the service is responding.';
      case 'protocol': return 'The service returned an unexpected status response. Check the API version.';
      case 'http': return 'The service could not return its status. Check its logs, then try again.';
      case 'cancelled': return null;
      case undefined: return null;
      default: return 'The status check could not be completed. Try again.';
    }
  });

  /** Refreshes the snapshot while preserving any previous successful observation. */
  async function refresh(): Promise<Result<SystemStatus, ApiFailure>> {
    refreshing.value = true;
    try {
      await query.refetch();
      if (query.error.value) return ResultExtensions.fail(query.error.value);
      return query.data.value
        ? ResultExtensions.ok(query.data.value)
        : ResultExtensions.fail(ApiFailureFactory.create('protocol'));
    } finally {
      refreshing.value = false;
    }
  }

  return { status: query.data, loading: query.loading, busy, failureMessage, refresh };
}
