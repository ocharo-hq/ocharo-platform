import { describe, expect, it } from 'vitest';
import { emptyLocale, activationProblem } from '../src/domain/catalogue/CatalogueRules';
import {
  catalogueLanguages,
  ensureProductLocale,
  productLocale,
  translationReview,
} from '../src/domain/catalogue/CatalogueLanguages';
import type { ProductLocales, ProductMedia } from '../src/domain/catalogue/CatalogueModels';

describe('catalogue language review', () => {
  it('keeps missing translations missing instead of falling back to Uzbek', () => {
    const locales: ProductLocales = {
      uz: { ...emptyLocale(), name: 'Nimcha', status: 'approved' },
      ru: emptyLocale(),
      en: emptyLocale(),
    };
    expect(productLocale(locales, 'it')).toBeUndefined();
    expect(catalogueLanguages).toHaveLength(25);
    const italian = ensureProductLocale(locales, 'it');
    expect(italian.status).toBe('draft');
    expect(italian.name).toBeNull();
    expect(translationReview(locales)).toHaveLength(3);
    italian.name = 'Gilet';
    expect(translationReview(locales).find((item) => item.code === 'it')?.approved).toBe(false);
    italian.status = 'approved';
    expect(translationReview(locales).find((item) => item.code === 'it')?.approved).toBe(true);
    expect(locales.uz.name).toBe('Nimcha');
  });
  it('requires an active image rather than any retained media', () => {
    const locales = { uz: { ...emptyLocale(), name: 'Nimcha' }, ru: emptyLocale(), en: emptyLocale() };
    const media = { isActive: false } as ProductMedia;
    expect(activationProblem({ locales, media: [media] })).not.toBeNull();
    expect(activationProblem({ locales, media: [{ ...media, isActive: true }] })).toBeNull();
  });
});
