import { identityClient } from '../common/IdentitySessionClient';
import { ApiFailureFactory, createApiClient, wowTwoEnvelope, type ApiFailure } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import { describeFailure } from '../catalogue/CatalogueApi';
import { catalogueSettings, productDetail } from '../catalogue/CatalogueWire';
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
/** Retains request details until the application resolves its interface-language message. */
export class DesignRequestError extends Error {
  constructor(readonly failure: ApiFailure) {
    super(describeFailure(failure));
    this.name = 'DesignRequestError';
  }
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
 * The retained Catalogue API is the sole writer; all requests remain same-origin and local. Payloads arrive in the API's
 * `{ data }` envelope and failures as ProblemDetails.
 */
export function createDesignApi(fetchImplementation: typeof fetch = globalThis.fetch) {
  const client = createApiClient({ envelope: wowTwoEnvelope, retry: false, fetch: fetchImplementation }),
    root = '/api/catalogue/designs',
    path = (id: string) => `${root}/${encodeURIComponent(id)}`;
  async function unwrap<T>(
    promise: Promise<{ ok: true; value: T } | { ok: false; failure: Parameters<typeof describeFailure>[0] }>,
  ): Promise<T> {
    const result = await promise;
    if (!result.ok) throw new DesignRequestError(result.failure);
    return result.value;
  }
  return {
    list: (signal?: AbortSignal) =>
      unwrap(client.get<DesignSummary[]>(root, { signal, decode: (v) => decode(v, Array.isArray) })),
    get: (id: string, signal?: AbortSignal) =>
      unwrap(client.get<DesignDocument>(path(id), { signal, decode: (v) => decode(v, validDocument) })),
    source: (signal?: AbortSignal) =>
      unwrap(
        client.get<DesignSource>('/api/catalogue/design-source', {
          signal,
          decode: (v) => decode(v, (v) => object(v) && Array.isArray(v.products) && object(v.settings), designSource),
        }),
      ),
    save: (id: string | null, title: string, snapshot: DesignSnapshot, expectedRevision?: number) => {
      const body = {
        title,
        snapshot: validateSnapshot(snapshot),
        ...(expectedRevision !== undefined ? { expectedRevision } : {}),
      };
      return id
        ? unwrap(client.put<DesignDocument>(path(id), { body, decode: (v) => decode(v, validDocument) }))
        : unwrap(client.post<DesignDocument>(root, { body, decode: (v) => decode(v, validDocument) }));
    },
    duplicate: (id: string) =>
      unwrap(
        client.post<DesignDocument>(`${path(id)}/duplicate`, { body: {}, decode: (v) => decode(v, validDocument) }),
      ),
    archive: (id: string, revision: number) =>
      unwrap(client.delete(`${path(id)}?expectedRevision=${revision}`, { response: 'empty' })),
    releases: (id: string) =>
      unwrap(
        client.get<ReleaseSummary[]>(`${path(id)}/releases`, {
          decode: (v) => decode(v, Array.isArray, (list: Record<string, unknown>[]) => list.map(releaseSummary)),
        }),
      ),
    withdraw: (id: string, revision: number) =>
      unwrap(client.post(`${path(id)}/withdraw`, { body: { expectedRevision: revision }, response: 'empty' })),
    revisions: (id: string) =>
      unwrap(client.get<DesignRevision[]>(`${path(id)}/revisions`, { decode: (v) => decode(v, Array.isArray) })),
    revision: (id: string, revision: number) =>
      unwrap(
        client.get<DesignDocument>(`${path(id)}/revisions/${revision}`, { decode: (v) => decode(v, validDocument) }),
      ),
    preparation: (id: string) =>
      unwrap(
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
      ),
    async prepare(
      id: string,
      metadata: { expectedRevision: number; fingerprint: string },
      bundle?: CatalogueRenderBundle,
    ): Promise<PreparedRelease> {
      if (!bundle)
        return unwrap(
          client.post<PreparedRelease>(`${path(id)}/prepare`, {
            body: metadata,
            decode: (v) => decode(v, (v) => object(v) && typeof v.id === 'string'),
          }),
        );
      const form = new FormData();
      form.append('metadata', JSON.stringify(metadata));
      form.append('files', new File([bundle.pdf], 'catalogue.pdf', { type: 'application/pdf' }));
      for (const page of bundle.pages) {
        form.append('files', new File([page.pdf], `page-${page.id}.pdf`, { type: 'application/pdf' }));
        form.append('files', new File([page.png], `page-${page.id}.png`, { type: 'image/png' }));
      }
      let response: Response;
      try {
        response = await fetchImplementation(`${path(id)}/prepare`, { method: 'POST', body: form });
      } catch {
        throw new Error('Unable to connect to the catalogue service.');
      }
      const parsed: unknown = await response.json().catch(() => null);
      if (!response.ok) throw new DesignRequestError(wowTwoEnvelope.toFailure(parsed, response));
      const value = object(parsed) ? parsed.data : undefined;
      if (!object(value) || typeof value.id !== 'string')
        throw new Error('The catalogue service returned an invalid release.');
      return value as unknown as PreparedRelease;
    },
    publish: (id: string, release: string, revision: number) =>
      unwrap(
        client.post<PreparedRelease>(`${path(id)}/releases/${encodeURIComponent(release)}/publish`, {
          body: { expectedRevision: revision },
          decode: (v) => decode(v, object),
        }),
      ),
  };
}
export const designApi = createDesignApi(identityClient.fetch);
