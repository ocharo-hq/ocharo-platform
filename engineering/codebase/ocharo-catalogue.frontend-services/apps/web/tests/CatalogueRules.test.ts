import { describe, expect, it } from 'vitest';
import type { ProductDocument } from '../src/domain/catalogue/CatalogueModels';
import {
  activationProblem,
  emptyLocale,
  formatUzs,
  tierProblem,
  toInput,
} from '../src/domain/catalogue/CatalogueRules';

describe('catalogue rules', () => {
  it('formats UZS with plain spaces', () => {
    expect(formatUzs(130000)).toBe('130 000 so‘m');
    expect(formatUzs(null)).toBe('—');
  });

  it('rejects overlapping or open middle tiers', () => {
    expect(
      tierProblem([
        { minimumQuantity: 1, maximumQuantity: null, unitPrice: 1 },
        { minimumQuantity: 10, maximumQuantity: null, unitPrice: 1 },
      ]),
    ).toMatch(/overlap/);
    expect(
      tierProblem([
        { minimumQuantity: 1, maximumQuantity: 1, unitPrice: 130000 },
        { minimumQuantity: 10, maximumQuantity: null, unitPrice: 100000 },
      ]),
    ).toBeNull();
    expect(tierProblem([{ minimumQuantity: 0, maximumQuantity: null, unitPrice: 1 }])).toMatch(/at least 1/);
  });

  it('requires an Uzbek name and an image before activation', () => {
    const locales = { uz: emptyLocale(), ru: emptyLocale(), en: emptyLocale() };
    expect(activationProblem({ locales, media: [] })).toMatch(/Uzbek name/);
    locales.uz.name = 'Nimcha';
    expect(activationProblem({ locales, media: [] })).toMatch(/active image/);
  });

  it('copies a document into an independent edit payload', () => {
    const document: ProductDocument = {
      id: 'nimcha-v06',
      garmentType: 'nimcha',
      variantCode: 'v06',
      designId: null,
      status: 'active',
      sortOrder: 6,
      sizes: ['S'],
      fabricCodes: [],
      decorationCodes: [],
      tags: ['kuz'],
      locales: { uz: { ...emptyLocale(), name: 'Nimcha' }, ru: emptyLocale(), en: emptyLocale() },
      pricing: {
        currency: 'UZS',
        status: 'confirmed',
        tiers: [{ minimumQuantity: 1, maximumQuantity: 1, unitPrice: 110000 }],
        wording: null,
      },
      media: [
        {
          id: 'm1',
          role: 'cutout',
          fileName: 'a.png',
          contentType: 'image/png',
          byteLength: 1,
          sha256: 'x',
          width: 1,
          height: 1,
          hasPreview: true,
          alt: null,
          sourcePath: null,
        },
      ],
      issues: [],
      sourceVerbatim: 'Material: sintetika',
      source: null,
      notes: null,
      stock: { S: 3 },
      madeToOrder: false,
      availability: 'in-stock',
      availableSizes: ['S'],
    };
    const input = toInput(document, 4);
    input.tags.push('yangi');
    input.pricing!.tiers[0]!.unitPrice = 1;
    expect(document.tags).toEqual(['kuz']);
    expect(document.pricing!.tiers[0]!.unitPrice).toBe(110000);
    expect(input.expectedRevision).toBe(4);
    expect(input.media).toEqual([{ id: 'm1', role: 'cutout', alt: null }]);
    expect(input.stock).toEqual({ S: 3 });
    input.stock!['S'] = 0;
    expect(document.stock!['S']).toBe(3);
  });
});
