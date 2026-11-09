import { onScopeDispose, ref, shallowRef } from 'vue';
import type { DesignSnapshot } from '@/domain/common/models/EditorModels';
import type { DesignPreflightReport } from '@/domain/design/DesignPreflight';
import { inspectDesign } from '@/integration/design/DesignPreflight';

export interface DesignPreflightReview {
  report: DesignPreflightReport; actionLabel: string; unsaved: boolean; inspectionOnly: boolean;
}
/** Warning approval belongs only to this exact snapshot and one requested export. */
export function useDesignPreflight() {
  const busy = ref(false), review = shallowRef<DesignPreflightReview | null>(null);
  const controller = new AbortController(); let decide: ((allowed: boolean) => void) | undefined;
  function resolve(allowed: boolean): void {
    if (allowed && review.value?.report.issues.some(issue => issue.severity === 'error')) return;
    const finish = decide; decide = undefined; review.value = null; finish?.(allowed);
  }
  onScopeDispose(() => { controller.abort(); resolve(false); });
  async function check(snapshot: DesignSnapshot, options: { pageIds?: readonly string[]; actionLabel: string; unsaved?: boolean; inspectionOnly?: boolean }): Promise<boolean> {
    if (busy.value) return false;
    busy.value = true;
    try {
      const inspected = await inspectDesign(snapshot, options.pageIds, controller.signal);
      controller.signal.throwIfAborted();
      // Callers report a check that could not run the way they report a failed export: with its reason.
      if (!inspected.ok) throw new Error(inspected.failure.message);
      const report = inspected.value;
      if (!report.issues.length && !options.unsaved && !options.inspectionOnly) return true;
      return await new Promise<boolean>(resolveDecision => {
        decide = resolveDecision;
        review.value = { report, actionLabel: options.actionLabel, unsaved: options.unsaved ?? false, inspectionOnly: options.inspectionOnly ?? false };
      });
    } finally { busy.value = false; }
  }
  return { busy, review, check, resolve };
}
