import { onBeforeUnmount, ref } from 'vue';

import { buildIdentityApi } from '@/integration/common/BuildIdentityApi';

/** Loads technical About data only on request and cancels when the view closes; a failed read shows `Unavailable`. */
export function useBuildIdentity() {
  const backendVersion = ref('Not checked');
  const controller = new AbortController();
  let generation = 0;
  onBeforeUnmount(() => controller.abort());
  async function refresh(): Promise<void> {
    const current = ++generation;
    backendVersion.value = 'Checking…';
    const result = await buildIdentityApi.getVersion(controller.signal);
    if (!controller.signal.aborted && current === generation) backendVersion.value = result.ok ? result.value : 'Unavailable';
  }
  return { backendVersion, refresh };
}
