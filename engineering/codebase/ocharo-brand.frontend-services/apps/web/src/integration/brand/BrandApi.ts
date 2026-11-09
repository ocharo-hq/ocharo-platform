import { identitySession } from '../common/IdentitySessionClient';
import { ApiFailureFactory, createApiClient, wowTwoEnvelope, type ApiFailure } from '@wow-two-beta/fe/foundation/http';
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
 * Same-origin brand endpoints; mutations are never retried automatically. The private API answers in the SDK envelope
 * (`{ data }`) and fails with SDK problem details; the public kit is read bare, as every Ocharo app reads it.
 */
export function createBrandApi(fetchImplementation: typeof fetch = globalThis.fetch) {
  const client = createApiClient({ envelope: wowTwoEnvelope, retry: false, fetch: fetchImplementation });
  const root = '/api/brand';
  return {
    draft: () => client.get<DraftView>(`${root}/draft`, { decode: shape(isObject) }),
    saveIdentity: (expectedRevision: number, identity: BrandIdentity) =>
      client.put<DraftView>(`${root}/identity`, { body: { expectedRevision, identity }, decode: shape(isObject) }),
    setCurrent: (slot: string, expectedRevision: number, versionId: string) =>
      client.post<DraftView>(`${root}/logos/${encodeURIComponent(slot)}/current`, {
        body: { expectedRevision, versionId },
        decode: shape(isObject),
      }),
    setStatus: (id: string, status: string) =>
      client.post<LogoVersion>(`${root}/logos/versions/${encodeURIComponent(id)}/status`, {
        body: { status },
        decode: shape(isObject),
      }),
    publish: (expectedRevision: number, note: string | null) =>
      client.post<ReleaseSummary>(`${root}/publish`, { body: { expectedRevision, note }, decode: shape(isObject) }),
    releases: () => client.get<ReleaseSummary[]>(`${root}/releases`, { decode: shape(isArray) }),
    appearances: () => client.get<BrandAppearance[]>(`${root}/appearances`, { decode: shape(isArray) }),
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
    cancelAppearance: (id: string) =>
      client.post<BrandAppearance>(`${root}/appearances/${encodeURIComponent(id)}/cancel`, {
        body: {},
        decode: shape(isObject),
      }),
    consumers: () => client.get<ConsumerView[]>(`${root}/consumers`, { decode: shape(isArray) }),
    /** The public kit is not enveloped. */
    publicKit: () => client.get<PublicBrand>('/api/public/brand', { unwrap: false, decode: shape(isObject) }),
    /** Multipart upload: the client sends `FormData` as it is, with the browser's boundary. */
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

export const brandApi = createBrandApi(identitySession.fetch);

export const brandUrls = {
  logo: (slot: string) => `/api/public/brand/logo/${slot}`,
  /** Admin media (drafts included); consumers get disclosed files only via the kit's public URLs. */
  media: (sha: string, preview = false) => `/api/brand/media/${sha}${preview ? '/preview' : ''}`,
  kit: '/api/public/brand',
  tokens: '/api/public/brand/tokens.css',
} as const;
