import { describe, expect, it } from 'vitest';
import { validateSnapshot } from '@/domain/workspace/EditorModels';
import { createCatalogueTemplate, createCardTemplate } from '@/domain/workspace/Templates';

describe('portable design templates', () => {
  it.each(['uz', 'ru', 'en'] as const)('validates the actual %s catalogue and card payloads', (locale) => {
    const catalogue = validateSnapshot(createCatalogueTemplate(locale));
    expect(catalogue.pages).toHaveLength(16);
    expect(catalogue.locale).toBe(locale);
    expect(new TextEncoder().encode(JSON.stringify(catalogue)).length).toBeLessThan(1_048_576);
    const card = validateSnapshot(createCardTemplate('labelled', locale));
    expect(card.pages).toHaveLength(2);
    expect(card.pages.every((p) => p.width === 85 && p.height === 55)).toBe(true);
  });
  it('retains edited prices, crop geometry and order across a JSON round trip', () => {
    const original = createCatalogueTemplate('uz');
    const price = original.pages.flatMap((p) => p.elements).find((e) => e.role === 'price')!;
    expect(price).toBeDefined(); price.text = '275 000';
    const photo = original.pages.flatMap((p) => p.elements).find((e) => e.type === 'image')!;
    photo.cropX = 30; photo.zoom = 1.4; photo.width = 73;
    const first = original.pages.shift()!; original.pages.splice(3, 0, first);
    const restored = validateSnapshot(JSON.parse(JSON.stringify(original)));
    expect(restored).toEqual(original);
    expect(createCatalogueTemplate('uz').pages[0]?.id).toBe(first.id);
  });
  it('rejects scripts, network images, path traversal, duplicate IDs and invalid geometry', () => {
    for (const src of ['javascript:alert(1)', 'https://tracker.test/image.png', '/templates/../../private/image.png']) {
      const data = createCardTemplate(); data.pages[0]!.elements[0]!.src = src;
      expect(() => validateSnapshot(data)).toThrow();
    }
    const duplicate = createCardTemplate(); duplicate.pages.push(duplicate.pages[0]!);
    expect(() => validateSnapshot(duplicate)).toThrow();
    const geometry = createCardTemplate(); geometry.pages[0]!.elements[0]!.width = Infinity;
    expect(() => validateSnapshot(geometry)).toThrow();
  });
});
