import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import type { Component } from 'vue';
import type { Window as HappyDomWindow } from 'happy-dom';
import { createMemoryHistory, createRouter } from 'vue-router';
import { ApiFailureFactory } from '@wow-two-beta/ui-vue/foundation/http';
import { ResultExtensions } from '@wow-two-beta/ui-vue/foundation/results';
import { catalogueApi } from '../src/integration/catalogue';
import { useUiLocale } from '../src/application/shared/UseUiLocale';
import { confirmation, resolveConfirmation } from '../src/application/common/UseFeedback';
import ProductsPage from '../src/presentation/catalogue/ProductsPage.vue';
import ProductEditorPage from '../src/presentation/catalogue/ProductEditorPage.vue';
import ContentSkeleton from '../src/presentation/common/ContentSkeleton.vue';
import { readFileSync } from 'node:fs';
const resultStyles = readFileSync('src/bootstrap/index.css', 'utf8');
import CollectionsPage from '../src/presentation/catalogue/CollectionsPage.vue';
import StockPage from '../src/presentation/catalogue/StockPage.vue';
import CopyPage from '../src/presentation/catalogue/CopyPage.vue';
import PricingPage from '../src/presentation/catalogue/PricingPage.vue';
import type { ProductCopyRow, ProductSummary, StockRow } from '../src/domain/catalogue/CatalogueModels';

const wrappers: VueWrapper[] = [];
const stock: StockRow = {
  id: 'nimcha-v01',
  garmentType: 'nimcha',
  status: 'active',
  revision: 2,
  nameUz: 'Nimcha',
  previewUrl: null,
  sizes: ['S', 'M'],
  stock: { S: 4, M: 6 },
  madeToOrder: false,
  availability: 'in-stock',
};
const product: ProductSummary = {
  id: stock.id,
  garmentType: 'nimcha',
  variantCode: 'v01',
  status: 'active',
  sortOrder: 0,
  nameUz: 'Nimcha',
  nameRu: null,
  nameEn: 'Waistcoat',
  retailPrice: 100000,
  wholesalePrice: 80000,
  pricingStatus: 'confirmed',
  localesApproved: 3,
  openIssues: 0,
  mediaCount: 1,
  primaryImageUrl: null,
  primaryPreviewUrl: '/image.png',
  tags: [],
  revision: 2,
  updatedAt: '2026-01-01T00:00:00Z',
  availability: 'in-stock',
  stockTotal: 10,
};
const copy: ProductCopyRow = {
  id: stock.id,
  garmentType: 'nimcha',
  status: 'active',
  revision: 2,
  previewUrl: null,
  locales: {
    uz: { name: 'Nimcha', fabric: null, decoration: null, description: null, status: 'approved' },
    ru: { name: 'Жилет', fabric: null, decoration: null, description: null, status: 'approved' },
    en: { name: 'Waistcoat', fabric: null, decoration: null, description: null, status: 'approved' },
  },
};
const failure = () => ResultExtensions.fail(ApiFailureFactory.create('http', new Response(null, { status: 500 })));
const SelectStub = {
  props: ['modelValue', 'options', 'label'],
  template:
    '<select :aria-label="label"><option v-for="option in options" :key="option.value" :value="option.value">{{option.label}}</option></select>',
};
async function open(component: Component, path = '/') {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', component },
      { path: '/products/:id', component: component === ProductEditorPage ? component : { template: '<div />' } },
    ],
  });
  await router.push(path);
  await router.isReady();
  const wrapper = mount(component, {
    global: {
      plugins: [router],
      stubs: {
        CatalogueSelect: SelectStub,
        Modal: { props: ['open'], template: '<div><slot /></div>' },
        ModalContent: { template: '<div><slot /></div>' },
      },
    },
  });
  wrappers.push(wrapper);
  await flushPromises();
  return wrapper;
}
beforeEach(() => {
  vi.restoreAllMocks();
  useUiLocale().setUiLocale('en');
  vi.spyOn(catalogueApi, 'listProducts').mockResolvedValue(ResultExtensions.ok([]));
  vi.spyOn(catalogueApi, 'listCollections').mockResolvedValue(ResultExtensions.ok([]));
  vi.spyOn(catalogueApi, 'stockRows').mockResolvedValue(ResultExtensions.ok([]));
  vi.spyOn(catalogueApi, 'copyRows').mockResolvedValue(ResultExtensions.ok([]));
});
afterEach(() => {
  resolveConfirmation(false);
  wrappers.splice(0).forEach((wrapper) => wrapper.unmount());
  vi.useRealTimers();
  vi.restoreAllMocks();
});
const screens = [
  {
    name: 'Products',
    component: ProductsPage,
    method: 'listProducts' as const,
    empty: 'No products found. Clear filters or import products in Settings.',
  },
  { name: 'Collections', component: CollectionsPage, method: 'listCollections' as const, empty: 'No collections yet.' },
  { name: 'Stock', component: StockPage, method: 'stockRows' as const, empty: 'No products found.' },
  { name: 'Copy', component: CopyPage, method: 'copyRows' as const, empty: 'No products found.' },
  { name: 'Prices', component: PricingPage, method: 'listProducts' as const, empty: 'No products found.' },
];
const editableScreens = [
  {
    name: 'Stock',
    component: StockPage,
    selector: 'input[type="number"]',
    draft: '8',
    configure(pending: Promise<ReturnType<typeof failure>>) {
      return vi
        .mocked(catalogueApi.stockRows)
        .mockResolvedValueOnce(ResultExtensions.ok([structuredClone(stock)]))
        .mockReturnValue(pending);
    },
  },
  {
    name: 'Copy',
    component: CopyPage,
    selector: 'input[aria-label="nimcha-v01 Uzbek"]',
    draft: 'Local draft',
    configure(pending: Promise<ReturnType<typeof failure>>) {
      return vi
        .mocked(catalogueApi.copyRows)
        .mockResolvedValueOnce(ResultExtensions.ok([structuredClone(copy)]))
        .mockReturnValue(pending);
    },
  },
];
describe('Catalogue first-load editor geometry', () => {
  it.each([
    {
      variant: 'designer-web' as const,
      grid: '.catalogue-skeleton-designer-web-grid',
      columns: '300px minmax(0, 1fr)',
      regions: 2,
    },
    {
      variant: 'designer-pdf' as const,
      grid: '.catalogue-skeleton-designer-pdf-grid',
      columns: '230px minmax(0, 1fr) 250px',
      regions: 3,
    },
    {
      variant: 'product-form' as const,
      grid: '.catalogue-skeleton-form-grid',
      columns: 'minmax(0, 1.35fr) minmax(340px, 1fr)',
      regions: 2,
    },
  ])('$variant reserves its real editor regions instead of gallery cards', ({ variant, grid, columns, regions }) => {
    const previousWidth = window.innerWidth;
    const viewport = (window as unknown as HappyDomWindow).happyDOM;
    viewport.setViewport({ width: 1440 });
    const style = document.createElement('style');
    // Happy DOM cannot parse Tailwind's source directives; the actual query-region CSS is standard CSS.
    style.textContent = resultStyles.slice(resultStyles.indexOf('/* Query surfaces'));
    document.head.append(style);
    const wrapper = mount(ContentSkeleton, { props: { variant, label: 'Loading editor' }, attachTo: document.body });
    wrappers.push(wrapper);
    try {
      const layout = wrapper.find(grid);
      expect(layout.element.children).toHaveLength(regions);
      expect(getComputedStyle(layout.element).gridTemplateColumns).toBe(columns);
      expect(wrapper.find('.catalogue-skeleton-card').exists()).toBe(false);
      expect(wrapper.findAll('[role="status"]')).toHaveLength(1);
      expect(wrapper.find('input, button').exists()).toBe(false);
      if (variant === 'designer-pdf') {
        expect(wrapper.find('.catalogue-skeleton-paper').exists()).toBe(true);
        expect(getComputedStyle(wrapper.find('.catalogue-skeleton-paper').element).width).toBe('720px');
      }
    } finally {
      style.remove();
      viewport.setViewport({ width: previousWidth });
    }
  });
});
describe('Catalogue result surfaces', () => {
  it.each(screens)(
    '$name renders successful empty copy plainly and retains toolbar Refresh',
    async ({ component, empty }) => {
      const wrapper = await open(component);
      const state = wrapper.find('.catalogue-empty-state');
      expect(state.text()).toBe(empty);
      expect(state.classes()).not.toContain('panel');
      expect(state.element.closest('.panel')).toBeNull();
      expect(wrapper.find('.catalogue-result-region').exists()).toBe(true);
      expect(wrapper.find('.catalogue-refresh').text()).toBe('Refresh');
      expect(wrapper.find('.catalogue-result-region button').exists()).toBe(false);
    },
  );
  it.each(screens)(
    '$name distinguishes initial failure from a successful empty result',
    async ({ component, method, empty }) => {
      vi.mocked(catalogueApi[method]).mockResolvedValue(failure());
      const wrapper = await open(component);
      expect(wrapper.find('.catalogue-empty-state').text()).toBe('Content unavailable');
      expect(wrapper.text()).not.toContain(empty);
      expect(wrapper.text()).not.toContain('500');
      expect(wrapper.find('[aria-busy="true"]').exists()).toBe(false);
      expect(wrapper.find('.catalogue-refresh').exists()).toBe(true);
    },
  );
  it('settles a failed Product editor read as plain unavailable content with contextual Refresh', async () => {
    vi.spyOn(catalogueApi, 'getSettings').mockResolvedValue(failure());
    const get = vi.spyOn(catalogueApi, 'getProduct').mockResolvedValue(failure());
    const wrapper = await open(ProductEditorPage, '/products/nimcha-v01');
    expect(get).toHaveBeenCalledWith('nimcha-v01');
    const unavailable = wrapper
      .findAll('.catalogue-empty-state')
      .find((state) => state.text() === 'Content unavailable')!;
    expect(unavailable.exists()).toBe(true);
    expect(unavailable.element.closest('.panel')).toBeNull();
    expect(wrapper.find('.catalogue-skeleton-product-form').exists()).toBe(false);
    expect(wrapper.text()).not.toContain('Product not found.');
    expect(wrapper.text()).not.toContain('500');
    expect(wrapper.findAll('.catalogue-refresh')).toHaveLength(2);
    expect(wrapper.find('[aria-busy="true"]').exists()).toBe(false);
  });
  it('uses Stock table cells, not image cards, on first load and never reports a confirmed zero', async () => {
    let finish!: (value: Awaited<ReturnType<typeof catalogueApi.stockRows>>) => void;
    vi.mocked(catalogueApi.stockRows).mockReturnValue(
      new Promise((resolve) => {
        finish = resolve;
      }),
    );
    const wrapper = await open(StockPage);
    expect(wrapper.findAll('tbody .catalogue-skeleton-row')).toHaveLength(5);
    expect(wrapper.find('tbody .catalogue-skeleton-row').findAll('td')).toHaveLength(
      wrapper.findAll('thead th').length,
    );
    expect(wrapper.find('.catalogue-skeleton-grid').exists()).toBe(false);
    expect(wrapper.text()).toContain('Pieces by size:');
    expect(wrapper.text()).not.toContain('0 in total');
    expect(wrapper.findAll('[role="status"]')).toHaveLength(1);
    finish(ResultExtensions.ok([]));
    await flushPromises();
    expect(wrapper.find('table').exists()).toBe(false);
    expect(wrapper.find('.catalogue-empty-state').text()).toBe('No products found.');
  });
  it('keeps the successful empty Collections surface during a refresh instead of inserting cards', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    const wrapper = await open(CollectionsPage);
    await wrapper.find('.catalogue-refresh').trigger('click');
    await flushPromises();
    expect(wrapper.find('.catalogue-empty-state').text()).toBe('No collections yet.');
    expect(wrapper.find('.catalogue-skeleton-grid').exists()).toBe(false);
    expect(wrapper.find('.catalogue-refresh').attributes('aria-busy')).toBe('true');
    await vi.advanceTimersByTimeAsync(399);
    await flushPromises();
    expect(wrapper.find('.catalogue-refresh').attributes('aria-busy')).toBe('true');
    await vi.advanceTimersByTimeAsync(1);
    await flushPromises();
    expect(wrapper.find('.catalogue-refresh').attributes('aria-busy')).not.toBe('true');
  });
  it('retains Stock names, rows, controls and last good values after a failed refresh', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    vi.mocked(catalogueApi.stockRows)
      .mockResolvedValueOnce(ResultExtensions.ok([structuredClone(stock)]))
      .mockResolvedValue(failure());
    const wrapper = await open(StockPage);
    const row = wrapper.find('tbody tr').element;
    const header = wrapper.find('thead').element;
    await wrapper.find('.catalogue-refresh').trigger('click');
    await flushPromises();
    expect(wrapper.find('tbody tr').element).toBe(row);
    expect(wrapper.find('thead').element).toBe(header);
    expect(wrapper.text()).toContain('Nimcha');
    expect(wrapper.findAll('[data-loading="true"]').length).toBeGreaterThan(0);
    await vi.advanceTimersByTimeAsync(400);
    await flushPromises();
    expect(wrapper.find('tbody tr').element).toBe(row);
    expect((wrapper.find('input[type="number"]').element as HTMLInputElement).value).toBe('4');
    expect(wrapper.findAll('[data-loading="true"]')).toHaveLength(0);
    expect(wrapper.text()).not.toContain('Content unavailable');
  });
  it.each(editableScreens)('$name disables row and bulk saves during an accepted refresh', async (screen) => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    let finish!: () => void;
    const pending = new Promise<ReturnType<typeof failure>>((resolve) => {
      finish = () => resolve(failure());
    });
    const query = screen.configure(pending);
    const wrapper = await open(screen.component);
    await wrapper.find(screen.selector).setValue(screen.draft);
    const bulkSave = wrapper.find('button.btn-primary');
    const rowSave = wrapper.find('tbody button.btn-outline');
    expect(bulkSave.attributes('disabled')).toBeUndefined();
    expect(rowSave.attributes('disabled')).toBeUndefined();
    await wrapper.find('.catalogue-refresh').trigger('click');
    expect(confirmation.value).not.toBeNull();
    expect(wrapper.findAll('[data-loading="true"]')).toHaveLength(0);
    resolveConfirmation(true);
    await flushPromises();
    expect(query).toHaveBeenCalledTimes(2);
    expect(bulkSave.attributes('disabled')).toBeDefined();
    expect(rowSave.attributes('disabled')).toBeDefined();
    expect(wrapper.find(screen.selector).attributes('disabled')).toBeDefined();
    finish();
    await flushPromises();
    await vi.advanceTimersByTimeAsync(400);
    await flushPromises();
    expect((wrapper.find(screen.selector).element as HTMLInputElement).value).toBe(screen.draft);
    expect(bulkSave.attributes('disabled')).toBeUndefined();
    expect(rowSave.attributes('disabled')).toBeUndefined();
  });
  it.each(editableScreens)('$name cancels dirty refresh without skeletons, a read or lost edits', async (screen) => {
    const query = screen.configure(Promise.resolve(failure()));
    const wrapper = await open(screen.component);
    await wrapper.find(screen.selector).setValue(screen.draft);
    await wrapper.find('.catalogue-refresh').trigger('click');
    expect(confirmation.value).not.toBeNull();
    expect(wrapper.find('.catalogue-refresh').attributes('aria-busy')).not.toBe('true');
    expect(wrapper.findAll('[data-loading="true"]')).toHaveLength(0);
    resolveConfirmation(false);
    await flushPromises();
    expect(query).toHaveBeenCalledTimes(1);
    expect((wrapper.find(screen.selector).element as HTMLInputElement).value).toBe(screen.draft);
    expect(wrapper.find('.catalogue-refresh').attributes('aria-busy')).not.toBe('true');
    expect(wrapper.findAll('[data-loading="true"]')).toHaveLength(0);
  });
  it('keeps Pricing inputs disabled and rejects queued changes while refresh is pending', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    let finish!: () => void;
    vi.mocked(catalogueApi.listProducts)
      .mockResolvedValueOnce(ResultExtensions.ok([structuredClone(product)]))
      .mockReturnValue(
        new Promise((resolve) => {
          finish = () => resolve(failure());
        }),
      );
    const get = vi.spyOn(catalogueApi, 'getProduct');
    const wrapper = await open(PricingPage);
    const input = wrapper.find('input[type="number"]');
    await wrapper.find('.catalogue-refresh').trigger('click');
    expect(input.attributes('disabled')).toBeDefined();
    (input.element as HTMLInputElement).value = '123000';
    input.element.dispatchEvent(new Event('change', { bubbles: true }));
    await flushPromises();
    expect(get).not.toHaveBeenCalled();
    finish();
    await flushPromises();
    await vi.advanceTimersByTimeAsync(400);
    await flushPromises();
    expect(input.attributes('disabled')).toBeUndefined();
  });
  it('keeps product identity and selection mounted while refresh skeletonizes only its values', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    vi.mocked(catalogueApi.listProducts)
      .mockResolvedValueOnce(ResultExtensions.ok([structuredClone(product)]))
      .mockResolvedValue(failure());
    const wrapper = await open(ProductsPage);
    await wrapper.find('button[aria-label="Select nimcha-v01"]').trigger('click');
    const card = wrapper.find('.catalogue-products-grid > li').element;
    await wrapper.find('.catalogue-refresh').trigger('click');
    await flushPromises();
    expect(wrapper.find('.catalogue-products-grid > li').element).toBe(card);
    expect(wrapper.text()).toContain('Waistcoat');
    expect(wrapper.find('button[aria-label="Select nimcha-v01"]').attributes('aria-pressed')).toBe('true');
    expect(wrapper.findAll('[data-loading="true"]').length).toBeGreaterThan(0);
    await vi.advanceTimersByTimeAsync(400);
    await flushPromises();
    expect(wrapper.find('.catalogue-products-grid > li').element).toBe(card);
    expect(wrapper.findAll('[data-loading="true"]')).toHaveLength(0);
    expect(wrapper.text()).not.toContain('500');
  });
});
