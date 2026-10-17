import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { defineComponent, ref } from 'vue';
import { createMemoryHistory, createRouter } from 'vue-router';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import { catalogueApi } from '../src/integration/catalogue';
import { useUiLocale } from '../src/application/shared/UseUiLocale';
import { useCatalogueStore } from '../src/application/catalogue/CatalogueStore';
import { orderProductSizes } from '../src/domain/catalogue/ProductSizes';
import { productReviewFieldLabel } from '../src/domain/catalogue/ProductReviewFields';
import type { ProductSummary } from '../src/domain/catalogue/CatalogueModels';
import ProductEditorPage from '../src/presentation/catalogue/ProductEditorPage.vue';
import ProductsPage from '../src/presentation/catalogue/ProductsPage.vue';
import TagInput from '../src/presentation/catalogue/components/TagInput.vue';
import ProductStatusPicker from '../src/presentation/catalogue/components/ProductStatusPicker.vue';
import { productFixture, sourceFixture } from './NativeDesignFixture';

const wrappers: VueWrapper[] = [];
const SelectStub = {
  props: ['modelValue', 'options', 'label'],
  emits: ['update:modelValue'],
  template:
    '<select :value="modelValue" :aria-label="label" @change="$emit(\'update:modelValue\', $event.target.value)"><option v-for="option in options" :key="option.value" :value="option.value">{{option.label}}</option></select>',
};

async function open(component: typeof ProductEditorPage | typeof ProductsPage, path = '/') {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', component: ProductsPage },
      { path: '/products/:id', component: ProductEditorPage },
    ],
  });
  await router.push(path);
  await router.isReady();
  const wrapper = mount(component, {
    global: {
      plugins: [router],
      stubs: {
        CatalogueSelect: SelectStub,
        Modal: { template: '<div><slot /></div>' },
        ModalContent: { template: '<div><slot /></div>' },
      },
    },
  });
  wrappers.push(wrapper);
  await flushPromises();
  return wrapper;
}

beforeEach(() => {
  useUiLocale().setUiLocale('en');
  useCatalogueStore().settings.value = { ...sourceFixture().settings, sizePreset: ['S', 'M', 'L', 'XL'] };
  vi.spyOn(catalogueApi, 'listProducts').mockResolvedValue(ResultExtensions.ok([]));
  vi.spyOn(catalogueApi, 'listCollections').mockResolvedValue(ResultExtensions.ok([]));
  vi.spyOn(catalogueApi, 'revisions').mockResolvedValue(ResultExtensions.ok([]));
});
afterEach(() => {
  wrappers.splice(0).forEach((wrapper) => wrapper.unmount());
  vi.restoreAllMocks();
});

describe('Product review interactions', () => {
  it('restores configured size order after removal and re-addition without reordering ordinary tags', async () => {
    const host = defineComponent({
      components: { TagInput },
      setup: () => ({ sizes: ref(['S', 'M', 'L']) }),
      template: '<TagInput v-model="sizes" label="Sizes" :suggestions="[\'S\', \'M\', \'L\']" :ordered-sizes="true" />',
    });
    const wrapper = mount(host);
    wrappers.push(wrapper);
    await wrapper.get('button[aria-label="Remove M"]').trigger('click');
    await wrapper
      .findAll('button')
      .find((button) => button.text() === '+ M')!
      .trigger('click');
    expect(wrapper.findAll('.chip').map((chip) => chip.text())).toEqual(['S', 'M', 'L']);
    expect(wrapper.getComponent(TagInput).props('modelValue')).toEqual(['S', 'M', 'L']);
    expect(orderProductSizes(['L', 'S', 'M', 'S'], ['M', 'S', 'L'])).toEqual(['M', 'S', 'L']);
    expect(orderProductSizes(['42', '10', '8', 'XS', 'M', 'XL'])).toEqual(['XS', 'M', 'XL', '8', '10', '42']);
    const tags = mount(TagInput, { props: { modelValue: ['z', 'a'], label: 'Tags' } });
    wrappers.push(tags);
    expect(tags.findAll('.chip').map((chip) => chip.text())).toEqual(['z', 'a']);
  });

  it('keeps one required status with tooltips and only the selected label visible', async () => {
    const host = defineComponent({
      components: { ProductStatusPicker },
      setup: () => ({ status: ref('draft') }),
      template: '<ProductStatusPicker v-model="status" />',
    });
    const wrapper = mount(host);
    wrappers.push(wrapper);
    expect(wrapper.text()).toBe('Draft');
    expect(wrapper.get('button[aria-label="Active"]').attributes('title')).toBe('Active');
    await wrapper.get('button[aria-label="Archived"]').trigger('click');
    expect(wrapper.text()).toBe('Archived');
    expect(wrapper.get('button[aria-label="Archived"]').attributes('aria-pressed')).toBe('true');
    await wrapper.get('button[aria-label="Archived"]').trigger('click');
    expect(wrapper.get('button[aria-label="Archived"]').attributes('aria-pressed')).toBe('true');
  });

  it('keeps translation confirmation language-specific and preserves imported issue identities when saving', async () => {
    const product = productFixture();
    product.document.issues = [
      { id: 'source-1', field: 'assets.product_reference_images', status: 'open', question: 'Verify reference photo' },
    ];
    vi.spyOn(catalogueApi, 'getProduct').mockResolvedValue(ResultExtensions.ok(product));
    const update = vi.spyOn(catalogueApi, 'updateProduct').mockResolvedValue(ResultExtensions.ok(product));
    const wrapper = await open(ProductEditorPage, '/products/' + product.document.id);
    const body = wrapper.get('.catalogue-product-body');
    expect(body.text()).toContain('Reference images');
    expect(body.text()).not.toContain('assets.product_reference_images');
    expect(body.text()).toContain('I confirm the translation');
    expect(body.text()).toContain('English');
    const copy = body.get('section[aria-labelledby="copy-heading"]');
    await copy.get('input[type="checkbox"]').setValue(false);
    await wrapper
      .findAll('button')
      .find((button) => button.text() === 'Save')!
      .trigger('click');
    await flushPromises();
    const payload = update.mock.calls[0]![1];
    expect(payload.locales.en.status).toBe('draft');
    expect(payload.locales.uz.status).toBe('approved');
    expect(payload.locales.ru.status).toBe('approved');
    expect(payload.issues[0]!.field).toBe('assets.product_reference_images');
    expect(body.find('.catalogue-product-footer').exists()).toBe(false);
    expect(wrapper.get('.catalogue-product-footer').find('button').exists()).toBe(true);
    expect(
      body.findAll('textarea').some((field) => field.element.parentElement?.textContent?.includes('Alt text')),
    ).toBe(true);
  });

  it('shows one warning translation pill and toggles the glass-backed card selection', async () => {
    const product: ProductSummary = {
      id: 'nimcha-v01',
      garmentType: 'nimcha',
      variantCode: 'v01',
      status: 'draft',
      sortOrder: 0,
      nameUz: 'Nimcha',
      nameRu: null,
      nameEn: 'Waistcoat',
      retailPrice: null,
      wholesalePrice: null,
      pricingStatus: null,
      localesApproved: 0,
      languagesTotal: 3,
      unconfirmedTranslations: 3,
      openIssues: 0,
      mediaCount: 0,
      primaryImageUrl: null,
      primaryPreviewUrl: null,
      tags: [],
      revision: 1,
      updatedAt: '2026-10-06T00:00:00Z',
      availability: 'unknown',
      stockTotal: null,
    };
    vi.mocked(catalogueApi.listProducts).mockResolvedValue(ResultExtensions.ok([product]));
    const wrapper = await open(ProductsPage);
    const card = wrapper.get('.catalogue-products-grid > li');
    expect(card.findAll('.chip').filter((chip) => chip.text().includes('languages confirmed'))).toHaveLength(1);
    expect(card.text()).not.toContain('unconfirmed translations');
    expect(card.findAll('.chip.bg-warning-soft').some((chip) => chip.text().includes('0/3'))).toBe(true);
    const selection = card.get('button[aria-label="Select nimcha-v01"]');
    expect(selection.classes()).toContain('catalogue-image-action');
    await selection.trigger('click');
    expect(selection.attributes('aria-pressed')).toBe('true');
    await selection.trigger('click');
    expect(selection.attributes('aria-pressed')).toBe('false');
  });

  it('keeps unrecognized review fields visible without relabelling unrelated facts', () => {
    expect(productReviewFieldLabel('custom.inspection')).toBe('custom.inspection');
  });
});
