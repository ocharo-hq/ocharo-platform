import type { ProductDetail } from '../src/domain/catalogue/CatalogueModels';
import { createCatalogue, emptyMetadata, type DesignSource } from '../src/domain/design/CatalogueDesign';
import type { DesignDocument } from '../src/domain/design/EditorModels';
export function productFixture(id = 'nimcha-v01'): ProductDetail {
  return {
    revision: 3,
    createdAt: '2026-10-01T00:00:00Z',
    updatedAt: '2026-10-01T00:00:00Z',
    media: [],
    collectionIds: [],
    document: {
      id,
      garmentType: 'nimcha',
      variantCode: 'v01',
      designId: null,
      status: 'active',
      sortOrder: 0,
      sizes: ['S', 'M'],
      fabricCodes: [],
      decorationCodes: [],
      tags: [],
      locales: {
        uz: { status: 'approved', name: 'Nimcha', fabric: 'Adras', decoration: null, description: 'O‘zbekcha' },
        ru: { status: 'approved', name: 'Жилет', fabric: 'Адрас', decoration: null, description: 'Русский' },
        en: {
          status: 'approved',
          name: 'Waistcoat',
          fabric: 'Adras',
          decoration: null,
          description: 'English description',
        },
        additional: {
          fr: { status: 'approved', name: 'Gilet', fabric: 'Adras', decoration: null, description: 'Français' },
        },
      },
      pricing: {
        currency: 'UZS',
        status: 'confirmed',
        wording: null,
        tiers: [
          { minimumQuantity: 1, maximumQuantity: 9, unitPrice: 150000 },
          { minimumQuantity: 10, maximumQuantity: null, unitPrice: 100000 },
        ],
      },
      media: Array.from({ length: 6 }, (_, index) => ({
        id: 'image-' + index,
        role: 'model' as const,
        group: 'model',
        isActive: true,
        fileName: 'model-' + index + '.png',
        contentType: 'image/png',
        byteLength: 1,
        sha256: String(index + 1).repeat(64),
        width: 100,
        height: 100,
        hasPreview: false,
        alt: 'Model ' + index,
        sourcePath: null,
      })),
      issues: [],
      sourceVerbatim: null,
      source: null,
      notes: null,
      stock: null,
      madeToOrder: false,
      availability: 'unknown',
      availableSizes: [],
    },
  };
}
export function sourceFixture(): DesignSource {
  return {
    products: [productFixture()],
    settings: {
      brandName: 'Ocharo',
      currency: 'UZS',
      garmentTypes: [],
      sizePreset: [],
      contactLines: [],
      priceNote: null,
    },
  };
}
export function documentFixture(): DesignDocument {
  const metadata = emptyMetadata('Woven days');
  metadata.isActive = true;
  return {
    id: '11111111-1111-1111-1111-111111111111',
    title: 'Woven days',
    revision: 2,
    archived: false,
    createdAtUtc: '2026-10-01T00:00:00Z',
    updatedAtUtc: '2026-10-01T00:00:00Z',
    snapshot: createCatalogue('Woven days', 'en', metadata),
  };
}
