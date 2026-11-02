import type { APIRequestContext } from '@playwright/test';
import type { CatalogueLanguage } from '../../src/domain/catalogue/CatalogueLanguages';
import type { ProductDetail } from '../../src/domain/catalogue/CatalogueModels';
import { createCatalogue, emptyMetadata, productPage, refreshCatalogue } from '../../src/domain/design/CatalogueDesign';
import type { CatalogueWebPage, DesignDocument } from '../../src/domain/design/EditorModels';
import { createDesign, sourceProducts } from './catalogueApi';

export interface CatalogueSeed {
  title: string;
  slug: string;
  locale: CatalogueLanguage;
  outputs: { web: boolean; pdf: boolean };
  active?: boolean;
  description?: string;
  web?: { title: string; layout?: CatalogueWebPage['layout']; productIds: string[]; isActive?: boolean }[];
  /** Product pages after the cover, built with the designer's own page composition. */
  pdf?: { productId: string; layout?: 'product' | 'continuation' | 'collage' }[];
}

/**
 * Saves a catalogue through the API using the same domain composition the designers use, so a test starts from a
 * realistic saved revision and then exercises the designer itself.
 */
export async function seedCatalogue(
  request: APIRequestContext,
  seed: CatalogueSeed,
): Promise<{ document: DesignDocument; products: ProductDetail[] }> {
  const ids = [
    ...new Set([
      ...(seed.web ?? []).flatMap((page) => page.productIds),
      ...(seed.pdf ?? []).map((page) => page.productId),
    ]),
  ];
  const products = await sourceProducts(request, ids);
  const metadata = {
    ...emptyMetadata(seed.title),
    slug: seed.slug,
    description: seed.description ?? '',
    isActive: seed.active ?? true,
    outputs: seed.outputs,
  };
  metadata.web.pages = (seed.web ?? []).map((page, index) => ({
    id: `web-${index + 1}`,
    title: page.title,
    layout: page.layout ?? 'spread',
    isActive: page.isActive ?? true,
    productIds: page.productIds,
  }));
  let snapshot = createCatalogue(seed.title, seed.locale, metadata);
  for (const page of seed.pdf ?? []) {
    const product = products.find((item) => item.document.id === page.productId)!;
    snapshot.pages.push(productPage(product, snapshot, page.layout ?? 'product'));
  }
  snapshot = refreshCatalogue(snapshot, products);
  return { document: await createDesign(request, seed.title, snapshot), products };
}
