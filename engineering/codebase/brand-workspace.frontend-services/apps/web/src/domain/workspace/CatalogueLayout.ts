import { cloneSnapshot, type DesignSnapshot } from './EditorModels';

/** Updates existing folios after page moves without adding numbers to photographic spreads. */
export function renumberCataloguePages(snapshot: DesignSnapshot): DesignSnapshot {
  const next = cloneSnapshot(snapshot);
  if (next.kind !== 'catalogue') return next;
  next.pages.forEach((page, index) => {
    for (const element of page.elements) if (element.role === 'page-number') element.text = String(index + 1).padStart(2, '0');
  });
  return next;
}

/** Keeps edited prices inside their original quantity column, including longer amounts. */
export function setCataloguePrice(snapshot: DesignSnapshot, pageId: string, elementId: string, amount: number): DesignSnapshot {
  const next = cloneSnapshot(snapshot), page = next.pages.find((item) => item.id === pageId);
  const element = page?.elements.find((item) => item.id === elementId && item.role === 'price');
  if (!page || !element || !Number.isFinite(amount) || amount < 0 || amount > 999999999) return next;
  if (element.binding?.kind === 'product') throw new Error('Edit canonical prices in Products, then refresh the catalogue.');
  element.text = new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 }).format(amount).replaceAll(',', ' ');
  if (page.catalogueSource) return next;
  const photo = page.elements.find((item) => item.productId === element.productId && item.role === 'photo');
  const otherPrice = page.elements.find((item) => item.productId === element.productId && item.role === 'price' && item.x > element.x);
  const right = otherPrice ? otherPrice.x - 2 : photo ? photo.x + photo.width - 2 : element.x + element.width;
  element.width = Math.max(element.width, right - element.x);
  return next;
}

/** Move a garment and its prices/specifications together between existing page slots. */
export function moveCatalogueProduct(snapshot: DesignSnapshot, pageId: string, productId: string, direction: -1 | 1): DesignSnapshot {
  const next = cloneSnapshot(snapshot), page = next.pages.find((item) => item.id === pageId);
  if (!page?.productIds) return next;
  const index = page.productIds.indexOf(productId), target = index + direction;
  if (index < 0 || target < 0 || target >= page.productIds.length) return next;
  const otherId = page.productIds[target]!;
  const first = page.elements.find((element) => element.productId === productId && element.role === 'photo');
  const second = page.elements.find((element) => element.productId === otherId && element.role === 'photo');
  if (!first || !second) return next;
  const dx = second.x - first.x, dy = second.y - first.y;
  page.elements = page.elements.map((element) => element.groupId === productId ? { ...element, x: element.x + dx, y: element.y + dy }
    : element.groupId === otherId ? { ...element, x: element.x - dx, y: element.y - dy } : element);
  [page.productIds[index], page.productIds[target]] = [otherId, productId];
  return next;
}
