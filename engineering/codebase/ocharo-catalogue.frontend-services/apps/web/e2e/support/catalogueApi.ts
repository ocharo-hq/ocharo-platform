import { expect, type APIRequestContext } from '@playwright/test';
import type { ProductDetail } from '../../src/domain/catalogue/CatalogueModels';
import type { DesignSource } from '../../src/domain/design/CatalogueDesign';
import type { DesignDocument, DesignSnapshot, DesignSummary } from '../../src/domain/design/EditorModels';
import { syntheticPhoto, type Rgb } from './images';

/** Short, slug-safe token that keeps each test's products and catalogues apart in a shared store. */
export function runToken(): string {
  return `${Date.now().toString(36).slice(-4)}${Math.random().toString(36).slice(2, 6)}`;
}

export interface LocaleSeed {
  name: string;
  fabric?: string;
  decoration?: string;
  description?: string;
  /** Approved unless stated; a draft is a missing translation for catalogue output. */
  status?: 'approved' | 'draft';
}
export interface ImageSeed {
  color: Rgb;
  width?: number;
  height?: number;
  role?: 'cutout' | 'model' | 'photo' | 'detail' | 'fabric' | 'reference';
  group?: 'product' | 'model' | 'detail' | 'fabric';
}
export interface ProductSeed {
  variant: string;
  garment?: 'nimcha' | 'baska' | 'korset' | 'kostyum' | 'abaya' | 'look';
  locales: Record<string, LocaleSeed>;
  images?: ImageSeed[];
  /** UZS 1–9 and 10+ unit prices; null leaves the product without prices. */
  prices?: [number, number] | null;
  pricesConfirmed?: boolean;
  openQuestion?: string;
  sizes?: string[];
  status?: 'active' | 'draft';
}

const empty = { status: 'draft', name: null, fabric: null, decoration: null, description: null };
function locale(seed?: LocaleSeed) {
  return seed
    ? {
        status: seed.status ?? 'approved',
        name: seed.name,
        fabric: seed.fabric ?? null,
        decoration: seed.decoration ?? null,
        description: seed.description ?? null,
      }
    : empty;
}

async function json<T>(response: Awaited<ReturnType<APIRequestContext['get']>>, action: string): Promise<T> {
  if (!response.ok()) throw new Error(`${action} failed: ${response.status()} ${await response.text()}`);
  return (await response.json()) as T;
}

/** Creates an active product through the public product API, uploads its images and sets their groups. */
export async function seedProduct(request: APIRequestContext, seed: ProductSeed): Promise<ProductDetail> {
  const { uz, ru, en, ...additional } = seed.locales;
  const input = {
    garmentType: seed.garment ?? 'nimcha',
    variantCode: seed.variant,
    sizes: seed.sizes ?? ['S', 'M', 'L'],
    locales: {
      uz: locale(uz),
      ru: locale(ru),
      en: locale(en),
      additional: Object.fromEntries(Object.entries(additional).map(([code, value]) => [code, locale(value)])),
    },
    pricing:
      seed.prices === null
        ? null
        : {
            currency: 'UZS',
            status: seed.pricesConfirmed === false ? 'draft' : 'confirmed',
            tiers: [
              { minimumQuantity: 1, maximumQuantity: 9, unitPrice: (seed.prices ?? [420_000, 380_000])[0] },
              { minimumQuantity: 10, maximumQuantity: null, unitPrice: (seed.prices ?? [420_000, 380_000])[1] },
            ],
            wording: null,
          },
    issues: seed.openQuestion
      ? [{ id: 'e2e-question', field: 'general', status: 'open', question: seed.openQuestion }]
      : [],
  };
  let product = await json<ProductDetail>(
    await request.post('/api/catalogue/products', { data: { ...input, status: 'draft' } }),
    'Create product',
  );
  for (const [index, image] of (seed.images ?? [{ color: [180, 140, 120] }]).entries())
    product = await json<ProductDetail>(
      await request.post(`/api/catalogue/products/${product.document.id}/media`, {
        multipart: {
          file: {
            name: `${seed.variant}-${index + 1}.png`,
            mimeType: 'image/png',
            buffer: syntheticPhoto(image.color, image.width, image.height),
          },
          role: image.role ?? 'photo',
          expectedRevision: String(product.revision),
        },
      }),
      'Upload product image',
    );
  const groups = seed.images ?? [];
  return json<ProductDetail>(
    await request.put(`/api/catalogue/products/${product.document.id}`, {
      data: {
        ...input,
        expectedRevision: product.revision,
        status: seed.status ?? 'active',
        media: product.document.media.map((media, index) => ({
          id: media.id,
          role: media.role,
          alt: null,
          isActive: true,
          group: groups[index]?.group ?? null,
        })),
      },
    }),
    'Activate product',
  );
}

/** Changes one language's copy through the product copy endpoint, as the product editor's copy grid does. */
export async function editProductCopy(
  request: APIRequestContext,
  id: string,
  code: string,
  change: Partial<{ status: 'approved' | 'draft'; name: string; fabric: string; description: string }>,
): Promise<ProductDetail> {
  const current = await json<ProductDetail>(await request.get(`/api/catalogue/products/${id}`), 'Read product');
  const locales = JSON.parse(JSON.stringify(current.document.locales)) as ProductDetail['document']['locales'];
  const target = code === 'uz' || code === 'ru' || code === 'en' ? locales[code] : (locales.additional ?? {})[code];
  if (!target) throw new Error(`Product ${id} has no ${code} copy.`);
  Object.assign(target, change);
  return json<ProductDetail>(
    await request.put(`/api/catalogue/products/${id}/copy`, { data: { expectedRevision: current.revision, locales } }),
    'Update product copy',
  );
}

export const designSource = async (request: APIRequestContext) =>
  json<DesignSource>(await request.get('/api/catalogue/design-source'), 'Read design source');

export const listDesigns = async (request: APIRequestContext, archived = false) =>
  json<DesignSummary[]>(await request.get(`/api/catalogue/designs?archived=${archived}`), 'List catalogues');

export const getDesign = async (request: APIRequestContext, id: string) =>
  json<DesignDocument>(await request.get(`/api/catalogue/designs/${id}`), 'Read catalogue');

export async function createDesign(
  request: APIRequestContext,
  title: string,
  snapshot: DesignSnapshot,
): Promise<DesignDocument> {
  return json<DesignDocument>(
    await request.post('/api/catalogue/designs', { data: { title, snapshot } }),
    'Create catalogue',
  );
}

export async function saveDesign(request: APIRequestContext, document: DesignDocument): Promise<DesignDocument> {
  return json<DesignDocument>(
    await request.put(`/api/catalogue/designs/${document.id}`, {
      data: { title: document.title, snapshot: document.snapshot, expectedRevision: document.revision },
    }),
    'Save catalogue',
  );
}

export async function revisions(request: APIRequestContext, id: string) {
  return json<{ revision: number; title: string }[]>(
    await request.get(`/api/catalogue/designs/${id}/revisions`),
    'List revisions',
  );
}

export async function releases(request: APIRequestContext, id: string) {
  return json<{ id: string; designRevision: number; publishedAtUtc: string | null; withdrawnAtUtc: string | null }[]>(
    await request.get(`/api/catalogue/designs/${id}/releases`),
    'List releases',
  );
}

/** The products a test seeded, in the shape the designers receive from the design source. */
export async function sourceProducts(request: APIRequestContext, ids: string[]): Promise<ProductDetail[]> {
  const source = await designSource(request);
  const products = ids.map((id) => source.products.find((product) => product.document.id === id));
  expect(products.every(Boolean), 'every seeded product is in the design source').toBe(true);
  return products as ProductDetail[];
}
