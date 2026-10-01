import { computed } from 'vue';

import { useAppQuery, useQueryCache } from '@wow-two-beta/ui-vue/query';

import type { DraftInput, WorkspaceLocation } from '@/domain/workspace/Models';

import type { AccessControls } from './UseAccess';

/** Owns tenant-scoped reads and server-confirmed preparation commands for one document. */
export function useWorkspace(access: AccessControls, location: WorkspaceLocation) {
  const cache = useQueryCache();
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

  function createOrganization(input: { name: string; slug: string }, signal: AbortSignal) {
    return access.write((s) => access.api.createOrganization(input, s), signal);
  }
  async function createBrand(input: { name: string; timeZone: string }, signal: AbortSignal) {
    const result = await access.write((s) => access.api.createBrand(orgId, input, s), signal);
    if (result.ok) await cache.invalidate([...scope, 'brands']);
    return result;
  }
  async function uploadMedia(file: File, provenance: string, signal: AbortSignal) {
    const result = await access.write((s) => access.api.uploadMedia(orgId, file, provenance, s), signal);
    if (result.ok) await cache.invalidate([...scope, 'media']);
    return result;
  }
  async function saveDraft(input: DraftInput, expectedRevision: number | null, signal: AbortSignal) {
    const id = location.draftId === 'new' ? null : location.draftId;
    const result = await access.write((s) => access.api.saveDraft(orgId, id, input, expectedRevision, s), signal);
    if (result.ok && id) cache.set(draftKey, result.value);
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
    if (result.ok) cache.set(draftKey, result.value);
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
    createOrganization,
    createBrand,
    uploadMedia,
    saveDraft,
    transition,
    createExport,
  };
}
export type WorkspaceControls = ReturnType<typeof useWorkspace>;
