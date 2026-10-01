/** Catalogue wire and view models, mirroring the OcharoCatalogue API documents. */
export type ProductStatus = 'draft' | 'active' | 'archived';
export type LocaleCode = 'uz' | 'ru' | 'en';
export type LocaleStatus = 'draft' | 'approved';
export type PricingStatus = 'draft' | 'confirmed';
export type IssueStatus = 'open' | 'resolved';
export type MediaRole = 'cutout' | 'model' | 'reference' | 'photo' | 'detail' | 'fabric';

export interface ProductLocale {
  status: LocaleStatus;
  name: string | null;
  fabric: string | null;
  decoration: string | null;
  description: string | null;
}

export interface ProductLocales {
  uz: ProductLocale;
  ru: ProductLocale;
  en: ProductLocale;
}

export interface PriceTier {
  minimumQuantity: number;
  maximumQuantity: number | null;
  unitPrice: number;
}

export interface ProductPricing {
  currency: string;
  status: PricingStatus;
  tiers: PriceTier[];
  wording: string | null;
}

export interface ProductMedia {
  id: string;
  role: MediaRole;
  fileName: string;
  contentType: string;
  byteLength: number;
  sha256: string;
  width: number | null;
  height: number | null;
  hasPreview: boolean;
  alt: string | null;
  sourcePath: string | null;
}

export interface ReviewIssue {
  id: string;
  field: string;
  status: IssueStatus;
  question: string;
}

export interface ProductSource {
  kind: string;
  path: string | null;
  sha256: string | null;
  importedAt: string | null;
}

export interface ProductDocument {
  id: string;
  garmentType: string;
  variantCode: string;
  designId: string | null;
  status: ProductStatus;
  sortOrder: number;
  sizes: string[];
  fabricCodes: string[];
  decorationCodes: string[];
  tags: string[];
  locales: ProductLocales;
  pricing: ProductPricing | null;
  media: ProductMedia[];
  issues: ReviewIssue[];
  sourceVerbatim: string | null;
  source: ProductSource | null;
  notes: string | null;
  stock: Record<string, number> | null;
  madeToOrder: boolean;
  availability: Availability;
  availableSizes: string[];
}

export type Availability = 'in-stock' | 'sold-out' | 'made-to-order' | 'unknown';

/** One row of the stock sheet. */
export interface StockRow {
  id: string;
  garmentType: string;
  status: ProductStatus;
  revision: number;
  nameUz: string | null;
  previewUrl: string | null;
  sizes: string[];
  stock: Record<string, number> | null;
  madeToOrder: boolean;
  availability: Availability;
}

export interface MediaView {
  media: ProductMedia;
  url: string;
  previewUrl: string | null;
}

export interface ProductDetail {
  document: ProductDocument;
  revision: number;
  createdAt: string;
  updatedAt: string;
  media: MediaView[];
  collectionIds: string[];
}

export interface ProductSummary {
  id: string;
  garmentType: string;
  variantCode: string;
  status: ProductStatus;
  sortOrder: number;
  nameUz: string | null;
  nameRu: string | null;
  nameEn: string | null;
  retailPrice: number | null;
  wholesalePrice: number | null;
  pricingStatus: PricingStatus | null;
  localesApproved: number;
  openIssues: number;
  mediaCount: number;
  primaryImageUrl: string | null;
  primaryPreviewUrl: string | null;
  tags: string[];
  revision: number;
  updatedAt: string;
  availability: Availability;
  stockTotal: number | null;
}

export interface RevisionSummary {
  revision: number;
  savedAt: string;
  summary: string;
}

export interface CollectionNames {
  uz: string;
  ru: string | null;
  en: string | null;
}

export interface CollectionDocument {
  id: string;
  names: CollectionNames;
  description: string | null;
  productIds: string[];
  isPublished: boolean;
  sortOrder: number;
}

export interface CollectionDetail {
  document: CollectionDocument;
  revision: number;
  updatedAt: string;
}

export interface GarmentType {
  key: string;
  uz: string;
  ru: string;
  en: string;
}

export interface CatalogueSettings {
  brandName: string;
  currency: string;
  garmentTypes: GarmentType[];
  sizePreset: string[];
  contactLines: string[];
  priceNote: string | null;
}

export interface SettingsEnvelope {
  settings: CatalogueSettings;
  revision: number;
  sourceRoot: string | null;
}

export interface BulkResult {
  changed: number;
  skipped: string[];
}

export interface ImportReport {
  sourceRoot: string;
  scanned: number;
  created: number;
  updated: number;
  unchanged: number;
  imagesStored: number;
  warnings: string[];
}

export interface PublicImage {
  role: MediaRole;
  url: string;
  previewUrl: string | null;
  width: number | null;
  height: number | null;
  alt: string | null;
  sha256: string;
}

export interface PublicPricing {
  currency: string;
  status: PricingStatus;
  retail: number | null;
  wholesale: number | null;
  tiers: PriceTier[];
  wording: string | null;
}

export interface PublicProduct {
  id: string;
  garmentType: string;
  garmentName: string;
  variantCode: string;
  name: string | null;
  fabric: string | null;
  decoration: string | null;
  description: string | null;
  localeStatus: LocaleStatus;
  sizes: string[];
  pricing: PublicPricing | null;
  images: PublicImage[];
  tags: string[];
  availability: Availability;
  availableSizes: string[] | null;
}

export interface PublicCollection {
  id: string;
  name: string;
  description: string | null;
  productIds: string[];
}

export interface PublicCatalogue {
  brand: string;
  currency: string;
  locale: LocaleCode;
  generatedAt: string;
  priceNote: string | null;
  garmentTypes: GarmentType[];
  collections: PublicCollection[];
  products: PublicProduct[];
}

/** Editable product payload sent on create/update. */
export interface ProductInput {
  expectedRevision?: number;
  id?: string;
  garmentType: string;
  variantCode?: string;
  designId?: string | null;
  status: ProductStatus;
  sortOrder: number;
  sizes: string[];
  fabricCodes: string[];
  decorationCodes: string[];
  tags: string[];
  locales: ProductLocales;
  pricing: ProductPricing | null;
  media?: { id: string; role: MediaRole; alt: string | null }[];
  issues: ReviewIssue[];
  notes: string | null;
  stock?: Record<string, number> | null;
  madeToOrder?: boolean;
  changeSummary?: string;
}

export interface ProductFilter {
  q?: string;
  garmentType?: string;
  status?: string;
  collection?: string;
  tag?: string;
}

/** One row of the copy grid. */
export interface ProductCopyRow {
  id: string;
  garmentType: string;
  status: ProductStatus;
  revision: number;
  previewUrl: string | null;
  locales: ProductLocales;
}
