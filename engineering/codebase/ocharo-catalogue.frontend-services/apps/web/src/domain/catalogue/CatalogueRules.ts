import type {
  LocaleCode,
  MediaRole,
  ProductDocument,
  ProductInput,
  ProductLocale,
  ProductStatus,
} from './CatalogueModels';

/** Display vocabulary for the Uzbek operator interface. */
export const statusLabels: Record<ProductStatus, string> = {
  draft: 'Qoralama',
  active: 'Katalogda',
  archived: 'Arxivda',
};

export const localeLabels: Record<LocaleCode, string> = { uz: 'O‘zbekcha', ru: 'Ruscha', en: 'Inglizcha' };

export const roleLabels: Record<MediaRole, string> = {
  cutout: 'Fonsiz',
  model: 'Modelda',
  reference: 'Namuna',
  photo: 'Surat',
  detail: 'Detal',
  fabric: 'Mato',
};

export const locales: LocaleCode[] = ['uz', 'ru', 'en'];

export const availabilityLabels: Record<string, string> = {
  'in-stock': 'Sotuvda bor',
  'sold-out': 'Tugagan',
  'made-to-order': 'Buyurtma asosida',
  unknown: 'Qoldiq yuritilmaydi',
};

/** Formats an UZS amount with thin grouping: 130 000 so‘m. */
export function formatUzs(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—';
  return `${new Intl.NumberFormat('ru-RU').format(value).replace(/[\u00a0\u202f]/g, ' ')} so‘m`;
}

/** Why a product cannot be activated yet, or null. Mirrors the API rule. */
export function activationProblem(document: Pick<ProductDocument, 'locales' | 'media'>): string | null {
  if (!document.locales.uz.name?.trim()) return 'Faol mahsulotga o‘zbekcha nom kerak.';
  if (document.media.length === 0) return 'Faol mahsulotga kamida bitta rasm kerak.';
  return null;
}

/** An empty draft locale. */
export function emptyLocale(): ProductLocale {
  return { status: 'draft', name: null, fabric: null, decoration: null, description: null };
}

/** Converts a stored document to the editable payload. */
export function toInput(document: ProductDocument, expectedRevision?: number): ProductInput {
  return {
    expectedRevision,
    id: document.id,
    garmentType: document.garmentType,
    variantCode: document.variantCode,
    designId: document.designId,
    status: document.status,
    sortOrder: document.sortOrder,
    sizes: [...document.sizes],
    fabricCodes: [...document.fabricCodes],
    decorationCodes: [...document.decorationCodes],
    tags: [...document.tags],
    locales: structuredClone(document.locales),
    pricing: document.pricing ? structuredClone(document.pricing) : null,
    media: document.media.map((m) => ({ id: m.id, role: m.role, alt: m.alt })),
    issues: structuredClone(document.issues),
    notes: document.notes,
    stock: document.stock ? { ...document.stock } : null,
    madeToOrder: document.madeToOrder ?? false,
  };
}

/** Validates price tiers the same way the API does; returns a message or null. */
export function tierProblem(
  tiers: { minimumQuantity: number; maximumQuantity: number | null; unitPrice: number }[],
): string | null {
  const sorted = [...tiers].sort((a, b) => a.minimumQuantity - b.minimumQuantity);
  for (let i = 0; i < sorted.length; i++) {
    const tier = sorted[i]!;
    if (!Number.isInteger(tier.minimumQuantity) || tier.minimumQuantity < 1) return 'Miqdor 1 dan boshlanadi.';
    if (tier.maximumQuantity !== null && tier.maximumQuantity < tier.minimumQuantity)
      return 'Maksimal miqdor minimaldan kichik.';
    if (!Number.isFinite(tier.unitPrice) || tier.unitPrice < 0) return 'Narx noto‘g‘ri.';
    const next = sorted[i + 1];
    if (next && (tier.maximumQuantity === null || tier.maximumQuantity >= next.minimumQuantity))
      return 'Narx oraliqlari kesishmoqda; faqat oxirgisi ochiq bo‘lishi mumkin.';
  }
  return null;
}
