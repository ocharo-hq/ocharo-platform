import { createApiClient, ApiFailureFactory, type ApiDecoder } from '@wow-two-beta/ui-vue/foundation/http';
import { ResultExtensions } from '@wow-two-beta/ui-vue/foundation/results';

import {
  isContactChannel, isMediaCapture, isSupplierStage,
  type ContactRoute, type MediaCapture, type MediaFilter, type MediaUpdateInput, type PersonInput, type RouteInput,
  type SourcingMedia, type SourcingMediaSummary, type SourcingUploadResult, type Supplier, type SupplierFilter,
  type SupplierInput, type SupplierPerson, type SupplierSummary,
} from '@/domain/workspace/Sourcing';

/** @internal Checks an untrusted JSON object. */
const record = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null && !Array.isArray(v);
const strings = (v: Record<string, unknown>, keys: string[]) => keys.every(key => typeof v[key] === 'string');
const nullableStrings = (v: Record<string, unknown>, keys: string[]) => keys.every(key => v[key] === null || typeof v[key] === 'string');
const count = (v: unknown) => Number.isSafeInteger(v) && Number(v) >= 0;
const revision = (v: unknown) => Number.isSafeInteger(v) && Number(v) > 0;
const nullableNumber = (v: unknown) => v === null || (typeof v === 'number' && Number.isFinite(v));
function decode<T>(guard: (v: unknown) => v is T): ApiDecoder<T> {
  return value => guard(value) ? ResultExtensions.ok(value) : ResultExtensions.fail(ApiFailureFactory.create('protocol'));
}
const list = <T>(guard: (v: unknown) => v is T) => (v: unknown): v is T[] => Array.isArray(v) && v.every(guard);

export const isSupplierSummary = (v: unknown): v is SupplierSummary => record(v) && strings(v, ['id', 'name', 'updatedAt']) &&
  nullableStrings(v, ['key', 'country', 'city', 'coverMediaId']) && isSupplierStage(v.stage) &&
  (v.primaryRoute === null || (record(v.primaryRoute) && isContactChannel(v.primaryRoute.channel) && typeof v.primaryRoute.destination === 'string')) &&
  count(v.routeCount) && count(v.personCount) && count(v.mediaCount) && revision(v.revision);
export const isSupplierPerson = (v: unknown): v is SupplierPerson => record(v) &&
  strings(v, ['id', 'supplierId', 'name', 'notes', 'createdAt', 'updatedAt']) && nullableStrings(v, ['role']) && revision(v.revision);
export const isContactRoute = (v: unknown): v is ContactRoute => record(v) &&
  strings(v, ['id', 'supplierId', 'destination', 'notes', 'createdAt', 'updatedAt']) && nullableStrings(v, ['personId', 'label', 'sourceUrl']) &&
  isContactChannel(v.channel) && typeof v.verified === 'boolean' && typeof v.isPrimary === 'boolean' && revision(v.revision);
export const isSupplier = (v: unknown): v is Supplier => record(v) && strings(v, ['id', 'name', 'notes', 'createdAt', 'updatedAt']) &&
  nullableStrings(v, ['key', 'country', 'city', 'website']) && isSupplierStage(v.stage) &&
  list(isSupplierPerson)(v.people) && list(isContactRoute)(v.routes) && revision(v.revision);
export const isSourcingMedia = (v: unknown): v is SourcingMedia => record(v) &&
  strings(v, ['id', 'sha256', 'contentType', 'originalName', 'caption', 'createdAt', 'updatedAt']) &&
  nullableStrings(v, ['supplierId', 'sourceLabel', 'sourceMessageId', 'receivedAt', 'takenAt']) &&
  count(v.byteSize) && nullableNumber(v.width) && nullableNumber(v.height) && typeof v.hasThumbnail === 'boolean' &&
  isMediaCapture(v.capture) && typeof v.hasLocation === 'boolean' && revision(v.revision);
const isSummary = (v: unknown): v is SourcingMediaSummary => record(v) && count(v.total) && count(v.unassigned);
const isUpload = (v: unknown): v is SourcingUploadResult => record(v) && isSourcingMedia(v.media) && typeof v.duplicate === 'boolean' &&
  Array.isArray(v.nearDuplicateIds) && v.nearDuplicateIds.every(id => typeof id === 'string');
const isDeleted = (v: unknown): v is { id: string } => record(v) && typeof v.id === 'string';
const isAssigned = (v: unknown): v is { updated: number } => record(v) && count(v.updated);

export interface UploadOptions { supplierId: string | null; capture: MediaCapture; caption: string }

/** Creates the Sourcing transport. It records outreach only; nothing here contacts a supplier. */
export function createSourcingApi(csrf: () => string, unauthorized: () => void) {
  const client = createApiClient({ credentials: 'same-origin', onUnauthorized: unauthorized, retry: false });
  const base = (o: string) => `/api/organizations/${encodeURIComponent(o)}/sourcing`;
  const id = encodeURIComponent;
  const write = (signal: AbortSignal) => ({ signal, headers: { 'X-CSRF-TOKEN': csrf() } });
  return {
    listSuppliers: (o: string, filter: SupplierFilter & { limit: number; offset: number }, signal: AbortSignal) => {
      const query = new URLSearchParams();
      if (filter.query.trim()) query.set('query', filter.query.trim());
      if (filter.stage) query.set('stage', filter.stage);
      query.set('limit', String(filter.limit));
      query.set('offset', String(filter.offset));
      return client.get(`${base(o)}/suppliers?${query}`, { signal, decode: decode(list(isSupplierSummary)) });
    },
    createSupplier: (o: string, input: SupplierInput, signal: AbortSignal) =>
      client.post(`${base(o)}/suppliers`, { ...write(signal), body: input, decode: decode(isSupplier) }),
    getSupplier: (o: string, supplierId: string, signal: AbortSignal) =>
      client.get(`${base(o)}/suppliers/${id(supplierId)}`, { signal, decode: decode(isSupplier) }),
    updateSupplier: (o: string, supplierId: string, expectedRevision: number, input: SupplierInput, signal: AbortSignal) =>
      client.put(`${base(o)}/suppliers/${id(supplierId)}`, { ...write(signal), body: { expectedRevision, ...input }, decode: decode(isSupplier) }),
    createPerson: (o: string, supplierId: string, input: PersonInput, signal: AbortSignal) =>
      client.post(`${base(o)}/suppliers/${id(supplierId)}/people`, { ...write(signal), body: input, decode: decode(isSupplierPerson) }),
    updatePerson: (o: string, personId: string, expectedRevision: number, input: PersonInput, signal: AbortSignal) =>
      client.put(`${base(o)}/people/${id(personId)}`, { ...write(signal), body: { expectedRevision, ...input }, decode: decode(isSupplierPerson) }),
    deletePerson: (o: string, personId: string, expectedRevision: number, signal: AbortSignal) =>
      client.delete(`${base(o)}/people/${id(personId)}?expectedRevision=${expectedRevision}`, { ...write(signal), decode: decode(isDeleted) }),
    createRoute: (o: string, supplierId: string, input: RouteInput, signal: AbortSignal) =>
      client.post(`${base(o)}/suppliers/${id(supplierId)}/routes`, { ...write(signal), body: input, decode: decode(isContactRoute) }),
    updateRoute: (o: string, routeId: string, expectedRevision: number, input: RouteInput, signal: AbortSignal) =>
      client.put(`${base(o)}/routes/${id(routeId)}`, { ...write(signal), body: { expectedRevision, ...input }, decode: decode(isContactRoute) }),
    deleteRoute: (o: string, routeId: string, expectedRevision: number, signal: AbortSignal) =>
      client.delete(`${base(o)}/routes/${id(routeId)}?expectedRevision=${expectedRevision}`, { ...write(signal), decode: decode(isDeleted) }),
    listMedia: (o: string, filter: MediaFilter, page: { limit: number; offset: number }, signal: AbortSignal) => {
      const query = new URLSearchParams();
      if (filter.kind === 'supplier') query.set('supplierId', filter.supplierId);
      else if (filter.kind === 'inbox') query.set('unassigned', 'true');
      query.set('limit', String(page.limit));
      query.set('offset', String(page.offset));
      return client.get(`${base(o)}/media?${query}`, { signal, decode: decode(list(isSourcingMedia)) });
    },
    mediaSummary: (o: string, signal: AbortSignal) => client.get(`${base(o)}/media/summary`, { signal, decode: decode(isSummary) }),
    uploadMedia: (o: string, file: File, options: UploadOptions, signal: AbortSignal) => {
      const body = new FormData();
      body.append('file', file, file.name);
      if (options.supplierId) body.append('supplierId', options.supplierId);
      body.append('capture', options.capture);
      if (options.caption) body.append('caption', options.caption);
      return client.post(`${base(o)}/media`, { ...write(signal), body, decode: decode(isUpload) });
    },
    updateMedia: (o: string, mediaId: string, expectedRevision: number, input: MediaUpdateInput, signal: AbortSignal) =>
      client.put(`${base(o)}/media/${id(mediaId)}`, { ...write(signal), body: { expectedRevision, ...input }, decode: decode(isSourcingMedia) }),
    assignMedia: (o: string, mediaIds: string[], supplierId: string | null, signal: AbortSignal) =>
      client.post(`${base(o)}/media/assign`, { ...write(signal), body: { mediaIds, supplierId }, decode: decode(isAssigned) }),
    deleteMedia: (o: string, mediaId: string, expectedRevision: number, signal: AbortSignal) =>
      client.delete(`${base(o)}/media/${id(mediaId)}?expectedRevision=${expectedRevision}`, { ...write(signal), decode: decode(isDeleted) }),
    contentUrl: (o: string, mediaId: string) => `${base(o)}/media/${id(mediaId)}/content`,
    thumbnailUrl: (o: string, mediaId: string) => `${base(o)}/media/${id(mediaId)}/thumbnail`,
  };
}
export type SourcingApi = ReturnType<typeof createSourcingApi>;
