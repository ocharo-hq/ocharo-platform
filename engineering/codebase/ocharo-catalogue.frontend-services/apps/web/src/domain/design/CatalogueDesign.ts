import { catalogueLanguages, productLocale, type CatalogueLanguage } from '../catalogue/CatalogueLanguages';
import type {
  CatalogueSettings as ProductSettings,
  ProductDetail,
  ProductMedia,
  ProductPricing,
} from '../catalogue/CatalogueModels';
import {
  cloneSnapshot,
  validCatalogueSlug,
  type CatalogueCopy,
  type CatalogueMediaGroup,
  type CatalogueMetadata,
  type CatalogueSettings,
  type DesignElement,
  type DesignPage,
  type DesignSnapshot,
} from './EditorModels';

export interface DesignSource {
  products: ProductDetail[];
  settings: ProductSettings;
}
export const copyFields = [
  'collection',
  'coverNote',
  'header',
  'footer',
  'sizes',
  'pieces',
  'priceOnRequest',
  'prices',
  'materials',
  'decoration',
] as const;
export const mediaGroups = ['all', 'model', 'product', 'detail', 'fabric'] as const;
export const priceFields = ['show', 'showCurrency', 'showQuantity', 'showRetail', 'showWholesale'] as const;
const coreCopy: Partial<Record<CatalogueLanguage, CatalogueCopy>> = {
  en: {
    collection: '',
    coverNote: '',
    header: '',
    footer: '',
    sizes: 'Sizes',
    pieces: 'pieces',
    priceOnRequest: 'Price on request',
    prices: 'Prices',
    materials: 'Fabric',
    decoration: 'Embellishment',
  },
  uz: {
    collection: '',
    coverNote: '',
    header: '',
    footer: '',
    sizes: 'O‘lchamlar',
    pieces: 'dona',
    priceOnRequest: 'Narx so‘rov bo‘yicha',
    prices: 'Narxlar',
    materials: 'Mato',
    decoration: 'Bezak',
  },
  ru: {
    collection: '',
    coverNote: '',
    header: '',
    footer: '',
    sizes: 'Размеры',
    pieces: 'шт.',
    priceOnRequest: 'Цена по запросу',
    prices: 'Цены',
    materials: 'Ткань',
    decoration: 'Отделка',
  },
};
export function defaultCopy(locale: CatalogueLanguage): CatalogueCopy {
  return {
    ...(coreCopy[locale] ?? {
      collection: '',
      coverNote: '',
      header: '',
      footer: '',
      sizes: '',
      pieces: '',
      priceOnRequest: '',
      prices: '',
      materials: '',
      decoration: '',
    }),
  };
}
export function catalogueSettings(snapshot?: DesignSnapshot): CatalogueSettings {
  return snapshot?.catalogueSettings
    ? (JSON.parse(JSON.stringify(snapshot.catalogueSettings)) as CatalogueSettings)
    : {
        copyByLocale: {},
        mediaGroup: 'all',
        priceDisplay: { show: true, showCurrency: true, showQuantity: true, showRetail: true, showWholesale: true },
      };
}
export function catalogueCopy(snapshot: DesignSnapshot): CatalogueCopy {
  return { ...defaultCopy(snapshot.locale), ...snapshot.catalogueSettings?.copyByLocale[snapshot.locale] };
}
export function catalogueSlug(name: string): string {
  const value = name
    .toLowerCase()
    .normalize('NFKD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '')
    .slice(0, 100)
    .replace(/-+$/, '');
  return validCatalogueSlug(value) ? value : 'collection';
}
export function emptyMetadata(name = ''): CatalogueMetadata {
  return {
    slug: catalogueSlug(name),
    description: '',
    isActive: false,
    outputs: { web: true, pdf: true },
    web: { theme: 'editorial', pages: [] },
  };
}
export const mediaUrl = (media: ProductMedia) => `/api/catalogue/media/${media.sha256}`;
export function productMedia(product: ProductDetail, group: CatalogueMediaGroup = 'all'): ProductMedia[] {
  return product.document.media.filter(
    (image) =>
      image.isActive !== false &&
      image.contentType.toLowerCase().startsWith('image/') &&
      (group === 'all' ||
        (image.group ??
          (image.role === 'model'
            ? 'model'
            : image.role === 'detail'
              ? 'detail'
              : image.role === 'fabric'
                ? 'fabric'
                : 'product')) === group),
  );
}
export function productEligible(product: ProductDetail, locale: CatalogueLanguage): boolean {
  const copy = productLocale(product.document.locales, locale);
  return (
    product.document.status === 'active' &&
    copy?.status === 'approved' &&
    !!copy.name?.trim() &&
    !product.document.issues.some((issue) => issue.status === 'open') &&
    productMedia(product).length > 0
  );
}
export function productName(product: ProductDetail, locale: CatalogueLanguage): string {
  return productLocale(product.document.locales, locale)?.name || product.document.id;
}
export function pageProductIds(page: DesignPage): string[] {
  return [
    ...new Set(
      [
        ...(page.productIds ?? []),
        page.catalogueSource?.productId,
        ...page.elements.flatMap((element) => [
          element.productId,
          element.binding?.kind === 'product' ? element.binding.productId : undefined,
        ]),
      ].filter((id): id is string => !!id),
    ),
  ];
}
export function publicPricing(pricing: ProductPricing | null | undefined): boolean {
  if (
    !pricing ||
    pricing.status !== 'confirmed' ||
    pricing.currency !== 'UZS' ||
    pricing.tiers.length < 1 ||
    pricing.tiers.length > 100
  )
    return false;
  const tiers = [...pricing.tiers].sort((a, b) => a.minimumQuantity - b.minimumQuantity);
  return tiers.every(
    (tier, index) =>
      Number.isSafeInteger(tier.minimumQuantity) &&
      tier.minimumQuantity >= 1 &&
      (tier.maximumQuantity === null ||
        (Number.isSafeInteger(tier.maximumQuantity) && tier.maximumQuantity >= tier.minimumQuantity)) &&
      Number.isFinite(tier.unitPrice) &&
      tier.unitPrice >= 0 &&
      tier.unitPrice <= 1_000_000_000 &&
      (!tiers[index + 1] ||
        (tier.maximumQuantity !== null && tier.maximumQuantity < tiers[index + 1]!.minimumQuantity)),
  );
}
export function eligiblePdf(snapshot: DesignSnapshot, products: ProductDetail[]) {
  const result = cloneSnapshot(snapshot),
    excluded: string[] = [],
    issues: { pageId: string; reason: string }[] = [];
  result.pages = result.pages.filter((page) => {
    const hashes = page.elements.flatMap((element) => {
      const hash = element.src?.match(/^\/api\/catalogue\/media\/([a-f0-9]{64})(?:\/preview)?$/)?.[1];
      return hash ? [hash] : [];
    });
    const explicit = pageProductIds(page),
      ids = explicit.length
        ? explicit
        : products
            .filter((product) => product.document.media.some((image) => hashes.includes(image.sha256)))
            .map((product) => product.document.id);
    const approved = ids.every((id) =>
      products.some((product) => product.document.id === id && productEligible(product, snapshot.locale)),
    );
    const imagesOkay = hashes.every((hash) =>
      products.some(
        (product) => ids.includes(product.document.id) && productMedia(product).some((image) => image.sha256 === hash),
      ),
    );
    const reason =
      page.isActive === false
        ? 'Inactive page'
        : !approved
          ? 'Products need language review'
          : !imagesOkay
            ? 'Selected image is inactive, unavailable or belongs to another product'
            : '';
    if (reason) {
      excluded.push(page.title);
      issues.push({ pageId: page.id, reason });
      return false;
    }
    return true;
  });
  return { snapshot: result, excluded, issues };
}
export function createCatalogue(title: string, locale: CatalogueLanguage, metadata: CatalogueMetadata): DesignSnapshot {
  const settings = catalogueSettings();
  settings.copyByLocale[locale] = { ...defaultCopy(locale), collection: title };
  const snapshot: DesignSnapshot = {
    schemaVersion: 1,
    kind: 'catalogue',
    templateId: 'ocharo-catalogue-v1',
    locale,
    catalogue: JSON.parse(JSON.stringify(metadata)) as CatalogueMetadata,
    catalogueSettings: settings,
    pages: [],
  };
  if (metadata.outputs.pdf) snapshot.pages.push(coverPage(title, metadata.description, locale));
  return snapshot;
}
export function coverPage(title: string, description: string, locale: CatalogueLanguage): DesignPage {
  const direction = catalogueLanguages.find((item) => item.code === locale)!.dir;
  return {
    id: crypto.randomUUID(),
    title: title || 'Cover',
    width: 210,
    height: 297,
    background: '#f6f0e5',
    isActive: true,
    elements: [
      {
        id: crypto.randomUUID(),
        type: 'text',
        label: 'Catalogue title',
        role: 'title',
        text: title,
        binding: { kind: 'shared', field: 'collection' },
        direction,
        x: 18,
        y: 28,
        width: 174,
        height: 65,
        fontFamily: 'serif',
        fontSize: 16,
        lineHeight: 1.2,
        color: '#302b25',
      },
      {
        id: crypto.randomUUID(),
        type: 'text',
        label: 'Catalogue introduction',
        text: description,
        direction,
        x: 18,
        y: 228,
        width: 174,
        height: 50,
        fontFamily: 'sans',
        fontSize: 4,
        lineHeight: 1.4,
        color: '#302b25',
      },
    ],
  };
}
export function amount(value: number, locale: CatalogueLanguage): string {
  return new Intl.NumberFormat(locale, { maximumFractionDigits: 0 }).format(value);
}
export function productText(element: DesignElement, product: ProductDetail, snapshot: DesignSnapshot): string {
  const binding = element.binding;
  if (binding?.kind !== 'product') return element.text ?? '';
  const words = productLocale(product.document.locales, snapshot.locale),
    copy = catalogueCopy(snapshot),
    settings = catalogueSettings(snapshot),
    pricing = product.document.pricing;
  if (['name', 'fabric', 'decoration', 'description'].includes(binding.field))
    return words?.[binding.field as 'name' | 'fabric' | 'decoration' | 'description'] ?? '';
  if (binding.field === 'sizes') return [copy.sizes, product.document.sizes.join(', ')].filter(Boolean).join(': ');
  if (binding.field === 'details' || binding.field === 'notes')
    return [words?.fabric, words?.decoration, words?.description, product.document.sizes.join(', ')]
      .filter(Boolean)
      .join('\n\n');
  const tier = pricing?.tiers[binding.index ?? 0],
    confirmed = publicPricing(pricing);
  if (binding.field === 'currency') return settings.priceDisplay.showCurrency && confirmed ? 'UZS' : '';
  if (binding.field === 'price')
    return confirmed && tier ? amount(tier.unitPrice, snapshot.locale) : copy.priceOnRequest;
  if (binding.field === 'price-label')
    return confirmed && tier
      ? [
          settings.priceDisplay.showQuantity
            ? `${tier.minimumQuantity}${tier.maximumQuantity === null ? '+' : tier.maximumQuantity === tier.minimumQuantity ? '' : `–${tier.maximumQuantity}`} ${copy.pieces}`
            : '',
          settings.priceDisplay.showCurrency ? 'UZS' : '',
        ]
          .filter(Boolean)
          .join(' · ')
      : '';
  return '';
}
/** Resolves canonical product fields into a copy; saved revisions are never mutated in place. */
export function refreshCatalogue(
  snapshot: DesignSnapshot,
  products: ProductDetail[],
  locale = snapshot.locale,
): DesignSnapshot {
  const next = cloneSnapshot(snapshot);
  next.productRevisions = Object.fromEntries(
    [...new Set(next.pages.flatMap(pageProductIds))].flatMap((id) => {
      const product = products.find((item) => item.document.id === id);
      return product ? [[id, product.revision]] : [];
    }),
  );
  next.locale = locale;
  next.catalogueSettings = catalogueSettings(next);
  next.catalogueSettings.copyByLocale[locale] ??= defaultCopy(locale);
  const copy = catalogueCopy(next);
  for (const page of next.pages)
    for (const element of page.elements) {
      if (element.type === 'text') element.direction = catalogueLanguages.find((item) => item.code === locale)!.dir;
      if (element.binding?.kind === 'shared') {
        element.text = copy[element.binding.field];
        continue;
      }
      if (element.binding?.kind === 'product') {
        const product = products.find(
          (item) =>
            item.document.id ===
            (element.binding?.kind === 'product'
              ? (element.binding.productId ?? element.productId)
              : element.productId),
        );
        if (!product) {
          element.text = '';
          continue;
        }
        element.text = productText(element, product, next);
        if (['price', 'price-label', 'currency'].includes(element.binding.field)) {
          const tier = product.document.pricing?.tiers[element.binding.index ?? 0],
            flags = next.catalogueSettings.priceDisplay;
          element.opacity =
            flags.show && (tier?.minimumQuantity === 1 ? flags.showRetail : flags.showWholesale) ? 1 : 0;
        }
      }
    }
  return next;
}
export function randomizeImages(
  snapshot: DesignSnapshot,
  products: ProductDetail[],
  pageId?: string,
  random: () => number = Math.random,
): DesignSnapshot {
  const next = cloneSnapshot(snapshot),
    group = catalogueSettings(next).mediaGroup;
  for (const page of next.pages) {
    if (pageId && page.id !== pageId) continue;
    for (const productId of pageProductIds(page)) {
      const product = products.find((item) => item.document.id === productId);
      if (!product) continue;
      const images = [...productMedia(product, group)];
      for (let i = images.length - 1; i > 0; i--) {
        const j = Math.floor(random() * (i + 1));
        [images[i], images[j]] = [images[j]!, images[i]!];
      }
      let index = 0;
      for (const element of page.elements.filter((item) => item.type === 'image' && item.productId === productId)) {
        const image = images[index++ % images.length];
        if (image) {
          element.src = mediaUrl(image);
          element.opacity = 1;
        } else element.opacity = 0;
      }
    }
  }
  return next;
}
/** A page retains its composition while choosing another active photo from its own product. */
export function replaceImage(
  snapshot: DesignSnapshot,
  pageId: string,
  elementId: string,
  sha: string,
  products: ProductDetail[],
): DesignSnapshot {
  const next = cloneSnapshot(snapshot),
    element = next.pages.find((page) => page.id === pageId)?.elements.find((item) => item.id === elementId),
    product = products.find((item) => item.document.id === element?.productId);
  if (
    !element ||
    element.type !== 'image' ||
    !product ||
    !productMedia(product, catalogueSettings(next).mediaGroup).some((image) => image.sha256 === sha)
  )
    throw new Error('Choose an active image from this product and image group.');
  element.src = `/api/catalogue/media/${sha}`;
  element.opacity = 1;
  return next;
}
/** Composition uses the original catalogue palette and exact A4 geometry, with canonical field bindings. */
export function productPage(
  product: ProductDetail,
  snapshot: DesignSnapshot,
  layout: 'product' | 'continuation' | 'collage',
): DesignPage {
  const id = product.document.id,
    images = productMedia(product, catalogueSettings(snapshot).mediaGroup),
    elements: DesignElement[] = [];
  const addText = (
    field: 'name' | 'details' | 'price' | 'price-label',
    x: number,
    y: number,
    width: number,
    height: number,
    size: number,
    index?: number,
  ) =>
    elements.push({
      id: crypto.randomUUID(),
      type: 'text',
      label: field,
      productId: id,
      binding: { kind: 'product', field, productId: id, ...(index !== undefined ? { index } : {}) },
      x,
      y,
      width,
      height,
      fontSize: size,
      fontFamily: field === 'name' ? 'serif' : 'sans',
      lineHeight: 1.25,
      color: '#302b25',
    });
  elements.push({
    id: crypto.randomUUID(),
    type: 'text',
    label: 'Header',
    binding: { kind: 'shared', field: 'header' },
    x: 14,
    y: 10,
    width: 180,
    height: 8,
    fontSize: 4,
    fontFamily: 'serif',
    color: '#302b25',
  });
  addText('name', 14, 25, 182, 25, 8);
  const count = layout === 'collage' ? 6 : layout === 'continuation' ? 4 : 3,
    top = layout === 'product' ? 60 : 58,
    height = layout === 'product' ? 145 : 190;
  for (let i = 0; i < count; i++) {
    const columns = layout === 'product' ? 2 : 2,
      rows = Math.ceil(count / columns),
      large = layout === 'product' && i === 0;
    const x = layout === 'product' ? (large ? 14 : 120) : 14 + (i % columns) * 94;
    const y =
      layout === 'product'
        ? large
          ? top
          : top + (i - 1) * (height / 2 + 2)
        : top + Math.floor(i / columns) * (height / rows + 3);
    elements.push({
      id: crypto.randomUUID(),
      type: 'image',
      label: `Product image ${i + 1}`,
      productId: id,
      src: images[i % Math.max(1, images.length)] ? mediaUrl(images[i % images.length]!) : undefined,
      x,
      y,
      width: layout === 'product' ? (large ? 102 : 76) : 88,
      height: layout === 'product' ? (large ? height : height / 2 - 2) : height / rows - 3,
      fit: 'contain',
      cropX: 50,
      cropY: 50,
      opacity: images.length ? 1 : 0,
    });
  }
  if (layout === 'product') addText('details', 14, 211, 182, 36, 3.2);
  const tiers = product.document.pricing?.tiers ?? [];
  tiers.slice(0, 3).forEach((_, i) => {
    addText('price', 14 + i * 62, 254, 58, 10, 5, i);
    addText('price-label', 14 + i * 62, 266, 58, 8, 2.6, i);
  });
  elements.push({
    id: crypto.randomUUID(),
    type: 'text',
    label: 'Footer',
    binding: { kind: 'shared', field: 'footer' },
    x: 14,
    y: 284,
    width: 182,
    height: 7,
    fontSize: 2.5,
    fontFamily: 'sans',
    color: '#847562',
  });
  const page: DesignPage = {
    id: crypto.randomUUID(),
    title: productName(product, snapshot.locale),
    width: 210,
    height: 297,
    background: '#f6f0e5',
    isActive: true,
    productIds: [id],
    elements,
  };
  return refreshCatalogue({ ...snapshot, pages: [page] }, [product]).pages[0]!;
}
