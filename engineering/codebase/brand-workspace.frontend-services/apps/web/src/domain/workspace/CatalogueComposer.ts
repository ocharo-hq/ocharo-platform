import type { CommerceDraft, Locale, Product } from './Commerce';
import { cloneSnapshot, validateSnapshot, type DesignElement, type DesignPage, type DesignSnapshot } from './EditorModels';
import { renumberCataloguePages } from './CatalogueLayout';

export type CataloguePageLayout = 'product' | 'continuation' | 'collage';
export interface CatalogueSelection { productId: string; imageHashes: string[] }
export const catalogueLayouts = [
  { id: 'product', title: 'Product page', description: 'Hero image, product story, sizes and price tiers.' },
  { id: 'continuation', title: 'Product continuation', description: 'Two detail images with fabric and garment notes.' },
  { id: 'collage', title: 'Photo collage', description: 'Up to six chosen photographs in a balanced grid.' },
] as const;
const uid = () => crypto.randomUUID();
function label(text: string, x: number, y: number, width: number, height: number, size = 4, role: DesignElement['role'] = 'caption'): DesignElement {
  return { id: uid(), type: 'text', label: role === 'title' ? 'Product title' : text.replace(/[\u0000-\u001f\u007f]/g, ' ').slice(0, 100) || 'Text', text, x, y, width, height,
    fontFamily: role === 'title' ? 'serif' : 'sans', fontSize: size, lineHeight: 1.3, color: '#282820', role };
}
export function productName(product: Product, locale: Locale): string {
  return product.document.locales[locale]?.name || product.document.id;
}
export function createCatalogueCover(locale: Locale, name = 'Ocharo'): DesignSnapshot {
  return { schemaVersion: 1, kind: 'catalogue', templateId: 'ocharo-composer-v1', locale, pages: [{
    id: uid(), title: 'Cover', width: 210, height: 297, background: '#f7f3e9', elements: [
      label(name, 18, 28, 174, 35, 16, 'title'), label('COLLECTION', 18, 78, 174, 16, 5),
      label('A collection, thoughtfully assembled.', 18, 239, 174, 20, 6),
    ],
  }] };
}
/** Copies a canonical revision into an editable private edition; it never changes canonical prices. */
export function composeCataloguePages(organizationId: string, draft: CommerceDraft, selections: CatalogueSelection[],
  layout: CataloguePageLayout, locale: Locale): DesignPage[] {
  if (!selections.length) throw new Error('Choose at least one product.');
  if (selections.length > 99) throw new Error('Choose at most 99 products per edition.');
  return selections.flatMap(selection => {
    const product = draft.snapshot.products.find(item => item.document.id === selection.productId);
    if (!product || product.document.status === 'archived') throw new Error('A selected product is no longer available.');
    const document = product.document, copy = document.locales[locale];
    const images = selection.imageHashes.map(hash => document.media.find(image => image.sha256 === hash));
    const capacity = layout === 'collage' ? 6 : 2;
    if (!images.length || images.some(image => !image) || new Set(selection.imageHashes).size !== images.length || images.length > capacity)
      throw new Error(`Choose one to ${capacity} available images for each product.`);
    const name = productName(product, locale);
    const page: DesignPage = { id: uid(), title: `${name.slice(0, 115)} · ${layout}`, width: 210, height: 297,
      background: '#fbf9f3', productIds: [document.id], catalogueSource: {
        brandId: draft.brandId, revision: draft.revision, productRevision: product.revision,
        productId: document.id, layout, currency: document.pricing?.currency ?? null, imageHashes: [...selection.imageHashes],
      }, elements: [label(draft.snapshot.identity.name, 15, 10, 165, 8, 3), label(name, 15, 24, 180, 24, 8, 'title')] };
    const frame = (index: number, x: number, y: number, width: number, height: number) => {
      const image = images[index]!;
      page.elements.push({id: uid(), type: 'image', label: image.alt || image.fileName, role: 'photo',
        src: `/api/organizations/${organizationId}/brands/${draft.brandId}/commerce/media/${image.sha256}`,
        x, y, width, height, fit: 'cover', cropX: 50, cropY: 50, zoom: 1, productId: document.id, groupId: document.id});
    };
    if (layout === 'product') {
      frame(0, 15, 53, 110, 173);
      if (images.length > 1) frame(1, 132, 53, 63, 85);
      const start = images.length > 1 ? 147 : 56;
      page.elements.push(label([copy?.fabric, copy?.decoration, document.sizes.length ? `Sizes: ${document.sizes.join(', ')}` : null].filter(Boolean).join('\n\n'), 132, start, 63, 80, 3.3));
      page.elements.push(label(copy?.description || '', 15, 235, 180, 25, 3.6));
    } else if (layout === 'continuation') {
      frame(0, 15, 53, images.length > 1 ? 87 : 180, 164);
      if (images.length > 1) frame(1, 108, 53, 87, 164);
      page.elements.push(label([copy?.fabric, copy?.decoration, copy?.description].filter(Boolean).join('\n'), 15, 226, 180, 44, 4));
    } else {
      const columns = images.length === 1 ? 1 : 2, rows = Math.ceil(images.length / columns);
      const width = (180 - (columns - 1) * 6) / columns, height = (214 - (rows - 1) * 6) / rows;
      images.forEach((_, index) => frame(index, 15 + (index % columns) * (width + 6), 53 + Math.floor(index / columns) * (height + 6), width, height));
    }
    const pricingPages: DesignPage[] = [];
    if (layout === 'product' && document.pricing) {
      const tiers = document.pricing.tiers.slice(0, 4);
      tiers.forEach((tier, index) => {
        const x = 15 + index * (180 / tiers.length), width = 174 / tiers.length;
        const quantity = `${tier.minimumQuantity}${tier.maximumQuantity === null ? '+' : `–${tier.maximumQuantity}`} pieces`;
        page.elements.push(label(`${quantity} · ${document.pricing!.currency}`, x, 267, width, 7, 2.7));
        page.elements.push({...label(String(tier.unitPrice), x, 276, width, 10, 4.5, 'price'), priceLabel: quantity, productId: document.id, groupId: document.id});
      });
      const additional = document.pricing.tiers.slice(4);
      for (let start = 0; start < additional.length; start += 12) {
        const pricingPage: DesignPage = { ...page, id: uid(), title: `${name.slice(0, 110)} · prices ${start / 12 + 1}`,
          elements: [label(draft.snapshot.identity.name, 15, 10, 165, 8, 3), label(`${name} · prices`, 15, 24, 180, 24, 8, 'title')] };
        additional.slice(start, start + 12).forEach((tier, index) => {
          const quantity = `${tier.minimumQuantity}${tier.maximumQuantity === null ? '+' : `–${tier.maximumQuantity}`} pieces`;
          pricingPage.elements.push(label(`${quantity} · ${document.pricing!.currency}`, 15, 58 + index * 17, 100, 12, 4));
          pricingPage.elements.push({ ...label(String(tier.unitPrice), 122, 58 + index * 17, 73, 12, 5, 'price'), priceLabel: quantity, productId: document.id });
        });
        pricingPage.elements.push(label('01', 187, 287, 10, 6, 2.7, 'page-number'));
        pricingPages.push(pricingPage);
      }
    }
    page.elements.push(label('01', 187, 287, 10, 6, 2.7, 'page-number'));
    page.elements = page.elements.map(element => ({...element, productId: document.id, groupId: document.id}));
    return [page, ...pricingPages];
  });
}
export function appendCataloguePages(snapshot: DesignSnapshot, pages: DesignPage[], afterId?: string): DesignSnapshot {
  if (snapshot.kind !== 'catalogue') throw new Error('Open the catalogue editor.');
  if (snapshot.pages.length + pages.length > 100) throw new Error('A catalogue supports up to 100 pages.');
  const next = cloneSnapshot(snapshot), index = afterId ? next.pages.findIndex(page => page.id === afterId) + 1 : next.pages.length;
  next.pages.splice(index < 1 ? next.pages.length : index, 0, ...pages);
  return validateSnapshot(renumberCataloguePages(next));
}
export function removeCataloguePage(snapshot: DesignSnapshot, id: string): DesignSnapshot {
  if (snapshot.pages.length < 2) throw new Error('Keep at least one page in the catalogue.');
  const next = cloneSnapshot(snapshot); next.pages = next.pages.filter(page => page.id !== id);
  return validateSnapshot(renumberCataloguePages(next));
}
