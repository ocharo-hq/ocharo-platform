import { describe, expect, it } from 'vitest';
import { brandContactLines, type BrandKit } from '../src/domain/catalogue/BrandKit';

const kit: BrandKit = {
  name: 'Ocharo',
  release: 3,
  descriptors: { uz: null, ru: null, en: null },
  links: [
    { key: 'instagram', label: 'Instagram', handle: 'ocharostile', url: 'https://www.instagram.com/ocharostile/' },
    { key: 'telegram', label: 'Telegram', handle: 'Ocharo_Style', url: 'https://t.me/Ocharo_Style' },
  ],
  logos: {},
};

describe('brandContactLines', () => {
  it('prints brand links and keeps only extra lines that do not repeat a handle', () => {
    expect(brandContactLines(kit, ['Instagram: ocharostile', 'telegram: ocharo_style', 'Print-only line'])).toEqual([
      'Instagram: ocharostile',
      'Telegram: Ocharo_Style',
      'Print-only line',
    ]);
  });

  it('falls back to catalogue lines when the Brand service is offline', () => {
    expect(brandContactLines(null, ['Instagram: ocharostile'])).toEqual(['Instagram: ocharostile']);
  });
});
