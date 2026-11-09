import type {
  CatalogueSettings,
  CollectionDetail,
  CollectionDocument,
  CollectionNames,
  MediaView,
  PriceTier,
  ProductCopyRow,
  ProductDetail,
  ProductDocument,
  ProductLocale,
  ProductLocales,
  ProductMedia,
  ProductPricing,
  ProductSource,
  ProductSummary,
  SettingsEnvelope,
  StockRow,
} from '../../domain/catalogue/CatalogueModels';

/**
 * Wire shapes of the catalogue API: the SDK's JSON leaves a null field out, so each nullable field may be absent.
 * The mappers below restore it as `null`, the value the app's models read for "none".
 */
type Wire<T> = { [K in keyof T]: null extends T[K] ? T[K] | undefined : T[K] };
type Raw = Record<string, unknown>;

const orNull = <T>(value: T | null | undefined): T | null => (value === undefined ? null : value);
const objects = (value: unknown): Raw[] => (Array.isArray(value) ? (value as Raw[]) : []);

function productLocale(raw: Wire<ProductLocale>): ProductLocale {
  return {
    ...raw,
    name: orNull(raw.name),
    fabric: orNull(raw.fabric),
    decoration: orNull(raw.decoration),
    description: orNull(raw.description),
  };
}

function productLocales(raw: ProductLocales): ProductLocales {
  return {
    ...raw,
    uz: productLocale(raw.uz),
    ru: productLocale(raw.ru),
    en: productLocale(raw.en),
    ...(raw.additional
      ? {
          additional: Object.fromEntries(
            Object.entries(raw.additional).map(([code, locale]) => [code, productLocale(locale)]),
          ),
        }
      : {}),
  };
}

function priceTier(raw: Wire<PriceTier>): PriceTier {
  return { ...raw, maximumQuantity: orNull(raw.maximumQuantity) };
}

function productPricing(raw: Wire<ProductPricing>): ProductPricing {
  return { ...raw, wording: orNull(raw.wording), tiers: raw.tiers.map(priceTier) };
}

function productMedia(raw: Wire<ProductMedia>): ProductMedia {
  return {
    ...raw,
    width: orNull(raw.width),
    height: orNull(raw.height),
    alt: orNull(raw.alt),
    sourcePath: orNull(raw.sourcePath),
    group: orNull(raw.group),
  };
}

function productSource(raw: Wire<ProductSource>): ProductSource {
  return { ...raw, path: orNull(raw.path), sha256: orNull(raw.sha256), importedAt: orNull(raw.importedAt) };
}

/** Restores the absent null fields of a product document, nested copy, pricing, media and provenance included. */
export function productDocument(raw: Wire<ProductDocument>): ProductDocument {
  return {
    ...raw,
    designId: orNull(raw.designId),
    locales: productLocales(raw.locales),
    pricing: raw.pricing ? productPricing(raw.pricing) : null,
    media: objects(raw.media).map((media) => productMedia(media as Wire<ProductMedia>)),
    sourceVerbatim: orNull(raw.sourceVerbatim),
    source: raw.source ? productSource(raw.source) : null,
    notes: orNull(raw.notes),
    stock: orNull(raw.stock),
  };
}

function mediaView(raw: Wire<MediaView>): MediaView {
  return { ...raw, media: productMedia(raw.media), previewUrl: orNull(raw.previewUrl) };
}

/** Restores the absent null fields of a product with its media views. */
export function productDetail(raw: Wire<ProductDetail>): ProductDetail {
  return {
    ...raw,
    document: productDocument(raw.document),
    media: objects(raw.media).map((media) => mediaView(media as Wire<MediaView>)),
  };
}

/** Restores the absent null fields of a product grid row. */
export function productSummary(raw: Wire<ProductSummary>): ProductSummary {
  return {
    ...raw,
    nameUz: orNull(raw.nameUz),
    nameRu: orNull(raw.nameRu),
    nameEn: orNull(raw.nameEn),
    retailPrice: orNull(raw.retailPrice),
    wholesalePrice: orNull(raw.wholesalePrice),
    pricingStatus: orNull(raw.pricingStatus),
    primaryImageUrl: orNull(raw.primaryImageUrl),
    primaryPreviewUrl: orNull(raw.primaryPreviewUrl),
    stockTotal: orNull(raw.stockTotal),
  };
}

/** Restores the absent null fields of a stock sheet row. */
export function stockRow(raw: Wire<StockRow>): StockRow {
  return { ...raw, nameUz: orNull(raw.nameUz), previewUrl: orNull(raw.previewUrl), stock: orNull(raw.stock) };
}

/** Restores the absent null fields of a copy grid row. */
export function productCopyRow(raw: Wire<ProductCopyRow>): ProductCopyRow {
  return { ...raw, previewUrl: orNull(raw.previewUrl), locales: productLocales(raw.locales) };
}

function collectionNames(raw: Wire<CollectionNames>): CollectionNames {
  return { ...raw, ru: orNull(raw.ru), en: orNull(raw.en) };
}

function collectionDocument(raw: Wire<CollectionDocument>): CollectionDocument {
  return { ...raw, names: collectionNames(raw.names), description: orNull(raw.description) };
}

/** Restores the absent null fields of a collection. */
export function collectionDetail(raw: Wire<CollectionDetail>): CollectionDetail {
  return { ...raw, document: collectionDocument(raw.document) };
}

/** Restores the absent null fields of the catalogue settings. */
export function catalogueSettings(raw: Wire<CatalogueSettings>): CatalogueSettings {
  return { ...raw, priceNote: orNull(raw.priceNote) };
}

/** Restores the absent null fields of the settings with their revision and import folder. */
export function settingsEnvelope(raw: Wire<SettingsEnvelope>): SettingsEnvelope {
  return { ...raw, settings: catalogueSettings(raw.settings), sourceRoot: orNull(raw.sourceRoot) };
}
