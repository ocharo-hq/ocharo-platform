import { createApiClient } from '@wow-two-beta/ui-vue/foundation/http';

import type { DraftInput } from '@/domain/workspace/Models';

import {
  brandDecoder,
  draftDecoder,
  exportDecoder,
  isBrand,
  isMedia,
  isOrganization,
  isSummary,
  listDecoder,
  loginDecoder,
  mediaDecoder,
  organizationDecoder,
  sessionDecoder,
} from './Decoders';

/** Creates one document's cookie client. Each unsafe request captures its session-bound CSRF token. */
export function createWorkspaceApi(csrfToken: () => string, unauthorized: () => void) {
  const client = createApiClient({ credentials: 'same-origin', onUnauthorized: unauthorized, retry: false });
  const request = (signal?: AbortSignal) => ({
    signal: AbortSignal.any([AbortSignal.timeout(30_000), ...(signal ? [signal] : [])]),
  });
  const write = (signal?: AbortSignal) => ({ ...request(signal), headers: { 'X-CSRF-TOKEN': csrfToken() } });
  const org = (id: string) => `/api/organizations/${encodeURIComponent(id)}`;
  const draft = (orgId: string, id: string) => `${org(orgId)}/drafts/${encodeURIComponent(id)}`;
  return {
    getSession: (signal?: AbortSignal) =>
      client.get('/api/auth/session', { ...request(signal), decode: sessionDecoder }),
    login: (input: { email: string; password: string }, signal: AbortSignal) =>
      client.post('/api/auth/login', { ...write(signal), body: input, decode: loginDecoder }),
    logout: (signal?: AbortSignal) => client.post('/api/auth/logout', { ...write(signal), body: {}, response: 'empty' }),
    getOrganizations: (signal: AbortSignal) =>
      client.get('/api/organizations', { ...request(signal), decode: listDecoder(isOrganization) }),
    createOrganization: (input: { name: string; slug: string }, signal: AbortSignal) =>
      client.post('/api/organizations', { ...write(signal), body: input, decode: organizationDecoder }),
    getBrands: (id: string, signal: AbortSignal) =>
      client.get(`${org(id)}/brands`, { ...request(signal), decode: listDecoder(isBrand) }),
    createBrand: (id: string, input: { name: string; timeZone: string }, signal: AbortSignal) =>
      client.post(`${org(id)}/brands`, { ...write(signal), body: input, decode: brandDecoder }),
    getMedia: (id: string, signal: AbortSignal) =>
      client.get(`${org(id)}/media`, { ...request(signal), decode: listDecoder(isMedia) }),
    uploadMedia: (id: string, file: File, provenance: string, signal: AbortSignal) => {
      const body = new FormData();
      body.append('file', file);
      body.append('provenance', provenance);
      return client.post(`${org(id)}/media`, { ...write(signal), body, decode: mediaDecoder });
    },
    getDrafts: (id: string, signal: AbortSignal) =>
      client.get(`${org(id)}/drafts`, { ...request(signal), decode: listDecoder(isSummary) }),
    getDraft: (id: string, draftId: string, signal: AbortSignal) =>
      client.get(draft(id, draftId), { ...request(signal), decode: draftDecoder }),
    saveDraft: (
      id: string,
      draftId: string | null,
      input: DraftInput,
      expectedRevision: number | null,
      signal: AbortSignal,
    ) =>
      draftId
        ? client.put(draft(id, draftId), {
            ...write(signal),
            body: { ...input, expectedRevision },
            decode: draftDecoder,
          })
        : client.post(`${org(id)}/drafts`, { ...write(signal), body: input, decode: draftDecoder }),
    transition: (
      id: string,
      draftId: string,
      action: 'request-review' | 'approve' | 'revoke-approval',
      expectedRevision: number,
      reason: string,
      signal: AbortSignal,
    ) =>
      client.post(`${draft(id, draftId)}/${action}`, {
        ...write(signal),
        body: { expectedRevision, ...(action === 'revoke-approval' ? { reason } : {}) },
        decode: draftDecoder,
      }),
    createExport: (
      id: string,
      draftId: string,
      expectedRevision: number,
      idempotencyKey: string,
      signal: AbortSignal,
    ) =>
      client.post(`${draft(id, draftId)}/exports`, {
        ...write(signal),
        body: { expectedRevision, idempotencyKey },
        decode: exportDecoder,
      }),
  };
}
