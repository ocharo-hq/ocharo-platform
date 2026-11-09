import { ApiFailureFactory, type ApiClient, type ApiFailure } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import type {
  BulkResult,
  CatalogueSettings,
  CollectionDetail,
  CollectionNames,
  ImportReport,
  ProductDetail,
  ProductDocument,
  ProductFilter,
  ProductCopyRow,
  ProductInput,
  ProductLocales,
  ProductSummary,
  StockRow,
  PublicCatalogue,
  RevisionSummary,
  SettingsEnvelope,
} from '../../domain/catalogue/CatalogueModels';
import {
  collectionDetail,
  productCopyRow,
  productDetail,
  productDocument,
  productSummary,
  settingsEnvelope,
  stockRow,
} from './CatalogueWire';
import { catalogueHttp } from '../common/IdentitySession';

/**
 * Structural guard over the `data` of the API's success envelope: accepts the expected shape and maps it to the app's
 * model, restoring the null fields the API's JSON leaves out; anything else is a protocol failure.
 */
function shape<T>(check: (value: unknown) => boolean, map: (value: never) => T = (value) => value as T) {
  return (value: unknown) =>
    check(value)
      ? ResultExtensions.ok(map(value as never))
      : ResultExtensions.fail(ApiFailureFactory.create('protocol'));
}
function list<T>(map: (value: never) => T) {
  return (values: unknown[]) => values.map((value) => map(value as never));
}
const isObject = (value: unknown) => typeof value === 'object' && value !== null && !Array.isArray(value);
const isArray = (value: unknown) => Array.isArray(value);
const hasDocument = (value: unknown) => isObject(value) && isObject((value as { document?: unknown }).document);

/** Maps request failures to safe interface copy; diagnostic ProblemDetails remain on the failure object. */
export function describeFailure(
  failure: ApiFailure,
  translate: (source: string, variables?: Record<string, string | number>) => string = (source, variables) =>
    source.replace(/\{(\w+)\}/g, (match, key: string) => String(variables?.[key] ?? match)),
): string {
  const messages: Record<number, string> = {
    400: 'Check the form fields and try again.',
    401: 'Your session is no longer valid. Sign in again.',
    403: 'You do not have permission to perform this action.',
    404: 'The requested item is unavailable. Refresh and try again.',
    409: 'This item changed elsewhere. Reload before saving.',
    422: 'Check the form fields and try again.',
    429: 'Too many requests. Wait a moment and try again.',
  };
  if (messages[failure.status]) return translate(messages[failure.status]!);
  if (failure.code === 'transport')
    return translate('Could not connect to the catalogue service. Check that it is running.');
  return failure.status > 0
    ? translate('The catalogue service returned error {status}.', { status: failure.status })
    : translate(failure.message);
}

/**
 * Same-origin catalogue endpoints over `client`, by default the session's {@link catalogueHttp}; mutations are never
 * retried automatically. Payloads arrive in the API's `{ data }` envelope and failures as ProblemDetails; the public feed
 * alone keeps its unenveloped shape. Reads take the caller's cancellation signal.
 */
export function createCatalogueApi(client: ApiClient = catalogueHttp) {
  const root = '/api/catalogue';
  const id = (value: string) => encodeURIComponent(value);
  return {
    /** `GET /api/catalogue/products`: the product grid for a filter. */
    listProducts(filter: ProductFilter, signal?: AbortSignal) {
      return client.get<ProductSummary[]>(`${root}/products`, {
        query: { ...filter },
        signal,
        decode: shape(isArray, list(productSummary)),
      });
    },
    /** `GET /api/catalogue/products/{id}`: one product with its document and revision. */
    getProduct(productId: string, signal?: AbortSignal) {
      return client.get<ProductDetail>(`${root}/products/${id(productId)}`, {
        signal,
        decode: shape(hasDocument, productDetail),
      });
    },
    /** `POST /api/catalogue/products`: creates a product. */
    createProduct(input: ProductInput) {
      return client.post<ProductDetail>(`${root}/products`, { body: input, decode: shape(hasDocument, productDetail) });
    },
    /** `PUT /api/catalogue/products/{id}`: saves a product against the revision it was edited from. */
    updateProduct(productId: string, input: ProductInput) {
      return client.put<ProductDetail>(`${root}/products/${id(productId)}`, {
        body: input,
        decode: shape(hasDocument, productDetail),
      });
    },
    /** `POST /api/catalogue/products/{id}/duplicate`: copies a product as a draft. */
    duplicateProduct(productId: string) {
      return client.post<ProductDetail>(`${root}/products/${id(productId)}/duplicate`, {
        body: {},
        decode: shape(hasDocument, productDetail),
      });
    },
    /** `GET /api/catalogue/products/stock`: every product's stock row. */
    stockRows(signal?: AbortSignal) {
      return client.get<StockRow[]>(`${root}/products/stock`, { signal, decode: shape(isArray, list(stockRow)) });
    },
    /** `PUT /api/catalogue/products/{id}/stock`: saves one product's stock. */
    saveStock(productId: string, expectedRevision: number, stock: Record<string, number> | null, madeToOrder: boolean) {
      return client.put<StockRow>(`${root}/products/${id(productId)}/stock`, {
        body: { expectedRevision, stock, madeToOrder },
        decode: shape(isObject, stockRow),
      });
    },
    /** `GET /api/catalogue/products/copy`: every product's copy row. */
    copyRows(signal?: AbortSignal) {
      return client.get<ProductCopyRow[]>(`${root}/products/copy`, {
        signal,
        decode: shape(isArray, list(productCopyRow)),
      });
    },
    /** `PUT /api/catalogue/products/{id}/copy`: saves one product's copy in every language. */
    saveCopy(productId: string, expectedRevision: number, locales: ProductLocales) {
      return client.put<ProductCopyRow>(`${root}/products/${id(productId)}/copy`, {
        body: { expectedRevision, locales },
        decode: shape(isObject, productCopyRow),
      });
    },
    /** `GET /api/catalogue/products/{id}/revisions`: a product's saved revisions. */
    revisions(productId: string, signal?: AbortSignal) {
      return client.get<RevisionSummary[]>(`${root}/products/${id(productId)}/revisions`, {
        signal,
        decode: shape(isArray),
      });
    },
    /** `GET /api/catalogue/products/{id}/revisions/{revision}`: one saved revision's document. */
    revision(productId: string, revision: number) {
      return client.get<ProductDocument>(`${root}/products/${id(productId)}/revisions/${revision}`, {
        decode: shape(isObject, productDocument),
      });
    },
    /** `POST /api/catalogue/products/{id}/revisions/{revision}/restore`: restores a revision as the next one. */
    restore(productId: string, revision: number, expectedRevision: number) {
      return client.post<ProductDetail>(`${root}/products/${id(productId)}/revisions/${revision}/restore`, {
        body: { expectedRevision },
        decode: shape(hasDocument, productDetail),
      });
    },
    /** `POST /api/catalogue/products/bulk/status`: sets the status of a selection. */
    bulkStatus(productIds: string[], status: string) {
      return client.post<BulkResult>(`${root}/products/bulk/status`, {
        body: { productIds, status },
        decode: shape(isObject),
      });
    },
    /** `POST /api/catalogue/products/bulk/pricing`: adjusts the prices of a selection. */
    bulkPricing(
      productIds: string[],
      mode: 'percent' | 'add',
      value: number,
      roundTo: number,
      tier: 'retail' | 'wholesale' | null,
    ) {
      return client.post<BulkResult>(`${root}/products/bulk/pricing`, {
        body: { productIds, mode, value, roundTo, tier },
        decode: shape(isObject),
      });
    },
    /**
     * `POST /api/catalogue/products/{id}/media`: a multipart image upload; the client sends `FormData` as it is, with the
     * browser's boundary.
     */
    uploadImage(productId: string, file: File, role: string, expectedRevision: number) {
      const form = new FormData();
      form.append('file', file);
      form.append('role', role);
      form.append('expectedRevision', String(expectedRevision));
      return client.post<ProductDetail>(`${root}/products/${id(productId)}/media`, {
        body: form,
        decode: shape(hasDocument, productDetail),
      });
    },
    /** `GET /api/catalogue/collections`: every collection. */
    listCollections(signal?: AbortSignal) {
      return client.get<CollectionDetail[]>(`${root}/collections`, {
        signal,
        decode: shape(isArray, list(collectionDetail)),
      });
    },
    /** `POST /api/catalogue/collections` creates a collection; `PUT /api/catalogue/collections/{id}` saves one. */
    saveCollection(
      collectionId: string | null,
      body: {
        expectedRevision: number | null;
        id?: string;
        names: CollectionNames;
        description: string | null;
        productIds: string[];
        isPublished: boolean;
        sortOrder: number;
      },
    ) {
      return collectionId
        ? client.put<CollectionDetail>(`${root}/collections/${id(collectionId)}`, {
            body,
            decode: shape(hasDocument, collectionDetail),
          })
        : client.post<CollectionDetail>(`${root}/collections`, { body, decode: shape(hasDocument, collectionDetail) });
    },
    /** `DELETE /api/catalogue/collections/{id}`: deletes a collection; its products remain. */
    deleteCollection(collectionId: string) {
      return client.delete(`${root}/collections/${id(collectionId)}`, { response: 'empty' });
    },
    /** `GET /api/catalogue/settings`: the catalogue settings, their revision and the import source. */
    getSettings(signal?: AbortSignal) {
      return client.get<SettingsEnvelope>(`${root}/settings`, { signal, decode: shape(isObject, settingsEnvelope) });
    },
    /** `PUT /api/catalogue/settings`: saves the settings against the revision they were edited from. */
    saveSettings(settings: CatalogueSettings, expectedRevision: number) {
      return client.put<SettingsEnvelope>(`${root}/settings`, {
        body: { settings, expectedRevision },
        decode: shape(isObject, settingsEnvelope),
      });
    },
    /** `POST /api/catalogue/import/workspace`: imports the configured products folder. */
    importWorkspace(updateExisting: boolean, includeReferenceImages: boolean) {
      return client.post<ImportReport>(`${root}/import/workspace`, {
        body: { sourceRoot: null, updateExisting, includeReferenceImages },
        decode: shape(isObject),
      });
    },
    /** `GET /api/public/catalogue`: the public feed, unenveloped, as Marketing and Blog read it. */
    publicCatalogue(locale: string, signal?: AbortSignal) {
      // The public feed keeps the unenveloped shape, with null fields written, that Marketing and Blog read.
      return client.get<PublicCatalogue>('/api/public/catalogue', {
        query: { locale },
        signal,
        unwrap: false,
        decode: shape(isObject),
      });
    },
  } as const;
}

export type CatalogueApi = ReturnType<typeof createCatalogueApi>;

export const catalogueApi = createCatalogueApi();

export const catalogueExports = {
  json: '/api/catalogue/export/catalogue.json',
  csv: '/api/catalogue/export/prices.csv',
  workspace: '/api/catalogue/export/workspace.zip',
} as const;
