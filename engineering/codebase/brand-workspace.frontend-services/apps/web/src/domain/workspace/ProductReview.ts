import { catalogueLocales, productCopy, type CatalogueLocale, type Product } from './Commerce';

/** Core languages and explicitly authored translations are reviewed independently. */
export function unconfirmedProductLocales(product: Product): CatalogueLocale[] {
  return catalogueLocales.filter(language => ['uz', 'ru', 'en'].includes(language.code) || product.translations?.[language.code])
    .filter(language => productCopy(product, language.code).status !== 'approved' || !productCopy(product, language.code).name?.trim())
    .map(language => language.code);
}
export function productReviewIssueCount(product: Product): number {
  return unconfirmedProductLocales(product).length + product.document.issues.filter(issue => issue.status === 'open').length;
}
/** Existing non-UZS rates are never interpreted as som or automatically converted. */
export function productBandPrice(product: Product, quantity: 1 | 10): number | null {
  const pricing = product.document.pricing;
  if (!pricing || pricing.currency !== 'UZS') return null;
  return pricing.tiers.find(tier => tier.minimumQuantity <= quantity && (tier.maximumQuantity === null || tier.maximumQuantity >= quantity))?.unitPrice ?? null;
}
export function hasFixedSomPricing(product: Product): boolean {
  const pricing = product.document.pricing;
  return pricing?.currency === 'UZS' && pricing.tiers.length === 2 && pricing.tiers[0]?.minimumQuantity === 1 && pricing.tiers[0]?.maximumQuantity === 9 && pricing.tiers[1]?.minimumQuantity === 10 && pricing.tiers[1]?.maximumQuantity === null;
}
/** Blank or zero draft amounts remain incomplete and must never be approved as free products. */
export function canConfirmProductPrices(product: Product): boolean {
  return hasFixedSomPricing(product) && product.document.pricing!.tiers.every(tier => Number.isFinite(tier.unitPrice) && tier.unitPrice > 0);
}
export function updateProductBandPrice(product: Product, quantity: 1 | 10, amount: number): void {
  if (!Number.isFinite(amount) || amount < 0) return;
  const retail = quantity === 1 ? amount : productBandPrice(product, 1) ?? 0;
  const wholesale = quantity === 10 ? amount : productBandPrice(product, 10) ?? 0;
  product.document.pricing = { currency: 'UZS', status: 'draft', wording: product.document.pricing?.wording ?? null, tiers: [
    { minimumQuantity: 1, maximumQuantity: 9, unitPrice: retail },
    { minimumQuantity: 10, maximumQuantity: null, unitPrice: wholesale },
  ] };
}
