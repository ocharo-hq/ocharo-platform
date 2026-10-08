import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { createMemoryHistory, createRouter } from 'vue-router';
import CatalogueWebDesignerPage from '../src/presentation/design/CatalogueWebDesignerPage.vue';
import CatalogueWebPreview from '../src/presentation/design/CatalogueWebPreview.vue';
import { designApi } from '../src/integration/design/DesignApi';
import { useUiLocale } from '../src/application/shared/UseUiLocale';
import { documentFixture, sourceFixture } from './NativeDesignFixture';

vi.mock('../src/integration/design/DesignApi', async (original) => ({
  ...(await original<typeof import('../src/integration/design/DesignApi')>()),
  designApi: { get: vi.fn(), source: vi.fn(), save: vi.fn(), publish: vi.fn() },
}));

const SelectStub = {
  props: ['modelValue', 'options', 'label'],
  emits: ['update:modelValue'],
  template:
    '<select :value="modelValue" :aria-label="label" @change="$emit(\'update:modelValue\', $event.target.value)"><option v-for="option in options" :key="option.value" :value="option.value">{{ option.label }}</option></select>',
};

let dispose: (() => void) | undefined;
beforeEach(() => {
  vi.clearAllMocks();
  sessionStorage.clear();
  useUiLocale().setUiLocale('en');
  const doc = documentFixture();
  const source = sourceFixture();
  source.products[0]!.document.locales.en.status = 'draft';
  doc.snapshot.catalogue!.web.pages = [
    {
      id: 'page-one',
      title: 'First page',
      isActive: true,
      layout: 'spread',
      productIds: [source.products[0]!.document.id],
    },
  ];
  vi.mocked(designApi.get).mockResolvedValue(doc);
  vi.mocked(designApi.source).mockResolvedValue(source);
});
afterEach(() => {
  dispose?.();
  dispose = undefined;
});

async function open() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/catalogues/:id/web', component: CatalogueWebDesignerPage }],
  });
  await router.push('/catalogues/' + documentFixture().id + '/web');
  await router.isReady();
  const wrapper = mount(CatalogueWebDesignerPage, {
    global: { plugins: [router], stubs: { CatalogueSelect: SelectStub } },
  });
  dispose = () => wrapper.unmount();
  await flushPromises();
  return wrapper;
}

describe('web designer draft canvas', () => {
  it('renders unapproved selections and switches layouts without saving or publishing', async () => {
    const wrapper = await open();
    expect(wrapper.findComponent(CatalogueWebPreview).props('editing')).toBe(true);
    expect(wrapper.find('.web-page article').text()).toContain('Waistcoat');
    expect(wrapper.find('.web-page article').text()).toContain('Needs review');
    await wrapper.get('button[aria-label="Product grid"]').trigger('click');
    await flushPromises();
    expect(wrapper.find('.web-page.grid article').exists()).toBe(true);
    expect(wrapper.get('button[aria-label="Product grid"]').attributes('aria-pressed')).toBe('true');
    expect(wrapper.text()).toContain('Unsaved changes');
    expect(designApi.save).not.toHaveBeenCalled();
    expect(designApi.publish).not.toHaveBeenCalled();
  });

  it('keeps a new empty page visible and permits choosing its grid', async () => {
    const wrapper = await open();
    await wrapper
      .findAll('button')
      .find((button) => button.text() === '+ Add page')!
      .trigger('click');
    await flushPromises();
    expect(wrapper.find('.web-page h2').text()).toContain('New page');
    await wrapper.get('button[aria-label="Product grid"]').trigger('click');
    await flushPromises();
    expect(wrapper.find('.web-page.grid').exists()).toBe(true);
    expect(wrapper.find('.web-page').text()).toContain('Choose products to fill this layout.');
    expect(wrapper.findAll('.pages li')).toHaveLength(2);
    await wrapper.findAll('.page-name')[0]!.trigger('click');
    await flushPromises();
    expect(wrapper.find('.web-page h2').text()).toBe('First page');
    expect(wrapper.find('.web-page article').text()).toContain('Waistcoat');
  });

  it('restores the unsaved layout and selections without writing to the server', async () => {
    const recovery = documentFixture();
    recovery.snapshot.catalogue!.web.pages = [
      {
        id: 'recovery-page',
        title: 'Recovered draft page',
        layout: 'grid',
        isActive: false,
        productIds: ['nimcha-v01'],
      },
    ];
    sessionStorage.setItem(
      'ocharo-catalogue-design:' + recovery.id,
      JSON.stringify({
        title: 'My unsaved catalogue',
        snapshot: recovery.snapshot,
      }),
    );
    const wrapper = await open();
    await wrapper
      .findAll('button')
      .find((button) => button.text() === 'Restore recovery copy')!
      .trigger('click');
    await flushPromises();
    expect(wrapper.find('.web-page.grid h2').text()).toContain('Recovered draft page');
    expect(wrapper.find('.web-page article').text()).toContain('Waistcoat');
    expect(wrapper.find('.web-page').text()).toContain('Inactive page');
    expect(designApi.save).not.toHaveBeenCalled();
  });
});
