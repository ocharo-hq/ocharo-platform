import {
  ApiFailureFactory,
  createApiClient,
  rawEnvelope,
  type ApiFailure,
  type ResponseEnvelope,
} from '@wow-two-beta/ui-vue/foundation/http';
import { ResultExtensions } from '@wow-two-beta/ui-vue/foundation/results';
import type {
  BrandAppearance,
  BrandIdentity,
  ConsumerView,
  DraftView,
  LogoVersion,
  PublicBrand,
  ReleaseSummary,
} from '../../domain/brand/BrandModels';

const envelope: ResponseEnvelope = {
  unwrap: rawEnvelope.unwrap,
  toFailure: (parsed, response) =>
    ApiFailureFactory.create(
      'http',
      response,
      typeof parsed === 'object' && parsed !== null && !Array.isArray(parsed)
        ? (parsed as Record<string, unknown>)
        : null,
    ),
};
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
  if ([400, 422].includes(failure.status)) return 'Kiritilgan qiymatlarni tekshiring. O‘zgarishlaringiz saqlanib turibdi.';
  return 'So‘rov bajarilmadi. O‘zgarishlaringiz saqlanib turibdi; qayta urinib ko‘ring.';
}

/** Same-origin brand endpoints; mutations are never retried automatically. */
export function createBrandApi(fetchImplementation: typeof fetch = globalThis.fetch) {
  const client = createApiClient({ envelope, retry: false, fetch: fetchImplementation });
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
    publicKit: () => client.get<PublicBrand>('/api/public/brand', { decode: shape(isObject) }),
    /** Multipart upload stays on fetch: the JSON client serializes bodies. */
    async upload(slot: string, file: File, note: string | null) {
      const form = new FormData();
      form.append('file', file);
      if (note) form.append('note', note);
      try {
        const response = await fetchImplementation(`${root}/logos/${encodeURIComponent(slot)}`, {
          method: 'POST',
          body: form,
        });
        const parsed: unknown = await response.json().catch(() => null);
        if (!response.ok)
          return ResultExtensions.fail(
            ApiFailureFactory.create('http', response, isObject(parsed) ? (parsed as Record<string, unknown>) : null),
          );
        return isObject(parsed)
          ? ResultExtensions.ok(parsed as LogoVersion)
          : ResultExtensions.fail(ApiFailureFactory.create('protocol'));
      } catch {
        return ResultExtensions.fail(ApiFailureFactory.create('transport'));
      }
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
