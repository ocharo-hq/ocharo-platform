import { computed, ref } from 'vue';
import { notifyError } from '@/application/shared/UseFeedback';

import { useAppQuery, useQueryCache } from '@wow-two-beta/ui-vue/query';

import type { DraftInput, WorkspaceLocation } from '@/domain/workspace/Models';

import type { AccessControls } from './UseAccess';

/** Owns tenant-scoped reads and server-confirmed preparation commands for one document. */
export function useWorkspace(access: AccessControls, location: WorkspaceLocation) {
  const cache = useQueryCache();
  const refreshing = ref(false);
  const orgId = location.organizationId ?? '';
  const scope = [access.session.value?.sessionId, orgId];
  const organizationsQuery = useAppQuery({
    key: [scope[0], 'organizations'],
    queryFn: ({ signal }) => access.api.getOrganizations(signal),
  });
  const organization = computed(() => organizationsQuery.data.value?.find((org) => org.id === orgId));
  const enabled = () => Boolean(organization.value);
  const brandsQuery = useAppQuery({
    key: [...scope, 'brands'],
    enabled,
    queryFn: ({ signal }) => access.api.getBrands(orgId, signal),
  });
  const mediaQuery = useAppQuery({
    key: [...scope, 'media'],
    enabled,
    queryFn: ({ signal }) => access.api.getMedia(orgId, signal),
  });
  const draftsQuery = useAppQuery({
    key: [...scope, 'drafts'],
    enabled,
    queryFn: ({ signal }) => access.api.getDrafts(orgId, signal),
  });
  const draftKey = [...scope, 'draft', location.draftId];
  const draftQuery = useAppQuery({
    key: draftKey,
    enabled: () => enabled() && Boolean(location.draftId && location.draftId !== 'new'),
    queryFn: ({ signal }) => access.api.getDraft(orgId, location.draftId!, signal),
  });
  const canEdit = computed(() => organization.value?.role === 'owner' || organization.value?.role === 'editor');
  const canApprove = computed(() => organization.value?.role === 'owner');
  const reads = [organizationsQuery, brandsQuery, mediaQuery, draftsQuery, draftQuery];
  // Transient refresh failures must not unmount an editor with unsaved work.
  const error = computed(() => reads.find(query => query.error.value &&
    (query.data.value === undefined || [401, 403, 404].includes(query.error.value.status)))?.error.value);
  const refreshError = computed(() => reads.find(query => query.error.value && query.data.value !== undefined)?.error.value);
  const loading = computed(
    () =>
      organizationsQuery.loading.value ||
      brandsQuery.loading.value ||
      mediaQuery.loading.value ||
      draftsQuery.loading.value ||
      draftQuery.loading.value,
  );

  const initialLoading = computed(() => reads.some(query => query.loading.value && query.data.value === undefined));

  async function refreshAfterWrite(keys: readonly unknown[][]) {
    refreshing.value = true;
    try { await Promise.all(keys.map(key => cache.invalidate(key))); }
    catch { notifyError('Saved successfully. The current view could not refresh. Your local edits are preserved.', 'workspace-refresh'); }
    finally { refreshing.value = false; }
  }

  async function refresh() {
    if (refreshing.value || loading.value || access.busy.value || access.dirty.value) return;
    refreshing.value = true;
    try {
      const keys: unknown[][] = [[scope[0], 'organizations']];
      if (organization.value) keys.push([...scope, 'brands'], [...scope, 'media'], [...scope, 'drafts']);
      if (organization.value && location.draftId && location.draftId !== 'new') keys.push(draftKey);
      await Promise.all(keys.map(key => cache.invalidate(key)));
    } finally { refreshing.value = false; }
  }
  function createOrganization(input: { name: string; slug: string }, signal: AbortSignal) {
    return access.write((s) => access.api.createOrganization(input, s), signal);
  }
  async function createBrand(input: { name: string; timeZone: string }, signal: AbortSignal) {
    const result = await access.write((s) => access.api.createBrand(orgId, input, s), signal);
    if (result.ok) await refreshAfterWrite([[...scope, 'brands']]);
    return result;
  }
  async function uploadMedia(file: File, provenance: string, signal: AbortSignal) {
    const result = await access.write((s) => access.api.uploadMedia(orgId, file, provenance, s), signal);
    if (result.ok) await refreshAfterWrite([[...scope, 'media']]);
    return result;
  }
  async function saveDraft(input: DraftInput, expectedRevision: number | null, signal: AbortSignal) {
    const id = location.draftId === 'new' ? null : location.draftId;
    const result = await access.write((s) => access.api.saveDraft(orgId, id, input, expectedRevision, s), signal);
    if (result.ok) await refreshAfterWrite([[...scope, 'drafts'], ...(id ? [draftKey] : [])]);
    return result;
  }
  async function transition(
    action: 'request-review' | 'approve' | 'revoke-approval',
    revision: number,
    reason: string,
    signal: AbortSignal,
  ) {
    const result = await access.write(
      (s) => access.api.transition(orgId, location.draftId!, action, revision, reason, s),
      signal,
    );
    if (result.ok) await refreshAfterWrite([draftKey, [...scope, 'drafts']]);
    return result;
  }
  function createExport(revision: number, key: string, signal: AbortSignal) {
    return access.write((s) => access.api.createExport(orgId, location.draftId!, revision, key, s), signal);
  }
  return {
    organizations: organizationsQuery.data,
    organization,
    brands: brandsQuery.data,
    media: mediaQuery.data,
    drafts: draftsQuery.data,
    draft: draftQuery.data,
    canEdit,
    canApprove,
    error,
    refreshError,
    loading,
    initialLoading,
    refreshing,
    refresh,
    createOrganization,
    createBrand,
    uploadMedia,
    saveDraft,
    transition,
    createExport,
  };
}
export type WorkspaceControls = ReturnType<typeof useWorkspace>;
