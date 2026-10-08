import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { enableAutoUnmount, mount } from '@vue/test-utils';
import CatalogueWebPreview from '../src/presentation/design/CatalogueWebPreview.vue';
import { useUiLocale } from '../src/application/shared/UseUiLocale';
import { catalogueSettings } from '../src/domain/design/CatalogueDesign';
import type { ProductDetail } from '../src/domain/catalogue/CatalogueModels';
import type { CatalogueWebPage, DesignSnapshot } from '../src/domain/design/EditorModels';
import { documentFixture, productFixture } from './NativeDesignFixture';

enableAutoUnmount(afterEach);
beforeEach(() => useUiLocale().setUiLocale('en'));

function snapshot(pages: CatalogueWebPage[]): DesignSnapshot {
  const value = documentFixture().snapshot;
  value.catalogue!.web.pages = pages;
  return value;
}

function page(productIds: string[], overrides: Partial<CatalogueWebPage> = {}): CatalogueWebPage {
  return { id: 'page-one', title: 'Summer selection', isActive: true, layout: 'grid', productIds, ...overrides };
}

function preview(products: ProductDetail[], pages: CatalogueWebPage[], editing = false, pageId?: string) {
  return mount(CatalogueWebPreview, {
    props: { products, snapshot: snapshot(pages), title: 'Summer catalogue', editing, pageId },
  });
}

describe('Catalogue web draft preview', () => {
  it('shows assigned unapproved translations in draft mode without changing default eligibility', () => {
    const product = productFixture();
    product.document.locales.en.status = 'draft';
    const pages = [page([product.document.id])];
    const draft = preview([product], pages, true);
    expect(draft.find('article h3').text()).toBe('Waistcoat');
    expect(draft.find('.needs-review').text()).toBe('Needs review');
    expect(draft.findAll('img')).toHaveLength(1);

    const publicPreview = preview([product], pages);
    expect(publicPreview.findAll('.web-page')).toHaveLength(0);
    expect(publicPreview.findAll('article')).toHaveLength(0);
    expect(publicPreview.text()).not.toContain('Needs review');
  });

  it.each([
    ['grid', 3],
    ['spread', 1],
    ['collage', 1],
  ] as const)('keeps an inactive empty %s page visible with %i deliberate product slots', (layout, count) => {
    const pages = [page([], { id: 'other', title: 'Other page' }), page([], { isActive: false, layout })];
    const draft = preview([], pages, true, 'page-one');
    expect(draft.findAll('.web-page')).toHaveLength(1);
    expect(draft.find('.web-page').classes()).toContain(layout);
    expect(draft.find('h2').text()).toBe('Summer selection');
    expect(draft.text()).toContain('Inactive page');
    expect(draft.findAll('.product-placeholder')).toHaveLength(count);
    expect(draft.find('.product-placeholder h3').text()).toBe('Product slot 1');
    expect(draft.text()).toContain('Choose products to fill this layout.');
    expect(draft.find('[aria-busy="true"]').exists()).toBe(false);
    expect(draft.find('.skeleton').exists()).toBe(false);
    expect(preview([], pages, false, 'page-one').find('.web-page').exists()).toBe(false);
  });

  it('preserves authored grid product order even when the source list has another order', () => {
    const first = productFixture('nimcha-first');
    const second = productFixture('nimcha-second');
    first.document.locales.en.name = 'First garment';
    second.document.locales.en.name = 'Second garment';
    const wrapper = preview([first, second], [page([second.document.id, first.document.id])], true);
    expect(wrapper.find('.web-page').classes()).toContain('grid');
    expect(wrapper.findAll('.products article').map((article) => article.find('h3').text())).toEqual([
      'Second garment',
      'First garment',
    ]);
    expect(wrapper.findAll('.needs-review')).toHaveLength(0);
  });

  it('keeps draft-only, archived, missing-image and open-issue products out of default output', () => {
    const approved = productFixture('approved');
    approved.document.locales.en.name = 'Approved garment';
    const draft = productFixture('draft');
    const archived = productFixture('archived');
    const missingImage = productFixture('missing-image');
    const openIssue = productFixture('open-issue');
    draft.document.status = 'draft';
    archived.document.status = 'archived';
    missingImage.document.media = [];
    openIssue.document.issues = [{ id: 'review', field: 'fabric', status: 'open', question: 'Confirm fabric' }];
    const products = [approved, draft, archived, missingImage, openIssue];
    const pages = [page(products.map((product) => product.document.id))];
    const defaultPreview = preview(products, pages);
    expect(defaultPreview.findAll('.products article').map((article) => article.find('h3').text())).toEqual([
      'Approved garment',
    ]);
    const draftPreview = preview(products, pages, true);
    expect(draftPreview.findAll('.products article')).toHaveLength(5);
    expect(draftPreview.findAll('.needs-review')).toHaveLength(4);
  });

  it('preserves active media group filtering and labels missing images without substituting inactive photographs', () => {
    const product = productFixture();
    product.document.media[0]!.group = 'product';
    product.document.media[0]!.isActive = false;
    product.document.locales.en.decoration = 'Hand embroidery';
    const value = snapshot([page([product.document.id])]);
    value.catalogueSettings = { ...catalogueSettings(value), mediaGroup: 'product' };
    const wrapper = mount(CatalogueWebPreview, {
      props: { products: [product], snapshot: value, title: 'Summer', editing: true },
    });
    expect(wrapper.findAll('img')).toHaveLength(0);
    expect(wrapper.find('.image-empty').text()).toBe('No active image');
    expect(wrapper.find('dl').text()).toContain('Hand embroidery');
  });

  it('localizes draft annotations using the interface language without translating authored content', () => {
    useUiLocale().setUiLocale('ru');
    const product = productFixture();
    product.document.locales.en.status = 'draft';
    const wrapper = preview([product], [page([product.document.id])], true);
    expect(wrapper.find('.needs-review').text()).toBe(useUiLocale().t('Needs review'));
    expect(wrapper.find('.needs-review').attributes('lang')).toBe('ru');
    expect(wrapper.find('h3').text()).toBe('Waistcoat');
    expect(wrapper.attributes('lang')).toBe('en');
  });
});
