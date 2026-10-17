import { computed, onScopeDispose, reactive, ref, shallowRef, watch } from 'vue';

import type { ApiFailure } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions, type Result } from '@wow-two-beta/fe/foundation/results';
import { useAppInfiniteQuery, useAppQuery, useQueryCache } from '@wow-two-beta/fe/query';

import { notifyError, requestConfirmation } from '@/application/shared/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { readWorkspaceLocation } from '@/domain/workspace/Models';
import {
  sourcingLimits, sourcingPath,
  type MediaFilter, type MediaUpdateInput, type PersonInput, type RouteInput, type SourcingLocation, type SourcingMedia,
  type SupplierFilter, type SupplierInput, type SupplierSummary,
} from '@/domain/workspace/Sourcing';
import { createSourcingApi, type UploadOptions } from '@/integration/workspace/SourcingApi';

import { failureMessage, type AccessControls } from './UseAccess';
import type { WorkspaceControls } from './UseWorkspace';

/** The result of a form save as the form needs it: conflicts keep the form's input. */
export type SaveOutcome = 'saved' | 'conflict' | 'failed';
/** Maps a write result for a form: conflicts stay inline, other failures become a safe toast. */
export function saveOutcome(result: Result<unknown, ApiFailure>): SaveOutcome {
  if (result.ok) return 'saved';
  if (result.failure.status === 409) return 'conflict';
  if (result.failure.code !== 'cancelled') notifyError(failureMessage(result.failure));
  return 'failed';
}

/** Owns Sourcing transport, cache keys and server-confirmed commands for one organization document. */
export function useSourcing(access: AccessControls, workspace: WorkspaceControls) {
  const api = createSourcingApi(() => access.session.value?.csrfToken ?? '', () => access.recheck());
  const cache = useQueryCache();
  const organizationId = () => workspace.organization.value?.id ?? '';
  // Shares the workspace scope prefix, so the shell's Refresh also reloads Sourcing.
  const scope = () => [access.session.value?.sessionId, organizationId(), 'sourcing'];
  const keys = {
    suppliers: () => [...scope(), 'suppliers'],
    supplierList: (filter: SupplierFilter) => [...scope(), 'suppliers', filter.query.trim(), filter.stage],
    supplier: (id: string) => [...scope(), 'supplier', id],
    media: () => [...scope(), 'media'],
    mediaList: (filter: MediaFilter) => [...scope(), 'media', filter.kind, filter.kind === 'supplier' ? filter.supplierId : null],
    summary: () => [...scope(), 'summary'],
  };
  const enabled = () => Boolean(workspace.organization.value);
  const canEdit = computed(() => workspace.canEdit.value);
  const refreshing = ref(false);

  // Several Sourcing forms can be open at once; the document is dirty while any of them is.
  const dirtyForms = reactive(new Set<string>());
  function setFormDirty(key: string, dirty: boolean): void {
    if (dirty) dirtyForms.add(key); else dirtyForms.delete(key);
  }
  watch(() => dirtyForms.size > 0, dirty => access.setDirty(dirty), { flush: 'sync' });
  onScopeDispose(() => { if (dirtyForms.size) access.setDirty(false); });

  async function refreshAfterWrite(targets: unknown[][]): Promise<void> {
    refreshing.value = true;
    try { await Promise.all(targets.map(key => cache.invalidate(key))); }
    catch { notifyError('Saved successfully. The current view could not refresh. Your local edits are preserved.', 'sourcing-refresh'); }
    finally { refreshing.value = false; }
  }
  async function run<T>(operation: (signal: AbortSignal) => Promise<Result<T, ApiFailure>>, signal: AbortSignal | undefined, after: () => unknown[][]) {
    const result = await access.write(operation, signal);
    if (result.ok) await refreshAfterWrite(after());
    return result;
  }
  const o = organizationId;
  const supplierViews = (id: string) => () => [keys.suppliers(), keys.supplier(id)];
  const mediaViews = () => [keys.media(), keys.summary(), keys.suppliers()];

  return {
    api, keys, enabled, canEdit, refreshing, organizationId, setFormDirty, refreshAfterWrite,
    contentUrl: (mediaId: string) => api.contentUrl(o(), mediaId),
    thumbnailUrl: (mediaId: string) => api.thumbnailUrl(o(), mediaId),
    refreshMediaViews: () => refreshAfterWrite(mediaViews()),
    refreshSupplier: (id: string) => cache.invalidate(keys.supplier(id)),
    createSupplier: (input: SupplierInput, signal?: AbortSignal) =>
      run(s => api.createSupplier(o(), input, s), signal, () => [keys.suppliers()]),
    updateSupplier: (id: string, expectedRevision: number, input: SupplierInput, signal?: AbortSignal) =>
      run(s => api.updateSupplier(o(), id, expectedRevision, input, s), signal, supplierViews(id)),
    createPerson: (supplierId: string, input: PersonInput, signal?: AbortSignal) =>
      run(s => api.createPerson(o(), supplierId, input, s), signal, supplierViews(supplierId)),
    updatePerson: (supplierId: string, personId: string, expectedRevision: number, input: PersonInput, signal?: AbortSignal) =>
      run(s => api.updatePerson(o(), personId, expectedRevision, input, s), signal, supplierViews(supplierId)),
    deletePerson: (supplierId: string, personId: string, expectedRevision: number, signal?: AbortSignal) =>
      run(s => api.deletePerson(o(), personId, expectedRevision, s), signal, supplierViews(supplierId)),
    createRoute: (supplierId: string, input: RouteInput, signal?: AbortSignal) =>
      run(s => api.createRoute(o(), supplierId, input, s), signal, supplierViews(supplierId)),
    updateRoute: (supplierId: string, routeId: string, expectedRevision: number, input: RouteInput, signal?: AbortSignal) =>
      run(s => api.updateRoute(o(), routeId, expectedRevision, input, s), signal, supplierViews(supplierId)),
    deleteRoute: (supplierId: string, routeId: string, expectedRevision: number, signal?: AbortSignal) =>
      run(s => api.deleteRoute(o(), routeId, expectedRevision, s), signal, supplierViews(supplierId)),
    /** Uploads one file; callers refresh views once per batch. */
    uploadMedia: (file: File, options: UploadOptions, signal?: AbortSignal) =>
      access.write(s => api.uploadMedia(o(), file, options, s), signal),
    updateMedia: (media: SourcingMedia, input: MediaUpdateInput, signal?: AbortSignal) =>
      run(s => api.updateMedia(o(), media.id, media.revision, input, s), signal, mediaViews),
    deleteMedia: (media: SourcingMedia, signal?: AbortSignal) =>
      run(s => api.deleteMedia(o(), media.id, media.revision, s), signal, mediaViews),
    /** Assigns in contract-sized batches; `null` returns the media to the inbox. */
    async assignMedia(mediaIds: readonly string[], supplierId: string | null, signal?: AbortSignal): Promise<Result<{ updated: number }, ApiFailure>> {
      let updated = 0;
      try {
        for (let index = 0; index < mediaIds.length; index += sourcingLimits.assignBatch) {
          const batch = mediaIds.slice(index, index + sourcingLimits.assignBatch);
          const result = await access.write(s => api.assignMedia(o(), batch, supplierId, s), signal);
          if (!result.ok) return result;
          updated += result.value.updated;
        }
        return ResultExtensions.ok({ updated });
      } finally { await refreshAfterWrite(mediaViews()); }
    },
  };
}
export type SourcingControls = ReturnType<typeof useSourcing>;

/** Offset boundaries can move between reads; display each ID once without changing raw page offsets. */
function uniqueItems<T extends { id: string }>(items: readonly T[]): T[] {
  return [...new Map(items.map(item => [item.id, item] as const)).values()];
}

/** Pages suppliers for a filter, keeping the last successful rows visible through refresh failures. */
export function useSupplierList(sourcing: SourcingControls, filter: () => SupplierFilter) {
  const pageSize = sourcingLimits.supplierPage;
  const query = useAppInfiniteQuery<SupplierSummary, SupplierSummary[]>({
    key: () => sourcing.keys.supplierList(filter()),
    enabled: sourcing.enabled,
    initialPageParam: 0,
    queryFn: ({ pageParam, signal }) => sourcing.api.listSuppliers(sourcing.organizationId(),
      { ...filter(), limit: pageSize, offset: Number(pageParam) || 0 }, signal),
    getNextPageParam: (last, all) => last.length < pageSize ? undefined : all.length * pageSize,
  });
  const items = computed(() => uniqueItems(query.items.value));
  const previous = shallowRef<readonly SupplierSummary[]>();
  watch([items, query.loading, query.error], ([items, loading, error]) => {
    if (!loading && !error) previous.value = items;
  }, { immediate: true });
  return {
    suppliers: computed(() => query.loading.value || query.error.value ? previous.value : items.value),
    loading: query.loading,
    error: query.error,
    refetch: query.refetch,
    hasNextPage: query.hasNextPage,
    fetchNextPage: query.fetchNextPage,
    isFetchingNextPage: query.isFetchingNextPage,
  };
}

export function useSupplier(sourcing: SourcingControls, id: () => string | null) {
  const query = useAppQuery({
    key: () => sourcing.keys.supplier(id() ?? ''),
    enabled: () => sourcing.enabled() && Boolean(id()),
    queryFn: ({ signal }) => sourcing.api.getSupplier(sourcing.organizationId(), id()!, signal),
  });
  return { supplier: query.data, loading: query.loading, error: query.error, refetch: query.refetch };
}

export function useMediaSummary(sourcing: SourcingControls) {
  const query = useAppQuery({
    key: () => sourcing.keys.summary(),
    enabled: sourcing.enabled,
    queryFn: ({ signal }) => sourcing.api.mediaSummary(sourcing.organizationId(), signal),
  });
  return { summary: query.data, error: query.error };
}

/** Pages one gallery 100 items at a time; invalidation refreshes every loaded page. */
export function useSourcingMedia(sourcing: SourcingControls, filter: () => MediaFilter) {
  const pageSize = sourcingLimits.mediaPage;
  const query = useAppInfiniteQuery<SourcingMedia, SourcingMedia[]>({
    key: () => sourcing.keys.mediaList(filter()),
    enabled: sourcing.enabled,
    initialPageParam: 0,
    queryFn: ({ pageParam, signal }) =>
      sourcing.api.listMedia(sourcing.organizationId(), filter(), { limit: pageSize, offset: Number(pageParam) || 0 }, signal),
    getNextPageParam: (last, all) => last.length < pageSize ? undefined : all.length * pageSize,
  });
  return { ...query, items: computed(() => uniqueItems(query.items.value)) };
}

/** Moves between the list, inbox and suppliers inside one document, guarding unsaved forms. */
export function useSourcingNavigation(access: AccessControls, organizationId: () => string, initial: SourcingLocation) {
  const { t } = useUiLocale();
  const current = shallowRef<SourcingLocation>(initial);
  const confirmDiscard = () => requestConfirmation(t('Discard unsaved changes and leave this page?'), { confirmLabel: t('Discard changes') });
  async function open(next: SourcingLocation): Promise<boolean> {
    if (access.busy.value) return false;
    if (access.dirty.value && !await confirmDiscard()) return false;
    if (access.busy.value) return false;
    access.setDirty(false);
    const path = sourcingPath(organizationId(), next);
    if (window.location.pathname !== path) window.history.pushState(window.history.state, '', path);
    current.value = next;
    return true;
  }
  async function popstate(): Promise<void> {
    const location = readWorkspaceLocation(window.location.pathname);
    if (location.section !== 'sourcing' || !location.sourcing || location.organizationId?.toLowerCase() !== organizationId().toLowerCase()) return;
    if (access.dirty.value && !await confirmDiscard()) {
      window.history.pushState(window.history.state, '', sourcingPath(organizationId(), current.value));
      return;
    }
    access.setDirty(false);
    current.value = location.sourcing;
  }
  const listener = () => { void popstate(); };
  window.addEventListener('popstate', listener);
  onScopeDispose(() => window.removeEventListener('popstate', listener));
  return { current, open };
}
