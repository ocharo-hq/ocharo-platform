/** @internal The standard apparel order when the catalogue has no size preset. */
const apparelSizes = ['XXS', 'XS', 'S', 'M', 'L', 'XL', 'XXL', '2XL', 'XXXL', '3XL', '4XL', '5XL'];

/** Orders sizes by the catalogue preset, then standard apparel and natural numeric order. */
export function orderProductSizes(values: readonly string[], preset: readonly string[] = []): string[] {
  const ordered = [...new Set([...preset, ...apparelSizes])];
  const rank = (value: string) => {
    const index = ordered.findIndex((size) => size.toUpperCase() === value.toUpperCase());
    return index < 0 ? ordered.length : index;
  };
  return [...new Set(values)].sort(
    (left, right) =>
      rank(left) - rank(right) || left.localeCompare(right, 'en', { numeric: true, sensitivity: 'base' }),
  );
}
