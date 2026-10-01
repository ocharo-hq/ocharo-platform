import { describe, expect, it } from 'vitest';
import {
  createCatalogue,
  emptyMetadata,
  productEligible,
  productMedia,
  productPage,
  refreshCatalogue,
  randomizeImages,
  replaceImage,
  eligiblePdf,
  publicPricing,
} from '../src/domain/design/CatalogueDesign';
import { validateSnapshot, isSafeDesignSource } from '../src/domain/design/EditorModels';
import originalEn from '../src/domain/design/templates/catalogue-en.json';
import { originalCatalogue } from '../src/domain/design/OriginalCatalogue';
import { productFixture, documentFixture } from './NativeDesignFixture';
describe('native catalogue design contracts', () => {
  it('starts extra languages with a neutral product-free cover', () => {
    const value = createCatalogue('Collection française', 'fr', emptyMetadata('collection-fr'));
    expect(value.locale).toBe('fr');
    expect(value.pages).toHaveLength(1);
    expect(value.pages[0]!.productIds).toBeUndefined();
    expect(JSON.stringify(value)).not.toMatch(/Synthetic poplin|abaya-v01|2026 Collection/);
    expect(validateSnapshot(value)).toEqual(value);
  });
  it('saves a web-only draft with zero PDF pages', () => {
    const meta = emptyMetadata('web');
    meta.outputs.pdf = false;
    expect(validateSnapshot(createCatalogue('Web', 'fr', meta)).pages).toEqual([]);
  });
  it('uses exact-language approval and a current active image', () => {
    const product = productFixture();
    expect(productEligible(product, 'fr')).toBe(true);
    expect(productEligible(product, 'ar')).toBe(false);
    product.document.media.forEach((image) => (image.isActive = false));
    expect(productEligible(product, 'en')).toBe(false);
  });
  it('groups legacy roles and omits inactive media', () => {
    const product = productFixture();
    product.document.media[0]!.group = null;
    product.document.media[0]!.role = 'cutout';
    product.document.media[1]!.isActive = false;
    expect(productMedia(product, 'product').map((image) => image.id)).toEqual(['image-0']);
    expect(productMedia(product, 'model')).toHaveLength(4);
  });
  it('refreshes canonical copy and records exact source revisions without changing page geometry', () => {
    const product = productFixture(),
      snapshot = documentFixture().snapshot;
    snapshot.pages = [productPage(product, snapshot, 'product')];
    const page = snapshot.pages[0]!,
      before = page.elements.map(({ x, y, width, height }) => ({ x, y, width, height }));
    const next = refreshCatalogue(snapshot, [product], 'fr');
    expect(next.productRevisions).toEqual({ 'nimcha-v01': 3 });
    expect(
      next.pages[0]!.elements.find((element) => element.binding?.kind === 'product' && element.binding.field === 'name')
        ?.text,
    ).toBe('Gilet');
    expect(next.pages[0]!.elements.map(({ x, y, width, height }) => ({ x, y, width, height }))).toEqual(before);
    expect(snapshot.locale).toBe('en');
  });
  it('never substitutes English into missing Arabic fields', () => {
    const product = productFixture(),
      snapshot = documentFixture().snapshot;
    snapshot.pages = [productPage(product, snapshot, 'product')];
    const next = refreshCatalogue(snapshot, [product], 'ar');
    expect(
      next.pages[0]!.elements.find((element) => element.binding?.kind === 'product' && element.binding.field === 'name')
        ?.text,
    ).toBe('');
    expect(
      next.pages[0]!.elements.filter((element) => element.type === 'text').every(
        (element) => element.direction === 'rtl',
      ),
    ).toBe(true);
  });
  it('randomizes only eligible group images while preserving crop and geometry', () => {
    const product = productFixture(),
      snapshot = documentFixture().snapshot;
    snapshot.pages = [productPage(product, snapshot, 'collage')];
    const first = snapshot.pages[0]!.elements.find((element) => element.type === 'image')!;
    first.cropX = 17;
    first.rotation = 9;
    const next = randomizeImages(snapshot, [product], undefined, () => 0);
    const after = next.pages[0]!.elements.find((element) => element.id === first.id)!;
    expect(after.src).not.toBe(first.src);
    expect([after.x, after.y, after.cropX, after.rotation]).toEqual([first.x, first.y, 17, 9]);
    expect(first.src).toContain('1'.repeat(64));
  });
  it('rejects a manual image from another product', () => {
    const product = productFixture(),
      snapshot = documentFixture().snapshot;
    snapshot.pages = [productPage(product, snapshot, 'product')];
    const image = snapshot.pages[0]!.elements.find((element) => element.type === 'image')!;
    expect(() => replaceImage(snapshot, snapshot.pages[0]!.id, image.id, 'f'.repeat(64), [product])).toThrow(
      /this product/,
    );
  });
  it('excludes a mixed PDF page as a whole when one product lacks approval', () => {
    const a = productFixture(),
      b = productFixture('baska-v01');
    b.document.locales.en.status = 'draft';
    const snapshot = documentFixture().snapshot;
    snapshot.pages.push(productPage(a, snapshot, 'product'));
    snapshot.pages[1]!.productIds!.push(b.document.id);
    expect(eligiblePdf(snapshot, [a, b]).snapshot.pages).toHaveLength(1);
  });
  it('excludes an inactive selected image even when the product has other active images', () => {
    const product = productFixture(),
      snapshot = documentFixture().snapshot;
    snapshot.pages = [productPage(product, snapshot, 'product')];
    product.document.media[0]!.isActive = false;
    expect(productEligible(product, 'en')).toBe(true);
    expect(eligiblePdf(snapshot, [product]).issues[0]?.reason).toMatch(/Selected image/);
  });
  it('rejects unowned local media but retains trusted template decorations', () => {
    const snapshot = documentFixture().snapshot;
    snapshot.pages[0]!.elements.push({
      id: 'image',
      type: 'image',
      label: 'Image',
      src: '/api/catalogue/media/' + 'f'.repeat(64),
      x: 1,
      y: 1,
      width: 10,
      height: 10,
    });
    expect(eligiblePdf(snapshot, []).snapshot.pages).toEqual([]);
    snapshot.pages[0]!.elements[2]!.src = '/templates/catalogue-original/assets/artwork.svg';
    expect(eligiblePdf(snapshot, []).snapshot.pages).toHaveLength(1);
  });
  it('shows valid legacy price bands but hides malformed confirmed prices', () => {
    const pricing = productFixture().document.pricing!;
    pricing.tiers[0]!.maximumQuantity = 1;
    expect(publicPricing(pricing)).toBe(true);
    pricing.tiers[0]!.maximumQuantity = 10;
    expect(publicPricing(pricing)).toBe(false);
    pricing.tiers[0]!.maximumQuantity = 9;
    pricing.currency = 'USD';
    expect(publicPricing(pricing)).toBe(false);
  });
  it('does not permit remote or traversing media URLs', () => {
    expect(isSafeDesignSource('https://example.com/a.png')).toBe(false);
    expect(isSafeDesignSource('/templates/catalogue-original/assets/../x.png')).toBe(false);
    expect(isSafeDesignSource('/templates/catalogue-original/assets/mark.svg')).toBe(true);
  });
  it('requires exact original products before importing the original design', () => {
    expect(() => originalCatalogue(documentFixture().snapshot, [])).toThrow(/requires products/);
  });
  it('binds every original price even when current product pricing is absent', () => {
    const products = [...new Set(originalEn.pages.flatMap((page) => page.productIds))].map((id) => {
      const product = productFixture(id);
      product.document.pricing = null;
      return product;
    });
    const result = originalCatalogue(documentFixture().snapshot, products).snapshot;
    const prices = result.pages.flatMap((page) => page.elements).filter((element) => element.role === 'price');
    expect(prices.length).toBeGreaterThan(0);
    expect(prices.every((element) => element.binding?.kind === 'product' && element.binding.field === 'price')).toBe(
      true,
    );
    expect(prices.every((element) => element.text === 'Price on request')).toBe(true);
    expect(result.pages.map((page) => [page.id, page.width, page.height])).toEqual(
      originalEn.pages.map((page) => [page.id, page.width, page.height]),
    );
  });
});
