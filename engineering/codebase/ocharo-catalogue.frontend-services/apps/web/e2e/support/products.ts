import type { APIRequestContext } from '@playwright/test';
import type { ProductDetail } from '../../src/domain/catalogue/CatalogueModels';
import { seedProduct, type ImageSeed, type LocaleSeed } from './catalogueApi';
import type { Rgb } from './images';

/** Four images, one per catalogue image group, so group and randomisation choices are observable. */
export function groupedImages(base: Rgb): ImageSeed[] {
  const shade = (factor: number): Rgb => [
    Math.min(255, Math.round(base[0] * factor)),
    Math.min(255, Math.round(base[1] * factor)),
    Math.min(255, Math.round(base[2] * factor)),
  ];
  return [
    { color: base, role: 'model', group: 'model' },
    { color: shade(0.8), role: 'cutout', group: 'product' },
    { color: shade(0.65), role: 'detail', group: 'detail' },
    { color: shade(1.2), role: 'fabric', group: 'fabric' },
  ];
}

export interface Garment {
  key: string;
  name: string;
  fabric: string;
  description: string;
  color: Rgb;
  prices: [number, number];
}
/** Synthetic garments; names and prices are invented test content, not brand records. */
export const garments: Garment[] = [
  {
    key: 'a',
    name: 'Linen Vest',
    fabric: 'Washed linen',
    description: 'Boxy vest with hand-finished seams.',
    color: [196, 150, 128],
    prices: [420_000, 380_000],
  },
  {
    key: 'b',
    name: 'Pleated Peplum',
    fabric: 'Silk crepe',
    description: 'Soft pleats that open at the waist.',
    color: [128, 152, 120],
    prices: [510_000, 470_000],
  },
  {
    key: 'c',
    name: 'Lace Corset',
    fabric: 'Cotton lace',
    description: 'Structured corset with a lace overlay.',
    color: [120, 136, 176],
    prices: [300_000, 260_000],
  },
];

/** A seeded garment's English name; the test's token keeps it unique in a store shared by parallel tests. */
export const garmentName = (index: number, token: string) => `${garments[index]!.name} ${token}`;

/** Seeds the three synthetic garments with English copy and returns them keyed by `a`, `b` and `c`. */
export async function seedGarments(
  request: APIRequestContext,
  token: string,
  locales: (garment: Garment, name: string) => Record<string, LocaleSeed> = (garment, name) => ({
    en: { name, fabric: garment.fabric, description: garment.description },
  }),
): Promise<Record<string, ProductDetail>> {
  const result: Record<string, ProductDetail> = {};
  for (const [index, garment] of garments.entries())
    result[garment.key] = await seedProduct(request, {
      variant: `${token}${garment.key}`,
      garment: garment.key === 'a' ? 'nimcha' : garment.key === 'b' ? 'baska' : 'korset',
      locales: locales(garment, garmentName(index, token)),
      images: groupedImages(garment.color),
      prices: garment.prices,
    });
  return result;
}

/** The media hash of a product's image in one group. */
export function groupImage(product: ProductDetail, group: string): string {
  const image = product.document.media.find((item) => item.group === group);
  if (!image) throw new Error(`${product.document.id} has no ${group} image.`);
  return image.sha256;
}
