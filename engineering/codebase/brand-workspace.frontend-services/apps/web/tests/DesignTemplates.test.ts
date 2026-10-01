import { describe, expect, it } from 'vitest';
import { readFileSync, existsSync } from 'node:fs';
import { resolve } from 'node:path';
import { validateSnapshot } from '@/domain/workspace/EditorModels';
import { createCatalogueTemplate, createCardTemplate } from '@/domain/workspace/Templates';
import { moveCatalogueProduct, renumberCataloguePages, setCataloguePrice } from '@/domain/workspace/CatalogueLayout';

describe('imported Ocharo templates', () => {
  it.each(['uz', 'ru', 'en'] as const)('preserves v22 pages, products, price tiers and source assets in %s', (locale) => {
    const template = validateSnapshot(createCatalogueTemplate(locale));
    expect(template.pages).toHaveLength(16);
    const products = template.pages.flatMap((page) => page.productIds ?? []);
    expect(products).toHaveLength(29);
    expect(new Set(products).size).toBe(29);
    expect(products[0]).toBe('abaya-v01');
    expect(products[28]).toBe('nimcha-v16');
    const prices = template.pages.flatMap((page) => page.elements.filter((element) => element.role === 'price'));
    expect(prices).toHaveLength(58);
    expect(prices.filter((element) => element.tier === 'single')).toHaveLength(29);
    expect(prices.filter((element) => element.tier === 'bulk')).toHaveLength(29);
    expect(prices.find((element) => element.productId === 'abaya-v01' && element.tier === 'single')?.text).toBe('150 000');
    expect(prices.find((element) => element.productId === 'abaya-v01' && element.tier === 'bulk')?.text).toBe('100 000');
    expect(new TextEncoder().encode(JSON.stringify(template)).byteLength).toBeLessThan(1_048_576);
    for (const element of template.pages.flatMap((page) => page.elements)) {
      if (element.src) expect(existsSync(resolve('public', element.src.slice(1)))).toBe(true);
    }
  });

  it('moves whole garment groups without changing page decoration or source prices', () => {
    const source = createCatalogueTemplate('en');
    const page = source.pages[2]!;
    const original = page.elements.filter((element) => element.groupId === 'baska-v01');
    const firstPhoto = original.find((element) => element.role === 'photo')!;
    const targetPhoto = page.elements.find((element) => element.groupId === 'baska-v02' && element.role === 'photo')!;
    const moved = moveCatalogueProduct(source, page.id, 'baska-v01', 1).pages[2]!;
    expect(moved.productIds?.slice(0, 2)).toEqual(['baska-v02', 'baska-v01']);
    for (const element of original) {
      const updated = moved.elements.find((item) => item.id === element.id)!;
      expect(updated.x).toBeCloseTo(element.x + targetPhoto.x - firstPhoto.x);
      expect(updated.y).toBeCloseTo(element.y + targetPhoto.y - firstPhoto.y);
      expect(updated.text).toBe(element.text);
    }
    expect(moved.elements.filter((element) => !element.groupId)).toEqual(page.elements.filter((element) => !element.groupId));
    expect(page.productIds?.[0]).toBe('baska-v01');
  });

  it('renumbers explicit folios after a page move without changing model identifiers', () => {
    const source = createCatalogueTemplate('en');
    [source.pages[1], source.pages[2]] = [source.pages[2]!, source.pages[1]!];
    const next = renumberCataloguePages(source);
    expect(next.pages[1]?.elements.find((element) => element.role === 'page-number')?.text).toBe('02');
    expect(next.pages[1]?.elements.find((element) => element.productId === 'baska-v03' && element.text === '03')).toBeDefined();
    expect(next.pages[0]?.elements.some((element) => element.role === 'page-number')).toBe(false);
    expect(source.pages[1]?.elements.find((element) => element.role === 'page-number')?.text).toBe('03');
  });

  it('allows a longer price while respecting the next price column', () => {
    const source = createCatalogueTemplate('en'), page = source.pages[7]!;
    const price = page.elements.find((element) => element.productId === 'nimcha-v05' && element.tier === 'single')!;
    const bulk = page.elements.find((element) => element.productId === 'nimcha-v05' && element.tier === 'bulk')!;
    const next = setCataloguePrice(source, page.id, price.id, 1250000);
    const updated = next.pages[7]!.elements.find((element) => element.id === price.id)!;
    expect(updated.text).toBe('1 250 000');
    expect(updated.width).toBeGreaterThan(price.width);
    expect(updated.x + updated.width).toBeLessThan(bulk.x);
    expect(price.text).toBe('130 000');
  });

  it.each(['uz', 'ru', 'en'] as const)('retains editable card faces and the optional descriptor in %s', (locale) => {
    const template = validateSnapshot(createCardTemplate('labelled', locale));
    expect(template.pages.map((page) => [page.width, page.height])).toEqual([[85, 55], [85, 55]]);
    expect(template.pages[1]?.elements.find((element) => element.id === 'contact-2')?.text).toBe('+998 97 733 25 15');
    const logoOnly = validateSnapshot(createCardTemplate('logo-only', locale));
    expect(logoOnly.pages[0]?.elements.find((element) => element.id === 'front-descriptor')?.opacity).toBe(0);
    expect(template.pages[0]?.elements.find((element) => element.id === 'front-descriptor')?.opacity).toBeUndefined();
  });

  it('records reproducible asset hashes instead of embedding media in snapshots', () => {
    const manifest = JSON.parse(readFileSync(resolve('public/templates/ocharo/manifest.json'), 'utf8')) as { assets: Array<{ url: string; derivativeSha256: string }> };
    expect(manifest.assets.length).toBeGreaterThan(50);
    expect(manifest.assets.every((asset) => /^[a-f\d]{64}$/.test(asset.derivativeSha256))).toBe(true);
  });
});
