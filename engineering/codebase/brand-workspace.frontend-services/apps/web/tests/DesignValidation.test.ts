import { describe, expect, it } from 'vitest';
import { validateSnapshot } from '@/domain/workspace/EditorModels';
import { createCatalogueTemplate, createCardTemplate } from '@/domain/workspace/Templates';

describe('untrusted design metadata', () => {
  it.each([null, {}, 'garment', [123], [''], ['a', 'a'], Array.from({ length: 301 }, (_, index) => `garment-${index}`)])(
    'rejects product lists that cannot safely drive catalogue tools: %j', (productIds) => {
      const input = createCatalogueTemplate();
      Object.assign(input.pages[2]!, { productIds });
      expect(() => validateSnapshot(input)).toThrow('Invalid page');
    },
  );

  it.each([
    ['productId', 123], ['productId', ''], ['groupId', {}], ['groupId', 'x'.repeat(257)],
    ['locked', 'false'], ['tier', 'retail'], ['role', 'script'], ['label', ['photo']],
    ['color', ['#ffffff']], ['background', ['#ffffff']], ['borderColor', ['#ffffff']],
  ])('rejects invalid element %s metadata', (key, value) => {
    const input = createCardTemplate();
    Object.assign(input.pages[0]!.elements[0]!, { [key as string]: value });
    expect(() => validateSnapshot(input)).toThrow('Invalid element');
  });

  it('bounds identity and display strings before they reach selectors or labels', () => {
    for (const value of ['', ' ', 'x'.repeat(257), 'line\nbreak']) {
      const input = createCardTemplate();
      input.templateId = value;
      expect(() => validateSnapshot(input)).toThrow('Unsupported design');
      input.templateId = 'card-v1'; input.pages[0]!.id = value;
      expect(() => validateSnapshot(input)).toThrow('Invalid page');
    }
    const input = createCardTemplate();
    input.pages[0]!.title = 'x'.repeat(161);
    expect(() => validateSnapshot(input)).toThrow('Invalid page');
  });

  it('preserves supported metadata and independent returned snapshots', () => {
    const input = createCatalogueTemplate();
    const element = input.pages[0]!.elements[0]!;
    Object.assign(element, { locked: false, productId: 'custom-garment', groupId: 'custom-group', tier: 'single', role: 'page-number' });
    const checked = validateSnapshot(input);
    expect(checked).toEqual(input);
    checked.pages[0]!.elements[0]!.label = 'Edited folio';
    expect(element.label).not.toBe('Edited folio');
  });
});
