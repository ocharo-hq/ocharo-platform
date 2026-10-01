/** Portable, versioned design snapshots; all geometry uses millimetres. */
export type DesignKind = 'catalogue' | 'business-card';
export interface DesignElement {
  id: string;
  type: 'text' | 'image' | 'shape';
  label: string;
  x: number; y: number; width: number; height: number;
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
  cropX?: number; cropY?: number; // 0..100 image position
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
export interface DesignPage {
  id: string; title: string; width: number; height: number; background: string;
  elements: DesignElement[];
  productIds?: string[];
  catalogueSource?: { brandId: string; revision: number; productRevision: number; productId: string; layout: 'product' | 'continuation' | 'collage'; currency: string | null; imageHashes: string[] };
}
export interface DesignSnapshot {
  schemaVersion: 1;
  kind: DesignKind;
  templateId: string;
  locale: 'uz' | 'ru' | 'en';
  pages: DesignPage[];
}
export interface DesignSummary {
  id: string; organizationId: string; kind: DesignKind; title: string;
  revision: number; archived: boolean; createdAtUtc: string; updatedAtUtc: string;
}
export interface DesignDocument extends DesignSummary { snapshot: DesignSnapshot }
export function cloneSnapshot(snapshot: DesignSnapshot): DesignSnapshot {
  return JSON.parse(JSON.stringify(snapshot)) as DesignSnapshot;
}
/** Bounds imported files before they become CSS or canvas instructions. */
export function validateSnapshot(value: unknown): DesignSnapshot {
  const d = value as DesignSnapshot;
  const metadata = (v: unknown, max = 256): v is string => typeof v === 'string' && v.trim().length > 0 && v.length <= max && !/[\u0000-\u001f\u007f]/.test(v);
  const color = (v: unknown): v is string => typeof v === 'string' && /^#[\da-f]{6}$/i.test(v);
  if (!d || d.schemaVersion !== 1 || !['catalogue', 'business-card'].includes(d.kind) ||
      !['uz', 'ru', 'en'].includes(d.locale) || !metadata(d.templateId) ||
      !Array.isArray(d.pages) || d.pages.length < 1 || d.pages.length > 100) throw new Error('Unsupported design file.');
  const ids = new Set<string>();
  const number = (n: unknown, min: number, max: number) => typeof n === 'number' && Number.isFinite(n) && n >= min && n <= max;
  for (const p of d.pages) {
    if (!p || !metadata(p.id) || ids.has(p.id) || !metadata(p.title, 160) ||
        !number(p.width, 10, 1000) || !number(p.height, 10, 1000) || !color(p.background) ||
        (p.productIds !== undefined && (!Array.isArray(p.productIds) || p.productIds.length > 300 ||
          !p.productIds.every((id) => metadata(id)) || new Set(p.productIds).size !== p.productIds.length)) ||
        !Array.isArray(p.elements) || p.elements.length > 300) throw new Error('Invalid page in design file.');
    if (p.catalogueSource !== undefined) {
      const source = p.catalogueSource;
      if (!source || !metadata(source.brandId) || !metadata(source.productId) ||
          !Number.isSafeInteger(source.revision) || source.revision < 1 ||
          !Number.isSafeInteger(source.productRevision) || source.productRevision < 1 ||
          !['product', 'continuation', 'collage'].includes(source.layout) ||
          (source.currency !== null && !/^[A-Z]{3}$/.test(source.currency)) ||
          !Array.isArray(source.imageHashes) || source.imageHashes.length > 6 ||
          !source.imageHashes.every(hash => /^[a-f0-9]{64}$/.test(hash))) throw new Error('Invalid catalogue source.');
    }
    ids.add(p.id);
    const elements = new Set<string>();
    for (const e of p.elements) {
      if (!e || !metadata(e.id) || elements.has(e.id) || !metadata(e.label) ||
          !['text', 'image', 'shape'].includes(e.type) || !number(e.x, -1000, 1000) || !number(e.y, -1000, 1000) ||
          !number(e.width, 0.1, 2000) || !number(e.height, 0.1, 2000) ||
          (e.src !== undefined && !isSafeDesignSource(e.src)) ||
          (e.fontFamily !== undefined && !['serif', 'sans'].includes(e.fontFamily)) ||
          (e.fontWeight !== undefined && !['normal', 'bold'].includes(e.fontWeight)) ||
          (e.align !== undefined && !['left', 'center', 'right'].includes(e.align)) ||
          (e.fit !== undefined && !['cover', 'contain'].includes(e.fit)) ||
          (e.locked !== undefined && typeof e.locked !== 'boolean') ||
          (e.productId !== undefined && !metadata(e.productId)) ||
          (e.groupId !== undefined && !metadata(e.groupId)) ||
          (e.tier !== undefined && !['single', 'bulk'].includes(e.tier)) ||
          (e.priceLabel !== undefined && !metadata(e.priceLabel)) ||
          (e.role !== undefined && !['photo', 'logo', 'title', 'price', 'caption', 'decoration', 'page-number'].includes(e.role)) ||
          (e.text !== undefined && (typeof e.text !== 'string' || e.text.length > 10000)) ||
          (e.color !== undefined && !color(e.color)) ||
          (e.background !== undefined && !color(e.background)) ||
          (e.borderColor !== undefined && !color(e.borderColor))) throw new Error('Invalid element in design file.');
      for (const [key, min, max] of [['fontSize', 0.5, 200], ['lineHeight', 0.5, 4], ['letterSpacing', -5, 30], ['cropX', 0, 100], ['cropY', 0, 100], ['zoom', 1, 5], ['opacity', 0, 1], ['rotation', -360, 360], ['radius', 0, 1000], ['borderWidth', 0, 20]] as const) {
        if (e[key] !== undefined && !number(e[key], min, max)) throw new Error(`Invalid ${key}.`);
      }
      elements.add(e.id);
    }
  }
  return cloneSnapshot(d);
}
export function isSafeDesignSource(src: unknown): src is string {
  return typeof src === 'string' && !src.split('/').some(part => part === '..' || part === '.') && ( /^\/templates\/[a-z\d/_\-.]+$/i.test(src) ||
    /^\/api\/organizations\/[a-f\d-]{36}\/media\/[a-f\d-]{36}\/content$/i.test(src) ||
    /^\/api\/organizations\/[a-f\d-]{36}\/brands\/[a-f\d-]{36}\/commerce\/media\/[a-f\d]{64}$/i.test(src));
}
