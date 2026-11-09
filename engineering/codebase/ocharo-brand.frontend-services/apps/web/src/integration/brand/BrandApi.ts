import { ApiFailureFactory, type ApiClient, type ApiFailure } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import type {
  BrandAppearance,
  BrandIdentity,
  ConsumerView,
  DraftView,
  LogoVersion,
  PublicBrand,
  ReleaseSummary,
} from '../../domain/brand/BrandModels';
import { brandHttp } from '../common/IdentitySession';

function shape<T>(check: (value: unknown) => boolean) {
  return (value: unknown) =>
    check(value) ? ResultExtensions.ok(value as T) : ResultExtensions.fail(ApiFailureFactory.create('protocol'));
}
const isObject = (value: unknown) => typeof value === 'object' && value !== null && !Array.isArray(value);
const isArray = (value: unknown) => Array.isArray(value);

/** Selects safe operator copy without displaying server diagnostics. */
export function describeFailure(failure: ApiFailure): string {
  if (failure.code === 'cancelled') return '';
  if (failure.status === 401) return 'Qayta kiring. O‘zgarishlaringiz saqlanib turibdi.';
  if (failure.status === 403) return 'Ruxsatingiz o‘zgardi. O‘zgarishlaringiz saqlanib turibdi.';
  if (failure.status === 409) return 'Yangi talqin mavjud. O‘zgarishlaringiz saqlanib turibdi; holatni tekshiring.';
  if ([400, 422].includes(failure.status))
    return 'Kiritilgan qiymatlarni tekshiring. O‘zgarishlaringiz saqlanib turibdi.';
  return 'So‘rov bajarilmadi. O‘zgarishlaringiz saqlanib turibdi; qayta urinib ko‘ring.';
}

/**
 * Same-origin brand endpoints over `client`, by default the session's {@link brandHttp}; mutations are never retried
 * automatically. The private API answers in the SDK envelope (`{ data }`) and fails with SDK problem details; the public
 * kit is read bare, as every Ocharo app reads it. Reads take the caller's cancellation signal.
 */
export function createBrandApi(client: ApiClient = brandHttp) {
  const root = '/api/brand';
  return {
    /** `GET /api/brand/draft`: the draft, its revision, every logo version and what waits to be published. */
    draft: (signal?: AbortSignal) => client.get<DraftView>(`${root}/draft`, { signal, decode: shape(isObject) }),
    /** `PUT /api/brand/identity`: saves the draft identity against the revision it was edited from. */
    saveIdentity: (expectedRevision: number, identity: BrandIdentity) =>
      client.put<DraftView>(`${root}/identity`, { body: { expectedRevision, identity }, decode: shape(isObject) }),
    /** `POST /api/brand/logos/{slot}/current`: makes a version the slot's draft file. */
    setCurrent: (slot: string, expectedRevision: number, versionId: string) =>
      client.post<DraftView>(`${root}/logos/${encodeURIComponent(slot)}/current`, {
        body: { expectedRevision, versionId },
        decode: shape(isObject),
      }),
    /** `POST /api/brand/logos/versions/{id}/status`: approves a version or returns it to draft. */
    setStatus: (id: string, status: string) =>
      client.post<LogoVersion>(`${root}/logos/versions/${encodeURIComponent(id)}/status`, {
        body: { status },
        decode: shape(isObject),
      }),
    /** `POST /api/brand/publish`: publishes the draft as the next release. */
    publish: (expectedRevision: number, note: string | null) =>
      client.post<ReleaseSummary>(`${root}/publish`, { body: { expectedRevision, note }, decode: shape(isObject) }),
    /** `GET /api/brand/releases`: every published release, newest first. */
    releases: (signal?: AbortSignal) =>
      client.get<ReleaseSummary[]>(`${root}/releases`, { signal, decode: shape(isArray) }),
    /** `GET /api/brand/appearances`: every scheduled, running, ended or cancelled event look. */
    appearances: (signal?: AbortSignal) =>
      client.get<BrandAppearance[]>(`${root}/appearances`, { signal, decode: shape(isArray) }),
    /** `POST /api/brand/appearances`: schedules a new event look or edits a scheduled one. */
    saveAppearance: (body: {
      id: string | null;
      name: string;
      startsAt: string;
      endsAt: string;
      overrides: Record<string, string>;
      accentHex: string | null;
      note: string | null;
      linkLabel: string | null;
      linkUrl: string | null;
    }) => client.post<BrandAppearance>(`${root}/appearances`, { body, decode: shape(isObject) }),
    /** `POST /api/brand/appearances/{id}/cancel`: cancels a scheduled event look. */
    cancelAppearance: (id: string) =>
      client.post<BrandAppearance>(`${root}/appearances/${encodeURIComponent(id)}/cancel`, {
        body: {},
        decode: shape(isObject),
      }),
    /** `GET /api/brand/consumers`: the apps that read the brand and the release each last saw. */
    consumers: (signal?: AbortSignal) =>
      client.get<ConsumerView[]>(`${root}/consumers`, { signal, decode: shape(isArray) }),
    /** `GET /api/public/brand`: the published kit, anonymous and not enveloped. */
    publicKit: (signal?: AbortSignal) =>
      client.get<PublicBrand>('/api/public/brand', { signal, unwrap: false, decode: shape(isObject) }),
    /** `POST /api/brand/logos/{slot}`: a multipart upload; the client sends `FormData` as it is, with the browser's boundary. */
    upload(slot: string, file: File, note: string | null) {
      const form = new FormData();
      form.append('file', file);
      if (note) form.append('note', note);
      return client.post<LogoVersion>(`${root}/logos/${encodeURIComponent(slot)}`, {
        body: form,
        decode: shape(isObject),
      });
    },
  } as const;
}

export const brandApi = createBrandApi();

export const brandUrls = {
  logo: (slot: string) => `/api/public/brand/logo/${slot}`,
  /** Admin media (drafts included); consumers get disclosed files only via the kit's public URLs. */
  media: (sha: string, preview = false) => `/api/brand/media/${sha}${preview ? '/preview' : ''}`,
  kit: '/api/public/brand',
  tokens: '/api/public/brand/tokens.css',
} as const;
