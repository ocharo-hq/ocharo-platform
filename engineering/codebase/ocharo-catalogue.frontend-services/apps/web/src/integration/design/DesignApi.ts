import {
  ApiFailureFactory,
  createApiClient,
  rawEnvelope,
  type ResponseEnvelope,
} from '@wow-two-beta/ui-vue/foundation/http';
import { ResultExtensions } from '@wow-two-beta/ui-vue/foundation/results';
import { describeFailure } from '../catalogue/CatalogueApi';
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
const envelope: ResponseEnvelope = {
  unwrap: rawEnvelope.unwrap,
  toFailure: (parsed, response) => ApiFailureFactory.create('http', response, object(parsed) ? parsed : null),
};
function decode<T>(value: unknown, check: (v: unknown) => boolean) {
  return check(value) ? ResultExtensions.ok(value as T) : ResultExtensions.fail(ApiFailureFactory.create('protocol'));
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
/** The retained Catalogue API is the sole writer; all requests remain same-origin and local. */
export function createDesignApi(fetchImplementation: typeof fetch = globalThis.fetch) {
  const client = createApiClient({ envelope, retry: false, fetch: fetchImplementation }),
    root = '/api/catalogue/designs',
    path = (id: string) => `${root}/${encodeURIComponent(id)}`;
  async function unwrap<T>(
    promise: Promise<{ ok: true; value: T } | { ok: false; failure: Parameters<typeof describeFailure>[0] }>,
  ): Promise<T> {
    const result = await promise;
    if (!result.ok) throw new Error(describeFailure(result.failure));
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
          decode: (v) => decode(v, (v) => object(v) && Array.isArray(v.products) && object(v.settings)),
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
      unwrap(client.get<ReleaseSummary[]>(`${path(id)}/releases`, { decode: (v) => decode(v, Array.isArray) })),
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
      const value: unknown = await response.json().catch(() => null);
      if (!response.ok)
        throw new Error(describeFailure(ApiFailureFactory.create('http', response, object(value) ? value : null)));
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
export const designApi = createDesignApi();
