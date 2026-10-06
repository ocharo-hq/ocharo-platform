/** @internal Human names for imported product-review paths; persisted field identifiers stay intact. */
const reviewFieldLabels: Record<string, string> = {
  general: 'General',
  'assets.product_reference_images': 'Reference images',
  'assets.cutout_images': 'Cutout images',
  'assets.model_images': 'Model images',
  'assets.source_images': 'Product photos',
  pricing: 'Price',
  sizes: 'Sizes',
};

/** Resolves a review field's human label without rewriting its source identity. */
export function productReviewFieldLabel(field: string): string {
  return reviewFieldLabels[field] ?? field;
}
