import { ApiFailureFactory, type ApiDecoder } from '@wow-two-beta/ui-vue/foundation/http';
import { ResultExtensions } from '@wow-two-beta/ui-vue/foundation/results';

import type {
  Brand,
  Draft,
  DraftSummary,
  ManualExport,
  MediaAsset,
  Organization,
  Session,
} from '@/domain/workspace/Models';

/** @internal Checks an untrusted JSON object. */
const record = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null && !Array.isArray(v);
/** @internal Checks required string members. */
const strings = (v: Record<string, unknown>, keys: string[]) => keys.every((key) => typeof v[key] === 'string');
/** @internal Checks a positive revision integer. */
const revision = (v: unknown): v is number => Number.isSafeInteger(v) && Number(v) > 0;
/** @internal Builds a runtime decoder rather than casting server payloads. */
function decoder<T>(guard: (value: unknown) => value is T): ApiDecoder<T> {
  return (value) =>
    guard(value) ? ResultExtensions.ok(value) : ResultExtensions.fail(ApiFailureFactory.create('protocol'));
}
export const isSession = (v: unknown): v is Session =>
  record(v) &&
  typeof v.authenticated === 'boolean' &&
  typeof v.setupRequired === 'boolean' &&
  typeof v.csrfToken === 'string' &&
  v.csrfToken.length > 0 &&
  (v.authenticated
    ? typeof v.sessionId === 'string' && record(v.user) && strings(v.user, ['id', 'email', 'displayName'])
    : v.sessionId === null && v.user === null);
export const isOrganization = (v: unknown): v is Organization =>
  record(v) && strings(v, ['id', 'name', 'slug']) && ['owner', 'editor', 'viewer'].includes(String(v.role));
export const isBrand = (v: unknown): v is Brand =>
  record(v) && strings(v, ['id', 'organizationId', 'name', 'timeZone']);
export const isMedia = (v: unknown): v is MediaAsset =>
  record(v) &&
  strings(v, ['id', 'organizationId', 'fileName', 'contentType', 'sha256', 'provenance', 'createdAtUtc']) &&
  Number.isSafeInteger(v.byteLength) &&
  Number(v.byteLength) > 0;
export const isSummary = (v: unknown): v is DraftSummary =>
  record(v) &&
  strings(v, ['id', 'brandId', 'title', 'updatedAtUtc']) &&
  revision(v.revision) &&
  ['draft', 'in-review', 'approved'].includes(String(v.status));
export const isDraft = (v: unknown): v is Draft =>
  isSummary(v) &&
  record(v) &&
  strings(v, ['caption', 'language', 'destination', 'sourceNotes']) &&
  Array.isArray(v.media) &&
  v.media.every(isMedia) &&
  (v.requestedReviewRevision === null || revision(v.requestedReviewRevision)) &&
  (v.approval === null ||
    (record(v.approval) &&
      strings(v.approval, ['id', 'approvedAtUtc', 'approvedBy']) &&
      revision(v.approval.revision)));
export const isExport = (v: unknown): v is ManualExport =>
  record(v) &&
  strings(v, ['id', 'draftId', 'createdAtUtc', 'downloadUrl']) &&
  revision(v.revision) &&
  v.packageKind === 'manual-content-package' &&
  v.mediaIncluded === true &&
  v.publicationStatus === 'unverified';
export const sessionDecoder = decoder(isSession);
export const organizationDecoder = decoder(isOrganization);
export const brandDecoder = decoder(isBrand);
export const mediaDecoder = decoder(isMedia);
export const draftDecoder = decoder(isDraft);
export const exportDecoder = decoder(isExport);
/** Validates every element of a server collection. */
export function listDecoder<T>(guard: (value: unknown) => value is T): ApiDecoder<T[]> {
  return decoder((v): v is T[] => Array.isArray(v) && v.every(guard));
}
export const loginDecoder = decoder((v): v is { authenticated: true } => record(v) && v.authenticated === true);
