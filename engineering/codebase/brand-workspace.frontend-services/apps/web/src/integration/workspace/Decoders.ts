import { validateSnapshot, type DesignDocument, type DesignSummary } from '@/domain/workspace/EditorModels';
import type { DesignRevision } from '@/domain/workspace/DesignRevisions';
import { ApiFailureFactory, type ApiDecoder } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';

import {
  organizationRoles,
  type Brand,
  type Draft,
  type DraftSummary,
  type ManualExport,
  type MediaAsset,
  type Organization,
  type Session,
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
/** @internal The BFF's session payload; the person and organization are absent while signed out. */
const isBffSession = (v: unknown): v is Record<string, unknown> =>
  record(v) && typeof v.isAuthenticated === 'boolean' && (!v.isAuthenticated || (
    record(v.person) && typeof v.person.sub === 'string' && v.person.sub.length > 0 &&
    typeof v.csrfToken === 'string' && v.csrfToken.length > 0 &&
    Array.isArray(v.permissions) && v.permissions.every((permission) => typeof permission === 'string') &&
    (v.organization === null || (record(v.organization) && typeof v.organization.id === 'string'))));
/** @internal Reads an optional string member. */
const text = (v: unknown): string | null => (typeof v === 'string' && v.trim().length > 0 ? v : null);
/** Maps the BFF's `GET /api/identity/session` into Desk's session. */
export const sessionDecoder: ApiDecoder<Session> = (value) => {
  if (!isBffSession(value)) return ResultExtensions.fail(ApiFailureFactory.create('protocol'));
  if (!value.isAuthenticated)
    return ResultExtensions.ok({ authenticated: false, user: null, sessionId: null, csrfToken: '', organizationId: null, role: null, permissions: [] });
  const person = value.person as Record<string, unknown>;
  const organization = record(value.organization) ? value.organization : null;
  const role = organizationRoles.find((item) => item === organization?.role) ?? null;
  const email = text(person.email), phone = text(person.phoneNumber);
  return ResultExtensions.ok({
    authenticated: true,
    user: { id: String(person.sub), email: email ?? '', displayName: text(person.name) ?? email ?? phone ?? '', phone },
    sessionId: String(person.sub),
    csrfToken: String(value.csrfToken),
    organizationId: organization ? String(organization.id) : null,
    role,
    permissions: value.permissions as string[],
  });
};
export const isOrganization = (v: unknown): v is Organization =>
  record(v) && strings(v, ['id', 'name', 'slug']) && organizationRoles.some((role) => role === v.role);
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
export const brandDecoder = decoder(isBrand);
export const mediaDecoder = decoder(isMedia);
export const draftDecoder = decoder(isDraft);
export const exportDecoder = decoder(isExport);
/** Validates every element of a server collection. */
export function listDecoder<T>(guard: (value: unknown) => value is T): ApiDecoder<T[]> {
  return decoder((v): v is T[] => Array.isArray(v) && v.every(guard));
}
export const signOutDecoder = decoder((v): v is { endSessionUrl: string } => record(v) && typeof v.endSessionUrl === 'string' && v.endSessionUrl.length > 0);

function catalogueSummary(value:unknown):boolean {
  if(value===undefined||value===null)return true;
  return record(value)&&strings(value,['slug','description'])&&typeof value.isActive==='boolean'&&record(value.outputs)&&typeof value.outputs.web==='boolean'&&typeof value.outputs.pdf==='boolean'&&record(value.web)&&['editorial','grid'].includes(String(value.web.theme))&&Number.isSafeInteger(value.web.pageCount)&&Number(value.web.pageCount)>=0&&Number.isSafeInteger(value.pdfPageCount)&&Number(value.pdfPageCount)>=0;
}
export function isDesignSummary(value: unknown): value is DesignSummary {
  return record(value) && strings(value, ['id', 'organizationId', 'title', 'createdAtUtc', 'updatedAtUtc']) &&
    ['catalogue', 'business-card'].includes(String(value.kind)) && revision(value.revision) && typeof value.archived === 'boolean' && catalogueSummary(value.catalogue) && (value.locale === undefined || value.locale === null || typeof value.locale === 'string');
}
export function isDesignDocument(value: unknown): value is DesignDocument {
  if (!isDesignSummary(value) || !record(value)) return false;
  try { return validateSnapshot(value.snapshot).kind === value.kind; } catch { return false; }
}
export const designDocumentDecoder = decoder(isDesignDocument);

/** Validates immutable revision metadata before displaying its date or restore action. */
export function isDesignRevision(value: unknown): value is DesignRevision {
  return record(value) && revision(value.revision) && strings(value, ['title', 'savedBy', 'savedAtUtc']) &&
    typeof value.archived === 'boolean' && Number.isFinite(Date.parse(String(value.savedAtUtc)));
}
