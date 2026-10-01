import { ApiFailureFactory, createApiClient, rawEnvelope } from '@wow-two-beta/ui-vue/foundation/http';
import { ResultExtensions } from '@wow-two-beta/ui-vue/foundation/results';
import { decodePublishedCatalogue, type PublishedCatalogue } from '../../domain/public/PublishedCatalogue';

/** Read-only, exact-language published editions. No operator or draft endpoint is used. */
export function createPublishedCatalogueApi(fetchImplementation: typeof fetch = globalThis.fetch) {
  const client = createApiClient({ envelope: rawEnvelope, retry: false, fetch: fetchImplementation });
  return {
    read(slug: string, locale: string, release?: string, signal?: AbortSignal) {
      return client.get<PublishedCatalogue>(`/api/public/catalogue/editions/${encodeURIComponent(slug)}`, {
        query: { locale, ...(release ? { release } : {}) },
        signal,
        decode: (value) => {
          const decoded = decodePublishedCatalogue(value, slug, locale, release);
          return decoded ? ResultExtensions.ok(decoded) : ResultExtensions.fail(ApiFailureFactory.create('protocol'));
        },
      });
    },
  };
}
export type PublishedCatalogueApi = ReturnType<typeof createPublishedCatalogueApi>;
export const publishedCatalogueApi = createPublishedCatalogueApi();
