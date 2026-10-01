import { cloneSnapshot, type DesignSnapshot } from './EditorModels';
import catalogueUz from './templates/catalogue-uz.json';
import catalogueRu from './templates/catalogue-ru.json';
import catalogueEn from './templates/catalogue-en.json';
import cardUz from './templates/card-uz.json';
import cardRu from './templates/card-ru.json';
import cardEn from './templates/card-en.json';
import products from './templates/products.json';
import assets from './templates/brand-assets.json';

export type TemplateLocale = DesignSnapshot['locale'];
export type CardVariant = 'labelled' | 'logo-only';
export const catalogueProducts = products;
export const brandAssets = assets;

/** Fresh copies preserve the imported v22 coordinates and independent edit history. */
export function createCatalogueTemplate(locale: TemplateLocale = 'uz'): DesignSnapshot {
  const seeds = { uz: catalogueUz, ru: catalogueRu, en: catalogueEn };
  return cloneSnapshot(seeds[locale] as DesignSnapshot);
}

export function createCardTemplate(variant: CardVariant = 'labelled', locale: TemplateLocale = 'uz'): DesignSnapshot {
  const seeds = { uz: cardUz, ru: cardRu, en: cardEn };
  const snapshot = cloneSnapshot(seeds[locale] as DesignSnapshot);
  if (variant === 'logo-only') {
    const descriptor = snapshot.pages[0]?.elements.find((element) => element.id === 'front-descriptor');
    if (descriptor) descriptor.opacity = 0;
  }
  return snapshot;
}
