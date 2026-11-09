import { ApiFailureFactory, type ApiClient, type ApiFailure } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions, type Result } from '@wow-two-beta/fe/foundation/results';
import { catalogueSettings, productDetail } from '../catalogue/CatalogueWire';
import { catalogueHttp } from '../common/IdentitySession';
import type { DesignSource } from '../../domain/design/CatalogueDesign';
import {
  validateSnapshot,
  type DesignDocument,
  type DesignSnapshot,
  type DesignSummary,
} from '../../domain/design/EditorModels';
import type { CatalogueRenderBundle } from './DesignExport';
export interface Preparation {
  designRevision: number;
  fingerprint: string;
  pdfPageIds: string[];
}
export interface PreparedRelease {
  id: string;
  designId: string;
  designRevision: number;
  slug: string;
  locale: string;
  title: string;
  createdAtUtc: string;
  pdfAsset?: string | null;
  pdfUrl?: string | null;
  pages: unknown[];
}
export interface ReleaseSummary {
  id: string;
  designRevision: number;
  slug: string;
  locale: string;
  createdAtUtc: string;
  publishedAtUtc: string | null;
  withdrawnAtUtc?: string | null;
}
export interface DesignRevision {
  revision: number;
  title: string;
  createdAtUtc?: string;
  updatedAtUtc?: string;
}
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v);
/** Checks the `data` of the API's success envelope and maps it to the app's model; anything else is a protocol failure. */
function decode<T>(value: unknown, check: (v: unknown) => boolean, map: (v: never) => T = (v) => v as T) {
  return check(value)
    ? ResultExtensions.ok(map(value as never))
    : ResultExtensions.fail(ApiFailureFactory.create('protocol'));
}
/** The API's JSON leaves a null field out; a release keeps reading `null` for a time it never reached. */
function releaseSummary(raw: Record<string, unknown>): ReleaseSummary {
  return {
    ...(raw as unknown as ReleaseSummary),
    publishedAtUtc: (raw.publishedAtUtc as string | undefined) ?? null,
    withdrawnAtUtc: (raw.withdrawnAtUtc as string | undefined) ?? null,
  };
}
/** Maps the designer's source: every product with its null fields restored, and the settings. */
function designSource(raw: Record<string, unknown>): DesignSource {
  return {
    products: (raw.products as never[]).map(productDetail),
    settings: catalogueSettings(raw.settings as never),
  };
}
function validDocument(v: unknown) {
  if (!object(v) || typeof v.id !== 'string' || typeof v.title !== 'string' || !Number.isSafeInteger(v.revision))
    return false;
  try {
    validateSnapshot(v.snapshot);
    return true;
  } catch {
    return false;
  }
}
/**
 * The retained Catalogue API is the sole writer; all requests remain same-origin and local, over `client`, by default the
 * session's {@link catalogueHttp}. Payloads arrive in the API's `{ data }` envelope and failures as ProblemDetails; every
 * endpoint returns a `Result`, so an expected failure never throws.
 */
export function createDesignApi(client: ApiClient = catalogueHttp) {
  const root = '/api/catalogue/designs',
    path = (id: string) => `${root}/${encodeURIComponent(id)}`;
  const isRelease = (v: unknown) => object(v) && typeof v.id === 'string';
  return {
    /** `GET /api/catalogue/designs`: every saved catalogue, newest first. */
    list: (signal?: AbortSignal): Promise<Result<DesignSummary[], ApiFailure>> =>
      client.get<DesignSummary[]>(root, { signal, decode: (v) => decode(v, Array.isArray) }),
    /** `GET /api/catalogue/designs/{id}`: one catalogue with its validated snapshot. */
    get: (id: string, signal?: AbortSignal): Promise<Result<DesignDocument, ApiFailure>> =>
      client.get<DesignDocument>(path(id), { signal, decode: (v) => decode(v, validDocument) }),
    /** `GET /api/catalogue/design-source`: every product and the settings the designers place. */
    source: (signal?: AbortSignal): Promise<Result<DesignSource, ApiFailure>> =>
      client.get<DesignSource>('/api/catalogue/design-source', {
        signal,
        decode: (v) => decode(v, (v) => object(v) && Array.isArray(v.products) && object(v.settings), designSource),
      }),
    /** `POST /api/catalogue/designs` creates a catalogue; `PUT /api/catalogue/designs/{id}` saves one at a revision. */
    save: (
      id: string | null,
      title: string,
      snapshot: DesignSnapshot,
      expectedRevision?: number,
    ): Promise<Result<DesignDocument, ApiFailure>> => {
      const body = {
        title,
        snapshot: validateSnapshot(snapshot),
        ...(expectedRevision !== undefined ? { expectedRevision } : {}),
      };
      return id
        ? client.put<DesignDocument>(path(id), { body, decode: (v) => decode(v, validDocument) })
        : client.post<DesignDocument>(root, { body, decode: (v) => decode(v, validDocument) });
    },
    /** `POST /api/catalogue/designs/{id}/duplicate`: copies a catalogue. */
    duplicate: (id: string): Promise<Result<DesignDocument, ApiFailure>> =>
      client.post<DesignDocument>(`${path(id)}/duplicate`, { body: {}, decode: (v) => decode(v, validDocument) }),
    /** `DELETE /api/catalogue/designs/{id}?expectedRevision=`: archives a catalogue at its revision. */
    archive: (id: string, revision: number): Promise<Result<void, ApiFailure>> =>
      client.delete(`${path(id)}?expectedRevision=${revision}`, { response: 'empty' }),
    /** `GET /api/catalogue/designs/{id}/releases`: the catalogue's prepared and published editions. */
    releases: (id: string): Promise<Result<ReleaseSummary[], ApiFailure>> =>
      client.get<ReleaseSummary[]>(`${path(id)}/releases`, {
        decode: (v) => decode(v, Array.isArray, (list: Record<string, unknown>[]) => list.map(releaseSummary)),
      }),
    /** `POST /api/catalogue/designs/{id}/withdraw`: withdraws every published edition. */
    withdraw: (id: string, revision: number): Promise<Result<void, ApiFailure>> =>
      client.post(`${path(id)}/withdraw`, { body: { expectedRevision: revision }, response: 'empty' }),
    /** `GET /api/catalogue/designs/{id}/revisions`: the catalogue's saved revisions. */
    revisions: (id: string): Promise<Result<DesignRevision[], ApiFailure>> =>
      client.get<DesignRevision[]>(`${path(id)}/revisions`, { decode: (v) => decode(v, Array.isArray) }),
    /** `GET /api/catalogue/designs/{id}/revisions/{revision}`: one saved revision's document. */
    revision: (id: string, revision: number): Promise<Result<DesignDocument, ApiFailure>> =>
      client.get<DesignDocument>(`${path(id)}/revisions/${revision}`, { decode: (v) => decode(v, validDocument) }),
    /** `GET /api/catalogue/designs/{id}/preparation`: the revision, fingerprint and PDF pages a release must carry. */
    preparation: (id: string): Promise<Result<Preparation, ApiFailure>> =>
      client.get<Preparation>(`${path(id)}/preparation`, {
        decode: (v) =>
          decode(
            v,
            (v) =>
              object(v) &&
              typeof v.fingerprint === 'string' &&
              Number.isSafeInteger(v.designRevision) &&
              Array.isArray(v.pdfPageIds),
          ),
      }),
    /**
     * `POST /api/catalogue/designs/{id}/prepare`: prepares a release. A web-only edition sends the metadata as JSON; one
     * with a PDF sends it as multipart, with the whole PDF and each page's PDF and PNG, the browser setting the boundary.
     */
    prepare(
      id: string,
      metadata: { expectedRevision: number; fingerprint: string },
      bundle?: CatalogueRenderBundle,
    ): Promise<Result<PreparedRelease, ApiFailure>> {
      if (!bundle)
        return client.post<PreparedRelease>(`${path(id)}/prepare`, {
          body: metadata,
          decode: (v) => decode(v, isRelease),
        });
      const form = new FormData();
      form.append('metadata', JSON.stringify(metadata));
      form.append('files', new File([bundle.pdf], 'catalogue.pdf', { type: 'application/pdf' }));
      for (const page of bundle.pages) {
        form.append('files', new File([page.pdf], `page-${page.id}.pdf`, { type: 'application/pdf' }));
        form.append('files', new File([page.png], `page-${page.id}.png`, { type: 'image/png' }));
      }
      return client.post<PreparedRelease>(`${path(id)}/prepare`, { body: form, decode: (v) => decode(v, isRelease) });
    },
    /** `POST /api/catalogue/designs/{id}/releases/{release}/publish`: publishes a prepared release. */
    publish: (id: string, release: string, revision: number): Promise<Result<PreparedRelease, ApiFailure>> =>
      client.post<PreparedRelease>(`${path(id)}/releases/${encodeURIComponent(release)}/publish`, {
        body: { expectedRevision: revision },
        decode: (v) => decode(v, object),
      }),
  };
}
export type DesignApi = ReturnType<typeof createDesignApi>;
export const designApi = createDesignApi();
