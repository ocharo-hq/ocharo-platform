import { catalogueLanguages } from '../catalogue/CatalogueLanguages';
import type { PriceTier, PublicProduct, PublicImage } from '../catalogue/CatalogueModels';

export interface PublishedImage extends PublicImage {
  group?: string | null;
  isActive?: boolean;
}
export interface PublishedProduct extends Omit<PublicProduct, 'images'> {
  images: PublishedImage[];
}
export interface PublishedWebPage {
  id: string;
  title: string;
  layout: 'spread' | 'grid' | 'collage';
  productIds: string[];
}
export interface PublishedWeb {
  theme: 'editorial' | 'grid';
  pages: PublishedWebPage[];
  mediaGroup: string;
  priceDisplay: {
    show: boolean;
    showCurrency: boolean;
    showQuantity: boolean;
    showRetail: boolean;
    showWholesale: boolean;
  };
  copy: Record<string, string>;
  productMedia: Record<string, string[]>;
}
export interface PublishedPdfPage {
  id: string;
  number: number;
  title: string;
  widthMm: number;
  heightMm: number;
  productIds: string[];
  imageUrl: string;
  pdfUrl: string;
}
export interface PublishedCatalogue {
  id: string;
  slug: string;
  title: string;
  description: string;
  locale: string;
  updatedAt: string;
  availableLocales: string[];
  products: PublishedProduct[];
  web: PublishedWeb | null;
  pdf: { pdfUrl: string; pages: PublishedPdfPage[] } | null;
}
const uuid = /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i;
const token = /^[a-zA-Z0-9][a-zA-Z0-9_-]{0,99}$/;
const hash = /^[a-f0-9]{64}$/;
const record = (value: unknown): value is Record<string, unknown> =>
  !!value && typeof value === 'object' && !Array.isArray(value);
const strings = (value: unknown): string[] =>
  Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : [];
export const catalogueLanguageSupported = (value: string): boolean =>
  catalogueLanguages.some((language) => language.code === value);
export const validPublishedSlug = (value: string): boolean =>
  /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(value) && value.length <= 100;
export const validReleaseId = (value: string): boolean => uuid.test(value);

/** A release may link only its own same-origin, pinned PDF assets. */
function asset(value: unknown, slug: string, locale: string, release: string, file: string): value is string {
  if (typeof value !== 'string' || !value.startsWith('/api/public/catalogue/editions/')) return false;
  try {
    const url = new URL(value, 'https://catalogue.invalid');
    return (
      url.origin === 'https://catalogue.invalid' &&
      !url.hash &&
      url.pathname ===
        `/api/public/catalogue/editions/${encodeURIComponent(slug)}/assets/${encodeURIComponent(file)}` &&
      url.searchParams.get('locale') === locale &&
      url.searchParams.get('release') === release &&
      [...url.searchParams.keys()].length === 2
    );
  } catch {
    return false;
  }
}
function validPricing(value: unknown): value is NonNullable<PublishedProduct['pricing']> {
  if (
    !record(value) ||
    value.status !== 'confirmed' ||
    value.currency !== 'UZS' ||
    !Array.isArray(value.tiers) ||
    !value.tiers.length ||
    value.tiers.length > 100
  )
    return false;
  let prior: number | null = 0;
  for (const tier of value.tiers) {
    if (
      !record(tier) ||
      !Number.isSafeInteger(tier.minimumQuantity) ||
      (tier.minimumQuantity as number) < 1 ||
      prior === null ||
      (tier.minimumQuantity as number) <= prior ||
      !Number.isSafeInteger(tier.unitPrice) ||
      (tier.unitPrice as number) < 0 ||
      (tier.unitPrice as number) > 1_000_000_000 ||
      (tier.maximumQuantity !== null &&
        (!Number.isSafeInteger(tier.maximumQuantity) ||
          (tier.maximumQuantity as number) < (tier.minimumQuantity as number)))
    )
      return false;
    prior = tier.maximumQuantity as number | null;
  }
  return true;
}
/** Decoder rejects wrong-language/release data and removes anything outside the frozen public boundary. */
export function decodePublishedCatalogue(
  value: unknown,
  slug: string,
  locale: string,
  release?: string,
): PublishedCatalogue | null {
  if (
    !record(value) ||
    typeof value.id !== 'string' ||
    !uuid.test(value.id) ||
    (release && release !== value.id) ||
    value.slug !== slug ||
    value.locale !== locale ||
    !catalogueLanguageSupported(locale) ||
    typeof value.title !== 'string' ||
    !value.title.trim() ||
    typeof value.description !== 'string' ||
    typeof value.updatedAt !== 'string' ||
    !Array.isArray(value.products)
  )
    return null;
  const products: PublishedProduct[] = value.products
    .filter(record)
    .filter(
      (product) =>
        typeof product.id === 'string' &&
        token.test(product.id) &&
        product.localeStatus === 'approved' &&
        typeof product.name === 'string' &&
        product.name.trim() &&
        Array.isArray(product.images),
    )
    .map(
      (product) =>
        ({
          ...product,
          sizes: strings(product.sizes),
          tags: strings(product.tags),
          pricing: validPricing(product.pricing) ? product.pricing : null,
          images: (product.images as unknown[])
            .filter(record)
            .filter(
              (image) =>
                image.isActive !== false &&
                typeof image.sha256 === 'string' &&
                hash.test(image.sha256) &&
                image.url === `/api/catalogue/media/${image.sha256}`,
            )
            .map((image) => ({ ...image, previewUrl: null })),
        }) as unknown as PublishedProduct,
    )
    .filter((product) => product.images.length > 0);
  const ids = new Set(products.map((product) => product.id));
  let web: PublishedWeb | null = null;
  if (value.web !== null && value.web !== undefined) {
    const source = value.web;
    if (
      !record(source) ||
      !['editorial', 'grid'].includes(String(source.theme)) ||
      !Array.isArray(source.pages) ||
      !record(source.priceDisplay) ||
      !record(source.copy) ||
      !record(source.productMedia) ||
      !['all', 'product', 'model', 'detail', 'fabric'].includes(String(source.mediaGroup))
    )
      return null;
    const flags = ['show', 'showCurrency', 'showQuantity', 'showRetail', 'showWholesale'] as const;
    if (flags.some((flag) => typeof (source.priceDisplay as Record<string, unknown>)[flag] !== 'boolean')) return null;
    const pages: PublishedWebPage[] = source.pages
      .filter(record)
      .filter(
        (page) =>
          page.isActive !== false &&
          typeof page.id === 'string' &&
          token.test(page.id) &&
          typeof page.title === 'string' &&
          ['spread', 'grid', 'collage'].includes(String(page.layout)),
      )
      .map((page) => ({
        id: page.id as string,
        title: page.title as string,
        layout: page.layout as PublishedWebPage['layout'],
        productIds: [...new Set(strings(page.productIds).filter((id) => ids.has(id)))],
      }))
      .filter((page) => page.productIds.length > 0);
    const selections = source.productMedia;
    const productMedia = Object.fromEntries(
      products.map((product) => [
        product.id,
        strings(selections[product.id]).filter((sha) => product.images.some((image) => image.sha256 === sha)),
      ]),
    );
    if (pages.length)
      web = {
        theme: source.theme as PublishedWeb['theme'],
        pages,
        mediaGroup: source.mediaGroup as string,
        priceDisplay: source.priceDisplay as unknown as PublishedWeb['priceDisplay'],
        copy: Object.fromEntries(
          Object.entries(source.copy).filter((entry): entry is [string, string] => typeof entry[1] === 'string'),
        ),
        productMedia,
      };
  }
  let pdf: PublishedCatalogue['pdf'] = null;
  if (value.pdf !== null && value.pdf !== undefined) {
    const source = value.pdf;
    if (
      !record(source) ||
      !asset(source.pdfUrl, slug, locale, value.id, 'catalogue.pdf') ||
      !Array.isArray(source.pages) ||
      !source.pages.length
    )
      return null;
    const pages: PublishedPdfPage[] = [];
    for (const page of source.pages) {
      if (
        !record(page) ||
        typeof page.id !== 'string' ||
        !token.test(page.id) ||
        typeof page.title !== 'string' ||
        page.number !== pages.length + 1 ||
        typeof page.widthMm !== 'number' ||
        page.widthMm <= 0 ||
        typeof page.heightMm !== 'number' ||
        page.heightMm <= 0 ||
        !asset(page.imageUrl, slug, locale, value.id, `page-${page.id}.png`) ||
        !asset(page.pdfUrl, slug, locale, value.id, `page-${page.id}.pdf`)
      )
        return null;
      pages.push(page as unknown as PublishedPdfPage);
    }
    pdf = { pdfUrl: source.pdfUrl, pages };
  }
  if (!web && !pdf) return null;
  return {
    id: value.id,
    slug,
    title: value.title,
    description: value.description,
    locale,
    updatedAt: value.updatedAt,
    availableLocales: [...new Set([...strings(value.availableLocales).filter(catalogueLanguageSupported), locale])],
    products,
    web,
    pdf,
  };
}
export function pageProducts(edition: PublishedCatalogue, page: PublishedWebPage): PublishedProduct[] {
  return page.productIds
    .map((id) => edition.products.find((product) => product.id === id))
    .filter((product): product is PublishedProduct => !!product);
}
export function chosenImages(
  product: PublishedProduct,
  web: PublishedWeb,
  layout: PublishedWebPage['layout'],
): PublishedImage[] {
  return (web.productMedia[product.id] ?? [])
    .map((sha) => product.images.find((image) => image.sha256 === sha))
    .filter((image): image is PublishedImage => !!image)
    .slice(0, layout === 'grid' ? 1 : layout === 'collage' ? 6 : 3);
}
export function visiblePrices(product: PublishedProduct, web: PublishedWeb): PriceTier[] {
  if (!web.priceDisplay.show || !validPricing(product.pricing)) return [];
  return product.pricing.tiers.filter((tier) =>
    tier.minimumQuantity === 1 ? web.priceDisplay.showRetail : web.priceDisplay.showWholesale,
  );
}
