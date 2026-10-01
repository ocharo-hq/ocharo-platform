import {
  catalogueLanguages as catalogueLocales,
  type CatalogueLanguage as CatalogueLocale,
} from '../catalogue/CatalogueLanguages';

/** Portable, versioned design snapshots; all geometry uses millimetres. */
export type DesignKind = 'catalogue' | 'business-card';
export interface CatalogueCopy {
  collection: string;
  coverNote: string;
  header: string;
  footer: string;
  sizes: string;
  pieces: string;
  priceOnRequest: string;
  prices: string;
  materials: string;
  decoration: string;
}
export type CatalogueMediaGroup = 'all' | 'model' | 'product' | 'detail' | 'fabric';
export interface CatalogueSettings {
  copyByLocale: Partial<Record<CatalogueLocale, CatalogueCopy>>;
  mediaGroup: CatalogueMediaGroup;
  priceDisplay: {
    show: boolean;
    showCurrency: boolean;
    showQuantity: boolean;
    showRetail: boolean;
    showWholesale: boolean;
  };
  brandId?: string;
  sourceRevision?: number;
}
export type CatalogueBinding =
  | { kind: 'shared'; field: keyof CatalogueCopy }
  | {
      kind: 'product';
      field:
        | 'name'
        | 'fabric'
        | 'decoration'
        | 'description'
        | 'details'
        | 'notes'
        | 'sizes'
        | 'price'
        | 'price-label'
        | 'currency';
      index?: number;
      productId?: string;
    };
export interface DesignElement {
  id: string;
  binding?: CatalogueBinding;
  direction?: 'ltr' | 'rtl';
  type: 'text' | 'image' | 'shape';
  label: string;
  x: number;
  y: number;
  width: number;
  height: number;
  text?: string;
  src?: string;
  color?: string;
  background?: string;
  fontFamily?: 'serif' | 'sans';
  fontSize?: number; // millimetres, not CSS pixels
  fontWeight?: 'normal' | 'bold';
  align?: 'left' | 'center' | 'right';
  letterSpacing?: number;
  lineHeight?: number;
  fit?: 'cover' | 'contain';
  cropX?: number;
  cropY?: number; // 0..100 image position
  zoom?: number; // >=1
  opacity?: number;
  rotation?: number;
  locked?: boolean;
  productId?: string;
  groupId?: string;
  tier?: 'single' | 'bulk';
  priceLabel?: string;
  borderColor?: string;
  borderWidth?: number;
  radius?: number;
  role?: 'photo' | 'logo' | 'title' | 'price' | 'caption' | 'decoration' | 'page-number';
}
export interface CatalogueWebPage {
  id: string;
  title: string;
  layout: 'spread' | 'grid' | 'collage';
  isActive: boolean;
  productIds: string[];
}
export interface CatalogueMetadata {
  slug: string;
  description: string;
  isActive: boolean;
  outputs: { web: boolean; pdf: boolean };
  web: { theme: 'editorial' | 'grid'; pages: CatalogueWebPage[] };
}
export interface CatalogueSummary {
  slug: string;
  description: string;
  isActive: boolean;
  outputs: { web: boolean; pdf: boolean };
  web: { theme: 'editorial' | 'grid'; pageCount: number };
  pdfPageCount: number;
}
export interface DesignPage {
  isActive?: boolean;
  id: string;
  title: string;
  width: number;
  height: number;
  background: string;
  elements: DesignElement[];
  productIds?: string[];
  catalogueSource?: {
    brandId: string;
    revision: number;
    productRevision: number;
    productId: string;
    layout: 'product' | 'continuation' | 'collage';
    currency: string | null;
    imageHashes: string[];
    mediaGroup?: CatalogueMediaGroup;
  };
}
export interface DesignSnapshot {
  schemaVersion: 1;
  kind: DesignKind;
  templateId: string;
  locale: CatalogueLocale;
  productRevisions?: Record<string, number>;
  catalogueSettings?: CatalogueSettings;
  catalogue?: CatalogueMetadata;
  pages: DesignPage[];
}
export interface DesignSummary {
  locale?: CatalogueLocale | null;
  catalogue?: CatalogueSummary | null;
  id: string;
  kind?: DesignKind;
  title: string;
  revision: number;
  archived: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}
export interface DesignDocument extends DesignSummary {
  snapshot: DesignSnapshot;
}
export function cloneSnapshot(snapshot: DesignSnapshot): DesignSnapshot {
  return JSON.parse(JSON.stringify(snapshot)) as DesignSnapshot;
}
const reservedCatalogueSlugs = new Set([
  'api',
  'admin',
  'edit',
  'new',
  'pages',
  'editions',
  'collections',
  'products',
  'catalogue',
  'catalogues',
  'assets',
  'media',
  'download',
  'downloads',
  'settings',
  'login',
  'logout',
]);
export const validCatalogueSlug = (value: string): boolean =>
  value.length > 0 &&
  value.length <= 100 &&
  /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(value) &&
  !reservedCatalogueSlugs.has(value);
/** Bounds imported files before they become CSS or canvas instructions. */
export function validateSnapshot(value: unknown): DesignSnapshot {
  const d = value as DesignSnapshot;
  const metadata = (v: unknown, max = 256): v is string =>
    typeof v === 'string' &&
    v.trim().length > 0 &&
    v.length <= max &&
    ![...v].some((char) => char.charCodeAt(0) < 32 || char.charCodeAt(0) === 127);
  const color = (v: unknown): v is string => typeof v === 'string' && /^#[\da-f]{6}$/i.test(v);
  if (
    !d ||
    d.schemaVersion !== 1 ||
    !['catalogue', 'business-card'].includes(d.kind) ||
    !catalogueLocales.some((item) => item.code === d.locale) ||
    !metadata(d.templateId) ||
    !Array.isArray(d.pages) ||
    d.pages.length > 100 ||
    (d.pages.length < 1 && !(d.kind === 'catalogue' && d.catalogue))
  )
    throw new Error('Unsupported design file.');
  if (d.catalogue !== undefined) {
    const c = d.catalogue;
    if (
      d.kind !== 'catalogue' ||
      !c ||
      typeof c.slug !== 'string' ||
      !validCatalogueSlug(c.slug) ||
      typeof c.description !== 'string' ||
      c.description.length > 2000 ||
      typeof c.isActive !== 'boolean' ||
      !c.outputs ||
      typeof c.outputs.web !== 'boolean' ||
      typeof c.outputs.pdf !== 'boolean' ||
      (!c.outputs.web && !c.outputs.pdf) ||
      !c.web ||
      !['editorial', 'grid'].includes(c.web.theme) ||
      !Array.isArray(c.web.pages) ||
      c.web.pages.length > 100
    )
      throw new Error('Invalid catalogue metadata.');
    const webIds = new Set<string>();
    for (const page of c.web.pages) {
      if (
        !page ||
        !metadata(page.id, 100) ||
        !/^[a-zA-Z0-9_-]+$/.test(page.id) ||
        webIds.has(page.id) ||
        !metadata(page.title, 160) ||
        !['spread', 'grid', 'collage'].includes(page.layout) ||
        typeof page.isActive !== 'boolean' ||
        !Array.isArray(page.productIds) ||
        page.productIds.length > 1000 ||
        !page.productIds.every((id) => metadata(id, 120) && /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(id)) ||
        new Set(page.productIds).size !== page.productIds.length
      )
        throw new Error('Invalid web catalogue page.');
      webIds.add(page.id);
    }
  }
  if (
    d.productRevisions !== undefined &&
    (!d.productRevisions ||
      typeof d.productRevisions !== 'object' ||
      Array.isArray(d.productRevisions) ||
      Object.keys(d.productRevisions).length > 1000 ||
      Object.entries(d.productRevisions).some(
        ([id, revision]) =>
          !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(id) || id.length > 120 || !Number.isSafeInteger(revision) || revision < 1,
      ))
  )
    throw new Error('Invalid product revisions.');
  const groups = ['all', 'model', 'product', 'detail', 'fabric'];
  const copyFields = [
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
  ];
  if (d.catalogueSettings !== undefined) {
    const settings = d.catalogueSettings;
    if (
      d.kind !== 'catalogue' ||
      !settings ||
      !groups.includes(settings.mediaGroup) ||
      !settings.priceDisplay ||
      !['show', 'showCurrency', 'showQuantity', 'showRetail', 'showWholesale'].every(
        (key) => typeof settings.priceDisplay[key as keyof CatalogueSettings['priceDisplay']] === 'boolean',
      ) ||
      !settings.copyByLocale ||
      typeof settings.copyByLocale !== 'object' ||
      Array.isArray(settings.copyByLocale) ||
      Object.keys(settings.copyByLocale).length > catalogueLocales.length ||
      (settings.brandId !== undefined && !/^[a-f0-9-]{36}$/i.test(settings.brandId)) ||
      (settings.sourceRevision !== undefined &&
        (!Number.isSafeInteger(settings.sourceRevision) || settings.sourceRevision < 1))
    )
      throw new Error('Invalid catalogue settings.');
    for (const [locale, copy] of Object.entries(settings.copyByLocale)) {
      if (
        !catalogueLocales.some((item) => item.code === locale) ||
        !copy ||
        !copyFields.every(
          (key) =>
            typeof copy[key as keyof CatalogueCopy] === 'string' && copy[key as keyof CatalogueCopy].length <= 1000,
        )
      )
        throw new Error('Invalid catalogue copy.');
    }
  }
  const ids = new Set<string>();
  const number = (n: unknown, min: number, max: number) =>
    typeof n === 'number' && Number.isFinite(n) && n >= min && n <= max;
  for (const p of d.pages) {
    if (
      !p ||
      (p.isActive !== undefined && typeof p.isActive !== 'boolean') ||
      !metadata(p.id) ||
      ids.has(p.id) ||
      !metadata(p.title, 160) ||
      !number(p.width, 10, 1000) ||
      !number(p.height, 10, 1000) ||
      !color(p.background) ||
      (p.productIds !== undefined &&
        (!Array.isArray(p.productIds) ||
          p.productIds.length > 300 ||
          !p.productIds.every((id) => metadata(id)) ||
          new Set(p.productIds).size !== p.productIds.length)) ||
      !Array.isArray(p.elements) ||
      p.elements.length > 300
    )
      throw new Error('Invalid page in design file.');
    if (p.catalogueSource !== undefined) {
      const source = p.catalogueSource;
      if (
        !source ||
        !metadata(source.brandId) ||
        !metadata(source.productId) ||
        !Number.isSafeInteger(source.revision) ||
        source.revision < 1 ||
        !Number.isSafeInteger(source.productRevision) ||
        source.productRevision < 1 ||
        !['product', 'continuation', 'collage'].includes(source.layout) ||
        (source.mediaGroup !== undefined && !groups.includes(source.mediaGroup)) ||
        (source.currency !== null && !/^[A-Z]{3}$/.test(source.currency)) ||
        !Array.isArray(source.imageHashes) ||
        source.imageHashes.length > 6 ||
        !source.imageHashes.every((hash) => /^[a-f0-9]{64}$/.test(hash))
      )
        throw new Error('Invalid catalogue source.');
    }
    ids.add(p.id);
    const elements = new Set<string>();
    for (const e of p.elements) {
      if (
        !e ||
        !metadata(e.id) ||
        elements.has(e.id) ||
        !metadata(e.label) ||
        !['text', 'image', 'shape'].includes(e.type) ||
        !number(e.x, -1000, 1000) ||
        !number(e.y, -1000, 1000) ||
        !number(e.width, 0.1, 2000) ||
        !number(e.height, 0.1, 2000) ||
        (e.src !== undefined && !isSafeDesignSource(e.src)) ||
        (e.direction !== undefined && !['ltr', 'rtl'].includes(e.direction)) ||
        (e.fontFamily !== undefined && !['serif', 'sans'].includes(e.fontFamily)) ||
        (e.fontWeight !== undefined && !['normal', 'bold'].includes(e.fontWeight)) ||
        (e.align !== undefined && !['left', 'center', 'right'].includes(e.align)) ||
        (e.fit !== undefined && !['cover', 'contain'].includes(e.fit)) ||
        (e.locked !== undefined && typeof e.locked !== 'boolean') ||
        (e.productId !== undefined && !metadata(e.productId)) ||
        (e.groupId !== undefined && !metadata(e.groupId)) ||
        (e.tier !== undefined && !['single', 'bulk'].includes(e.tier)) ||
        (e.priceLabel !== undefined && !metadata(e.priceLabel)) ||
        (e.role !== undefined &&
          !['photo', 'logo', 'title', 'price', 'caption', 'decoration', 'page-number'].includes(e.role)) ||
        (e.text !== undefined && (typeof e.text !== 'string' || e.text.length > 10000)) ||
        (e.color !== undefined && !color(e.color)) ||
        (e.background !== undefined && !color(e.background)) ||
        (e.borderColor !== undefined && !color(e.borderColor))
      )
        throw new Error('Invalid element in design file.');
      if (e.binding !== undefined) {
        const binding = e.binding;
        if (
          d.kind !== 'catalogue' ||
          e.type !== 'text' ||
          !binding ||
          (binding.kind !== 'shared' && binding.kind !== 'product') ||
          (binding.kind === 'shared' && !copyFields.includes(binding.field)) ||
          (binding.kind === 'product' &&
            (![
              'name',
              'fabric',
              'decoration',
              'description',
              'details',
              'notes',
              'sizes',
              'price',
              'price-label',
              'currency',
            ].includes(binding.field) ||
              !metadata(binding.productId ?? e.productId) ||
              (binding.index !== undefined &&
                (!Number.isSafeInteger(binding.index) || binding.index < 0 || binding.index > 49)) ||
              (['price', 'price-label'].includes(binding.field) && binding.index === undefined)))
        )
          throw new Error('Invalid catalogue binding.');
      }
      for (const [key, min, max] of [
        ['fontSize', 0.5, 200],
        ['lineHeight', 0.5, 4],
        ['letterSpacing', -5, 30],
        ['cropX', 0, 100],
        ['cropY', 0, 100],
        ['zoom', 1, 5],
        ['opacity', 0, 1],
        ['rotation', -360, 360],
        ['radius', 0, 1000],
        ['borderWidth', 0, 20],
      ] as const) {
        if (e[key] !== undefined && !number(e[key], min, max)) throw new Error(`Invalid ${key}.`);
      }
      elements.add(e.id);
    }
  }
  return cloneSnapshot(d);
}
export function isSafeDesignSource(src: unknown): src is string {
  return (
    typeof src === 'string' &&
    (/^\/api\/catalogue\/media\/[a-f0-9]{64}(?:\/preview)?$/.test(src) ||
      /^\/templates\/catalogue-original\/assets\/[a-zA-Z0-9_-]+\.(?:png|jpg|jpeg|webp|svg)$/.test(src))
  );
}
