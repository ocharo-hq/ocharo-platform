import type { ProductDetail } from '../catalogue/CatalogueModels';
import { cloneSnapshot, type DesignElement, type DesignSnapshot } from './EditorModels';
import { catalogueSettings, defaultCopy, refreshCatalogue } from './CatalogueDesign';
import catalogueEn from './templates/catalogue-en.json';
import catalogueUz from './templates/catalogue-uz.json';
import catalogueRu from './templates/catalogue-ru.json';
/** An explicit replacement retains original geometry, with only verified source-ID bindings. */
export function originalCatalogue(snapshot: DesignSnapshot, products: ProductDetail[]) {
  if (!['uz', 'ru', 'en'].includes(snapshot.locale))
    throw new Error('The original design is available in Uzbek, Russian and English.');
  const seeds = { uz: catalogueUz, ru: catalogueRu, en: catalogueEn };
  const source = cloneSnapshot(seeds[snapshot.locale as keyof typeof seeds] as DesignSnapshot);
  source.catalogue = snapshot.catalogue;
  source.catalogueSettings = catalogueSettings(snapshot);
  const missing = [...new Set(source.pages.flatMap((page) => page.productIds ?? []))].filter(
    (id) => !products.some((product) => product.document.id === id),
  );
  if (missing.length) throw new Error('The original design requires products: ' + missing.join(', '));
  return connectCatalogueProducts(source, products);
}
export function connectCatalogueProducts(
  snapshot: DesignSnapshot,
  products: ProductDetail[],
): { snapshot: DesignSnapshot; warnings: string[] } {
  const next = cloneSnapshot(snapshot),
    settings = catalogueSettings(next),
    warnings = new Set<string>();
  next.catalogueSettings = settings;
  settings.copyByLocale[next.locale] ??= defaultCopy(next.locale);
  for (const page of next.pages) {
    const original = catalogueEn.pages.find((item) => item.id === page.id);
    if (!original) {
      if (!page.catalogueSource && !page.elements.some((item) => item.binding))
        warnings.add(`Connect unlinked fields manually: ${page.title}.`);
      continue;
    }
    const referenceElements = original.elements as DesignElement[];
    for (const element of page.elements) {
      const reference = referenceElements.find((item) => item.id === element.id && item.type === element.type);
      if (!reference) continue;
      if (element.type === 'image' && element.role === 'photo' && !element.productId) {
        const id = original.productIds.find((productId) => reference.label.includes(productId));
        if (id && products.some((item) => item.document.id === id)) element.productId = id;
      }
      if (element.type !== 'text' || element.binding) continue;
      const index = referenceElements.indexOf(reference),
        previous = referenceElements[index - 1];
      const singleProduct =
        original.productIds.length === 1
          ? products.find((item) => item.document.id === original.productIds[0])
          : undefined;
      const product = products.find((item) => item.document.id === reference.productId);
      if (reference.text === '2026 Collection') element.binding = { kind: 'shared', field: 'collection' };
      else if (singleProduct && reference.fontFamily === 'serif' && (reference.fontSize ?? 0) > 8)
        element.binding = { kind: 'product', field: 'name', productId: singleProduct.document.id };
      else if (singleProduct && reference.text?.startsWith('Sizes:'))
        element.binding = { kind: 'product', field: 'sizes', productId: singleProduct.document.id };
      else if (reference.text === 'Fabric:') element.binding = { kind: 'shared', field: 'materials' };
      else if (reference.text === 'Embellishment:') element.binding = { kind: 'shared', field: 'decoration' };
      else if (product && previous?.text === 'Fabric:')
        element.binding = { kind: 'product', field: 'fabric', productId: product.document.id };
      else if (product && previous?.text === 'Embellishment:')
        element.binding = { kind: 'product', field: 'decoration', productId: product.document.id };
      else if (product && reference.role === 'price') {
        const tierIndex =
          product.document.pricing?.tiers.findIndex(
            (tier) => tier.minimumQuantity === (reference.tier === 'bulk' ? 10 : 1),
          ) ?? -1;
        element.binding = {
          kind: 'product',
          field: 'price',
          index: tierIndex >= 0 ? tierIndex : reference.tier === 'bulk' ? 1 : 0,
          productId: product.document.id,
        };
      } else if (
        product &&
        (reference.text === '1 piece' || reference.text === '10+ pieces' || reference.text === 'UZS / piece')
      ) {
        const tierIndex =
          product.document.pricing?.tiers.findIndex(
            (tier) => tier.minimumQuantity === (reference.text === '10+ pieces' || previous?.tier === 'bulk' ? 10 : 1),
          ) ?? -1;
        element.binding = {
          kind: 'product',
          field: reference.text === 'UZS / piece' ? 'currency' : 'price-label',
          index: tierIndex >= 0 ? tierIndex : reference.text === '10+ pieces' || previous?.tier === 'bulk' ? 1 : 0,
          productId: product.document.id,
        };
      }
    }
  }
  const resolved = refreshCatalogue(next, products);
  return { snapshot: resolved, warnings: [...warnings] };
}

/** Exact original strings can switch core languages; hand-authored copy remains unchanged. */
export function localizeOriginalCopy(snapshot: DesignSnapshot, locale: DesignSnapshot['locale']): DesignSnapshot {
  const next = cloneSnapshot(snapshot),
    templates = { en: catalogueEn, uz: catalogueUz, ru: catalogueRu };
  const from = templates[snapshot.locale as keyof typeof templates],
    to = templates[locale as keyof typeof templates];
  if (!from || !to || locale === snapshot.locale) return next;
  for (const page of next.pages)
    for (const element of page.elements) {
      if (element.type !== 'text' || element.binding) continue;
      const original = from.pages
        .find((item) => item.id === page.id)
        ?.elements.find((item) => item.id === element.id) as DesignElement | undefined;
      const translated = to.pages
        .find((item) => item.id === page.id)
        ?.elements.find((item) => item.id === element.id) as DesignElement | undefined;
      if (original?.text === element.text && translated?.text !== undefined) element.text = translated.text;
    }
  return next;
}
