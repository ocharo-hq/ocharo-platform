import { catalogueLocales, productCopy, productMediaForGroup, type CatalogueLocale, type CommerceDraft, type CommerceImage, type Product } from './Commerce';
import { cloneSnapshot, validateSnapshot, type CatalogueBinding, type CatalogueCopy, type CatalogueMediaGroup, type CatalogueSettings, type DesignElement, type DesignPage, type DesignSnapshot } from './EditorModels';
import catalogueEn from './templates/catalogue-en.json';
import catalogueUz from './templates/catalogue-uz.json';
import catalogueRu from './templates/catalogue-ru.json';

export const catalogueCopyFields = ['collection', 'coverNote', 'header', 'footer', 'sizes', 'pieces', 'priceOnRequest', 'prices', 'materials', 'decoration'] as const;
export const catalogueMediaGroups = ['all', 'model', 'product', 'detail', 'fabric'] as const;
const coreCopy: Record<string, CatalogueCopy> = {
  en: { collection: '2026 Collection', coverNote: 'A collection, thoughtfully assembled.', header: 'Ocharo', footer: '', sizes: 'Sizes', pieces: 'pieces', priceOnRequest: 'Price on request', prices: 'Prices', materials: 'Fabric', decoration: 'Embellishment' },
  uz: { collection: '2026 kolleksiyasi', coverNote: 'Har bir tafsiloti o‘ylangan kolleksiya.', header: 'Ocharo', footer: '', sizes: 'O‘lchamlar', pieces: 'dona', priceOnRequest: 'Narx so‘rov bo‘yicha', prices: 'Narxlar', materials: 'Mato', decoration: 'Bezak' },
  ru: { collection: 'Коллекция 2026', coverNote: 'Коллекция, продуманная до мелочей.', header: 'Ocharo', footer: '', sizes: 'Размеры', pieces: 'шт.', priceOnRequest: 'Цена по запросу', prices: 'Цены', materials: 'Ткань', decoration: 'Отделка' },
};
/** New languages start empty so untranslated English is never presented as finished local copy. */
export function defaultCatalogueCopy(locale: CatalogueLocale): CatalogueCopy {
  return { ...(coreCopy[locale] ?? Object.fromEntries(catalogueCopyFields.map(key => [key, key === 'header' ? 'Ocharo' : ''])) as unknown as CatalogueCopy) };
}
export function catalogueSettings(snapshot?: DesignSnapshot): CatalogueSettings {
  return snapshot?.catalogueSettings ? JSON.parse(JSON.stringify(snapshot.catalogueSettings)) as CatalogueSettings : {
    copyByLocale: {}, mediaGroup: 'all', priceDisplay: { show: true, showCurrency: true, showQuantity: true, showRetail: true, showWholesale: true },
  };
}
export function catalogueCopy(snapshot: DesignSnapshot): CatalogueCopy {
  return { ...defaultCatalogueCopy(snapshot.locale), ...snapshot.catalogueSettings?.copyByLocale[snapshot.locale] };
}
export function catalogueProductId(element: DesignElement): string | undefined {
  return element.binding?.kind === 'product' ? element.binding.productId ?? element.productId : element.productId;
}
export function catalogueBrandId(snapshot: DesignSnapshot, page: DesignPage): string | undefined {
  return page.catalogueSource?.brandId ?? snapshot.catalogueSettings?.brandId;
}
export function catalogueMedia(product: Product, group: CatalogueMediaGroup): CommerceImage[] {
  const images = productMediaForGroup(product, group);
  return images.filter(image => ['image/png', 'image/jpeg', 'image/webp'].includes(image.contentType));
}
export function productBindingText(element: DesignElement, product: Product, locale: CatalogueLocale, copy: CatalogueCopy, settings: CatalogueSettings): string {
  const binding = element.binding;
  if (binding?.kind !== 'product') return element.text ?? '';
  const words = productCopy(product, locale), pricing = product.document.pricing;
  const sizes = product.document.sizes.length ? `${copy.sizes}: ${product.document.sizes.join(', ')}` : '';
  switch (binding.field) {
    case 'name': return words.name ?? '';
    case 'fabric': return words.fabric ?? '';
    case 'decoration': return words.decoration ?? '';
    case 'description': return words.description ?? '';
    case 'sizes': return sizes;
    case 'details': return [words.fabric, words.decoration, sizes].filter(Boolean).join('\n\n');
    case 'notes': return [words.fabric, words.decoration, words.description].filter(Boolean).join('\n');
    case 'currency': return settings.priceDisplay.showCurrency ? pricing?.currency ?? '' : '';
    case 'price': return pricing?.tiers[binding.index ?? 0] ? new Intl.NumberFormat(locale, { maximumFractionDigits: 2 }).format(pricing.tiers[binding.index ?? 0]!.unitPrice) : copy.priceOnRequest;
    case 'price-label': {
      const tier = pricing?.tiers[binding.index ?? 0];
      if (!tier) return '';
      return [settings.priceDisplay.showQuantity ? `${tier.minimumQuantity}${tier.maximumQuantity === null ? '+' : tier.maximumQuantity === tier.minimumQuantity ? '' : `–${tier.maximumQuantity}`} ${copy.pieces}` : '', settings.priceDisplay.showCurrency ? pricing?.currency : ''].filter(Boolean).join(' · ');
    }
  }
}
function showPrice(element: DesignElement, product: Product, settings: CatalogueSettings): boolean {
  if (element.binding?.kind !== 'product' || !['price','price-label','currency'].includes(element.binding.field)) return true;
  const tier = product.document.pricing?.tiers[element.binding.index ?? 0];
  return settings.priceDisplay.show && (tier?.minimumQuantity === 1 ? settings.priceDisplay.showRetail : settings.priceDisplay.showWholesale);
}
export interface CatalogueEditResult { snapshot: DesignSnapshot; warnings: string[] }
/** Resolves values into the saved snapshot; canonical changes never mutate an opened edition automatically. */
export function refreshCatalogue(snapshot: DesignSnapshot, drafts: CommerceDraft[], locale = snapshot.locale): CatalogueEditResult {
  const next = cloneSnapshot(snapshot), settings = catalogueSettings(next), warnings = new Set<string>();
  next.locale = locale; next.catalogueSettings = settings;
  settings.copyByLocale[locale] ??= defaultCatalogueCopy(locale);
  const copy = catalogueCopy(next);
  for (const page of next.pages) {
    const brand = catalogueBrandId(next, page), draft = drafts.find(item => item.brandId === brand);
    for (const element of page.elements) {
      if (element.type === 'text') element.direction = catalogueLocales.find(item => item.code === locale)?.dir ?? 'ltr';
      if (element.binding?.kind === 'shared') { element.text = copy[element.binding.field]; continue; }
      if (element.binding?.kind !== 'product') continue;
      const id = catalogueProductId(element), product = draft?.snapshot.products.find(item => item.document.id === id && item.document.status !== 'archived');
      if (!product) { warnings.add(`Product unavailable: ${id ?? 'unknown'}. Existing text retained.`); continue; }
      const text = productBindingText(element, product, locale, copy, settings);
      if (!text && ['name','fabric','description'].includes(element.binding.field)) warnings.add(`Missing ${locale} ${element.binding.field}: ${id}.`);
      element.text = text;
      if (['price','price-label','currency'].includes(element.binding.field)) element.opacity = showPrice(element, product, settings) ? 1 : 0;
      if (page.catalogueSource) { page.catalogueSource.revision = draft!.revision; page.catalogueSource.productRevision = product.revision; page.catalogueSource.currency = product.document.pricing?.currency ?? null; }
    }
    if (draft && settings.brandId === draft.brandId) settings.sourceRevision = draft.revision;
  }
  if (!copy.pieces || !copy.sizes || !copy.collection) warnings.add(`Complete the shared ${locale} catalogue text.`);
  return { snapshot: validateSnapshot(next), warnings: [...warnings] };
}
/** Selects only the frame's canonical product media; every other frame remains byte-for-byte unchanged. */
export function replaceCataloguePhoto(snapshot: DesignSnapshot, pageId: string, elementId: string, image: CommerceImage, drafts: CommerceDraft[], organizationId: string): DesignSnapshot {
  const next = cloneSnapshot(snapshot), page = next.pages.find(item => item.id === pageId), element = page?.elements.find(item => item.id === elementId && item.type === 'image' && item.role === 'photo');
  if (!page || !element) throw new Error('Select a product photograph.');
  const brand = catalogueBrandId(next, page), draft = drafts.find(item => item.brandId === brand);
  const product = draft?.snapshot.products.find(item => item.document.id === element.productId);
  const group = next.catalogueSettings?.mediaGroup ?? 'all';
  if (!product || !catalogueMedia(product, group).some(item => item.sha256 === image.sha256)) throw new Error('Choose an image from this product and media group.');
  element.src = `/api/organizations/${organizationId}/brands/${brand}/commerce/media/${image.sha256}`;
  element.label = image.alt || image.fileName;
  if (page.catalogueSource) page.catalogueSource.imageHashes = page.elements.filter(item => item.role === 'photo' && item.src?.includes('/commerce/media/')).map(item => item.src!.split('/').at(-1)!);
  return validateSnapshot(next);
}
/** One immutable result is one undo step, including a whole-catalogue shuffle. */
export function randomizeCatalogue(snapshot: DesignSnapshot, drafts: CommerceDraft[], organizationId: string, pageId?: string, random = Math.random): CatalogueEditResult {
  let next = cloneSnapshot(snapshot); const warnings = new Set<string>();
  const group = next.catalogueSettings?.mediaGroup ?? 'all';
  for (const page of next.pages.filter(item => !pageId || item.id === pageId)) {
    const brand = catalogueBrandId(next, page), draft = drafts.find(item => item.brandId === brand);
    const frames = page.elements.filter(element => element.type === 'image' && element.role === 'photo' && element.productId);
    for (const id of new Set(frames.map(frame => frame.productId!))) {
      const product = draft?.snapshot.products.find(item => item.document.id === id && item.document.status !== 'archived');
      const candidates = product ? [...catalogueMedia(product, group)] : [];
      if (!candidates.length) { warnings.add(`No ${group} images for ${id}; existing photographs retained.`); continue; }
      for (let index = candidates.length - 1; index > 0; index--) {
        const target = Math.floor(Math.max(0, Math.min(.999999, random())) * (index + 1));
        [candidates[index], candidates[target]] = [candidates[target]!, candidates[index]!];
      }
      const productFrames = frames.filter(frame => frame.productId === id);
      productFrames.forEach((frame, index) => { next = replaceCataloguePhoto(next, page.id, frame.id, candidates[index % candidates.length]!, drafts, organizationId); });
      if (candidates.length < productFrames.length) warnings.add(`${id}: ${candidates.length} images fill ${productFrames.length} frames; some repeat.`);
    }
    const target = next.pages.find(item => item.id === page.id);
    if (target?.catalogueSource) target.catalogueSource.mediaGroup = group;
  }
  return { snapshot: next, warnings: [...warnings] };
}
const originalTemplates = { en: catalogueEn, uz: catalogueUz, ru: catalogueRu };
/** Explicit, reversible adoption uses only stable IDs and the known v22 source relationships. */
export function connectCatalogueProducts(snapshot: DesignSnapshot, draft: CommerceDraft): CatalogueEditResult {
  const next = cloneSnapshot(snapshot), settings = catalogueSettings(next), warnings = new Set<string>();
  settings.brandId = draft.brandId; settings.sourceRevision = draft.revision; next.catalogueSettings = settings;
  settings.copyByLocale[next.locale] ??= defaultCatalogueCopy(next.locale);
  for (const page of next.pages) {
    const original = catalogueEn.pages.find(item => item.id === page.id);
    if (!original) { if (!page.catalogueSource && !page.elements.some(item => item.binding)) warnings.add(`Connect unlinked fields manually: ${page.title}.`); continue; }
    const referenceElements = original.elements as DesignElement[];
    for (const element of page.elements) {
      const reference = referenceElements.find(item => item.id === element.id && item.type === element.type);
      if (!reference) continue;
      if (element.type === 'image' && element.role === 'photo' && !element.productId) {
        const id = original.productIds.find(productId => reference.label.includes(productId));
        if (id && draft.snapshot.products.some(item => item.document.id === id)) element.productId = id;
      }
      if (element.type !== 'text' || element.binding) continue;
      const index = referenceElements.indexOf(reference), previous = referenceElements[index - 1];
      const singleProduct = original.productIds.length === 1 ? draft.snapshot.products.find(item => item.document.id === original.productIds[0]) : undefined;
      const product = draft.snapshot.products.find(item => item.document.id === reference.productId);
      if (reference.text === '2026 Collection') element.binding = { kind: 'shared', field: 'collection' };
      else if (singleProduct && reference.fontFamily === 'serif' && (reference.fontSize ?? 0) > 8) element.binding = {kind:'product',field:'name',productId:singleProduct.document.id};
      else if (singleProduct && reference.text?.startsWith('Sizes:')) element.binding = {kind:'product',field:'sizes',productId:singleProduct.document.id};
      else if (reference.text === 'Fabric:') element.binding = { kind: 'shared', field: 'materials' };
      else if (reference.text === 'Embellishment:') element.binding = { kind: 'shared', field: 'decoration' };
      else if (product && previous?.text === 'Fabric:') element.binding = { kind: 'product', field: 'fabric', productId: product.document.id };
      else if (product && previous?.text === 'Embellishment:') element.binding = { kind: 'product', field: 'decoration', productId: product.document.id };
      else if (product && reference.role === 'price') {
        const tierIndex = product.document.pricing?.tiers.findIndex(tier => tier.minimumQuantity === (reference.tier === 'bulk' ? 10 : 1)) ?? -1;
        if (tierIndex >= 0) element.binding = { kind: 'product', field: 'price', index: tierIndex, productId: product.document.id };
        else warnings.add(`Map the ${reference.tier ?? ''} price for ${product.document.id}.`);
      } else if (product && (reference.text === '1 piece' || reference.text === '10+ pieces' || reference.text === 'UZS / piece')) {
        const tierIndex = product.document.pricing?.tiers.findIndex(tier => tier.minimumQuantity === (reference.text === '10+ pieces' || previous?.tier === 'bulk' ? 10 : 1)) ?? -1;
        if (tierIndex >= 0) element.binding = { kind: 'product', field: reference.text === 'UZS / piece' ? 'currency' : 'price-label', index: tierIndex, productId: product.document.id };
      }
    }
  }
  const resolved = refreshCatalogue(next, [draft]);
  return { snapshot: resolved.snapshot, warnings: [...warnings, ...resolved.warnings] };
}
/** Optional manual bindings cover custom pages without guessing their intended product. */
export function bindCatalogueElement(snapshot: DesignSnapshot, pageId: string, elementId: string, binding: CatalogueBinding | null): DesignSnapshot {
  const next = cloneSnapshot(snapshot), element = next.pages.find(item => item.id === pageId)?.elements.find(item => item.id === elementId);
  if (!element || element.type !== 'text') throw new Error('Select a text frame.');
  if (binding) element.binding = binding; else delete element.binding;
  return validateSnapshot(next);
}
/** Known source text can switch core languages while hand-authored copy remains intact and visible for review. */
export function localizeUnboundTemplate(snapshot: DesignSnapshot, locale: CatalogueLocale): CatalogueEditResult {
  const next = cloneSnapshot(snapshot), warnings = new Set<string>();
  const from = originalTemplates[snapshot.locale as keyof typeof originalTemplates], to = originalTemplates[locale as keyof typeof originalTemplates];
  if (locale === snapshot.locale) return { snapshot: next, warnings: [] };
  for (const page of next.pages) for (const element of page.elements) {
    if (element.type !== 'text' || element.binding || element.role === 'page-number' || !element.text?.trim() || /^\d+$/.test(element.text)) continue;
    const original = from?.pages.find(item => item.id === page.id)?.elements.find(item => item.id === element.id) as DesignElement | undefined;
    const translated = to?.pages.find(item => item.id === page.id)?.elements.find(item => item.id === element.id) as DesignElement | undefined;
    if (original?.text === element.text && translated?.text !== undefined) element.text = translated.text;
    else warnings.add(`Review unlinked ${locale} text: ${page.title} / ${element.label}.`);
  }
  return { snapshot: next, warnings: [...warnings] };
}
