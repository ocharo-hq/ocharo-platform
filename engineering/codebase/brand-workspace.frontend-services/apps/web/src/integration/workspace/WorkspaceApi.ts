import type { DesignKind, DesignSnapshot } from '@/domain/workspace/EditorModels';
import { createApiClient, type ApiClientOptions } from '@wow-two-beta/fe/foundation/http';

import type { DraftInput } from '@/domain/workspace/Models';

import {
  brandDecoder,
  designDocumentDecoder,
  isDesignSummary,
  isDesignRevision,
  draftDecoder,
  exportDecoder,
  isBrand,
  isMedia,
  isOrganization,
  isSummary,
  listDecoder,
  mediaDecoder,
  sessionDecoder,
  signOutDecoder,
} from './Decoders';

/** The signed-in session's request lifetime: its scope and the 401 handler that expires it. */
export type SessionTransport = Pick<ApiClientOptions, 'scope' | 'onUnauthorized'>;

/** The BFF route that starts sign-in at Ocharo's Identity instance and returns to a local path. */
export const signInPath = (returnUrl: string) => `/api/identity/sign-in?returnUrl=${encodeURIComponent(returnUrl)}`;

/** Creates one document's cookie client. Each unsafe request captures its session-bound CSRF token. */
export function createWorkspaceApi(csrfToken: () => string, session: SessionTransport = {}) {
  const client = createApiClient({ credentials: 'same-origin', ...session, retry: false });
  const request = (signal?: AbortSignal) => ({
    signal: AbortSignal.any([AbortSignal.timeout(30_000), ...(signal ? [signal] : [])]),
  });
  const write = (signal?: AbortSignal) => ({ ...request(signal), headers: { 'X-Csrf-Token': csrfToken() } });
  const org = (id: string) => `/api/organizations/${encodeURIComponent(id)}`;
  const draft = (orgId: string, id: string) => `${org(orgId)}/drafts/${encodeURIComponent(id)}`;
  const design = (orgId: string, id?: string) => `${org(orgId)}/design-documents${id ? `/${encodeURIComponent(id)}` : ''}`;
  return {
    getDesigns: (orgId: string, kind: DesignKind, signal: AbortSignal) =>
      client.get(`${design(orgId)}?kind=${kind}&limit=100`, { ...request(signal), decode: listDecoder(isDesignSummary) }),
    getDesign: (orgId: string, id: string, signal: AbortSignal) =>
      client.get(design(orgId, id), { ...request(signal), decode: designDocumentDecoder }),
    getDesignRevisions: (orgId: string, id: string, signal: AbortSignal, offset = 0, limit = 20) =>
      client.get(`${design(orgId, id)}/revisions?limit=${limit}&offset=${offset}`, { ...request(signal), decode: listDecoder(isDesignRevision) }),
    getDesignRevision: (orgId: string, id: string, revision: number, signal: AbortSignal) =>
      client.get(`${design(orgId, id)}/revisions/${revision}`, { ...request(signal), decode: designDocumentDecoder }),
    saveDesign: (orgId: string, id: string | null, input: { kind: DesignKind; title: string; snapshot: DesignSnapshot; expectedRevision: number | null }, signal: AbortSignal) =>
      id ? client.put(design(orgId, id), { ...write(signal), body: { title: input.title, snapshot: input.snapshot, expectedRevision: input.expectedRevision }, decode: designDocumentDecoder })
        : client.post(design(orgId), { ...write(signal), body: { kind: input.kind, title: input.title, snapshot: input.snapshot }, decode: designDocumentDecoder }),
    archiveDesign: (orgId: string, id: string, expectedRevision: number, signal: AbortSignal) =>
      client.post(`${design(orgId, id)}/archive`, { ...write(signal), body: { expectedRevision }, decode: designDocumentDecoder }),
    getSession: (signal?: AbortSignal) =>
      client.get('/api/identity/session', { ...request(signal), decode: sessionDecoder }),
    /** Ends the BFF session; the answer names Identity's end-session address the browser goes to next. */
    logout: (signal?: AbortSignal) => client.post('/api/identity/sign-out', { ...write(signal), body: {}, decode: signOutDecoder }),
    getOrganizations: (signal: AbortSignal) =>
      client.get('/api/organizations', { ...request(signal), decode: listDecoder(isOrganization) }),
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
