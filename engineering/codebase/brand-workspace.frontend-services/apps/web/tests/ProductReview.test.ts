import { describe, expect, it } from 'vitest';
import { emptyCopy, type Product } from '@/domain/workspace/Commerce';
import { canConfirmProductPrices, hasFixedSomPricing, productBandPrice, productReviewIssueCount, unconfirmedProductLocales, updateProductBandPrice } from '@/domain/workspace/ProductReview';
function product(): Product { return { revision: 1, sourceRevision: null, document: { id: 'vest', garmentType: 'vest', variantCode: 'v1', status: 'active', sortOrder: 0, sizes: [], fabricCodes: [], decorationCodes: [], tags: [], locales: { uz: { ...emptyCopy(), name: 'Nimcha', status: 'approved' }, ru: emptyCopy(), en: { ...emptyCopy(), name: 'Vest', status: 'approved' } }, pricing: { currency: 'USD', status: 'confirmed', tiers: [{minimumQuantity:1, maximumQuantity:null, unitPrice:40}], wording:null }, media: [], issues: [], notes: null, stock: null, madeToOrder: false } }; }
describe('product review', () => {
 it('requires core and explicitly authored languages, without inventing twenty-two missing translation issues', () => {
  const value=product();value.translations={fr:{...emptyCopy(),name:'Gilet'}};
  expect(unconfirmedProductLocales(value)).toEqual(['ru','fr']);
  value.document.issues.push({id:'fabric',field:'fabric',status:'open',question:'Confirm silk composition'});
  expect(productReviewIssueCount(value)).toBe(3);
 });
 it('never relabels foreign amounts as UZS and replaces edited pricing with exactly two bands', () => {
  const value=product();expect(productBandPrice(value,1)).toBeNull();expect(hasFixedSomPricing(value)).toBe(false);
  updateProductBandPrice(value,1,500000);updateProductBandPrice(value,10,450000);
  expect(value.document.pricing).toMatchObject({currency:'UZS',status:'draft',tiers:[{minimumQuantity:1,maximumQuantity:9,unitPrice:500000},{minimumQuantity:10,maximumQuantity:null,unitPrice:450000}]});
  expect(hasFixedSomPricing(value)).toBe(true);
 });
 it('cannot confirm a partially entered price or a cleared amount', () => {
  const value=product();updateProductBandPrice(value,1,500000);expect(canConfirmProductPrices(value)).toBe(false);
  updateProductBandPrice(value,10,450000);expect(canConfirmProductPrices(value)).toBe(true);
  updateProductBandPrice(value,10,0);expect(canConfirmProductPrices(value)).toBe(false);expect(value.document.pricing?.status).toBe('draft');
 });
 it('preserves the other band and rejects invalid amounts', () => {
  const value=product();updateProductBandPrice(value,1,500000);updateProductBandPrice(value,10,450000);
  updateProductBandPrice(value,1,NaN);updateProductBandPrice(value,10,-1);
  expect(productBandPrice(value,1)).toBe(500000);expect(productBandPrice(value,10)).toBe(450000);
 });
});
