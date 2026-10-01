import { describe, expect, it } from 'vitest';
import { chosenImages, decodePublishedCatalogue, visiblePrices } from '../src/domain/public/PublishedCatalogue';
import { createPublishedCatalogueApi } from '../src/integration/public/PublishedCatalogueApi';

import { fixture, releaseId, firstHash, secondHash, pinned } from './PublishedCatalogueFixture';

describe('published catalogue boundary', () => {
  it('requires exact slug, language and immutable release when requested', () => {
    const source = fixture();
    expect(decodePublishedCatalogue(source, 'woven-days', 'en')?.availableLocales).toEqual(['en', 'fr']);
    expect(decodePublishedCatalogue(source, 'other', 'en')).toBeNull();
    expect(decodePublishedCatalogue(source, 'woven-days', 'fr')).toBeNull();
    expect(decodePublishedCatalogue(source, 'woven-days', 'en', '42')).toBeNull();
  });
  it('preserves chosen media order, explicit empty groups, and independent confirmed price flags', () => {
    const edition = decodePublishedCatalogue(fixture(), 'woven-days', 'en')!;
    expect(chosenImages(edition.products[0]!, edition.web!, 'spread').map((image) => image.sha256)).toEqual([
      secondHash,
      firstHash,
    ]);
    expect(visiblePrices(edition.products[0]!, edition.web!).map((tier) => tier.minimumQuantity)).toEqual([10]);
    edition.web!.productMedia['nimcha-v01'] = [];
    expect(chosenImages(edition.products[0]!, edition.web!, 'spread')).toEqual([]);
    edition.web!.priceDisplay.show = false;
    expect(visiblePrices(edition.products[0]!, edition.web!)).toEqual([]);
  });
  it('drops inactive images, unapproved products and empty pages without fetching live data', () => {
    const source = fixture();
    source.products[0]!.images[0]!.isActive = false;
    source.web.pages.push({ id: 'empty', title: 'Unconfirmed', layout: 'grid', productIds: ['unknown'] });
    const edition = decodePublishedCatalogue(source, 'woven-days', 'en')!;
    expect(edition.web!.pages).toHaveLength(1);
    expect(edition.products[0]!.images).toHaveLength(1);
    source.products[0]!.localeStatus = 'draft';
    expect(decodePublishedCatalogue(source, 'woven-days', 'en')?.web).toBeNull();
    expect(decodePublishedCatalogue(source, 'woven-days', 'en')?.products).toEqual([]);
  });
  it('hides draft, foreign currency and overlapping price tiers', () => {
    for (const change of [
      { status: 'draft' },
      { currency: 'USD' },
      {
        tiers: [
          { minimumQuantity: 1, maximumQuantity: null, unitPrice: 100 },
          { minimumQuantity: 10, maximumQuantity: null, unitPrice: 50 },
        ],
      },
    ]) {
      const source = fixture();
      Object.assign(source.products[0]!.pricing, change);
      expect(decodePublishedCatalogue(source, 'woven-days', 'en')!.products[0]!.pricing).toBeNull();
    }
  });
  it('rejects foreign or unpinned PDF download references', () => {
    for (const link of [
      'https://evil.example/file.pdf',
      pinned('catalogue.pdf').replace(releaseId, 'wrong'),
      pinned('catalogue.pdf') + '&token=secret',
    ]) {
      const source = fixture();
      source.pdf.pdfUrl = link;
      expect(decodePublishedCatalogue(source, 'woven-days', 'en')).toBeNull();
    }
  });
  it('uses only the read-only edition endpoint and validates the received locale', async () => {
    const calls: string[] = [];
    const api = createPublishedCatalogueApi(async (input) => {
      calls.push(String(input));
      return new Response(JSON.stringify(fixture()), { status: 200, headers: { 'Content-Type': 'application/json' } });
    });
    const success = await api.read('woven-days', 'en', releaseId);
    expect(success.ok).toBe(true);
    expect(calls[0]).toContain('/api/public/catalogue/editions/woven-days');
    expect(calls[0]).toContain(`release=${releaseId}`);
    const wrong = await api.read('woven-days', 'fr');
    expect(wrong.ok).toBe(false);
  });
});
