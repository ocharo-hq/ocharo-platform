import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { createMemoryHistory, createRouter } from 'vue-router';
import CatalogueGalleryPage from '../src/presentation/design/CatalogueGalleryPage.vue';
import CatalogueWebDesignerPage from '../src/presentation/design/CatalogueWebDesignerPage.vue';
import CatalogueWebPreview from '../src/presentation/design/CatalogueWebPreview.vue';
import { designApi, DesignRequestError } from '../src/integration/design/DesignApi';
import { ApiFailureFactory } from '@wow-two-beta/ui-vue/foundation/http';
import { useUiLocale } from '../src/application/shared/UseUiLocale';
import { feedbackBus } from '../src/application/common/UseFeedback';
let messages: string[] = [];
let unsubscribe = () => {};
let notificationClock = Date.now();
afterEach(() => {
  unsubscribe();
  vi.restoreAllMocks();
});
import { documentFixture, sourceFixture, productFixture } from './NativeDesignFixture';
vi.mock('../src/integration/design/DesignApi', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../src/integration/design/DesignApi')>()),
  designApi: {
    list: vi.fn(),
    get: vi.fn(),
    source: vi.fn(),
    save: vi.fn(),
    duplicate: vi.fn(),
    archive: vi.fn(),
    revisions: vi.fn(),
    releases: vi.fn(),
    preparation: vi.fn(),
    prepare: vi.fn(),
    publish: vi.fn(),
    withdraw: vi.fn(),
  },
}));
const SelectStub = {
  props: ['modelValue', 'options', 'label'],
  emits: ['update:modelValue'],
  template:
    '<select :value="modelValue" :aria-label="label" @change="$emit(\'update:modelValue\',$event.target.value)"><option v-for="option in options" :key="option.value" :value="option.value">{{option.label}}</option></select>',
};
async function open(component: typeof CatalogueGalleryPage | typeof CatalogueWebDesignerPage, path: string) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/catalogues', component: CatalogueGalleryPage },
      { path: '/catalogues/:id/web', component: CatalogueWebDesignerPage },
    ],
  });
  await router.push(path);
  await router.isReady();
  const wrapper = mount(component, { global: { plugins: [router], stubs: { CatalogueSelect: SelectStub } } });
  await flushPromises();
  return { wrapper, router };
}
beforeEach(() => {
  vi.clearAllMocks();
  notificationClock += 10_001;
  vi.spyOn(Date, 'now').mockImplementation(() => notificationClock);
  messages = [];
  unsubscribe = feedbackBus.subscribe((notice) => messages.push(String(notice.description)));
  sessionStorage.clear();
  useUiLocale().setUiLocale('en');
  const doc = documentFixture();
  vi.mocked(designApi.list).mockResolvedValue([
    {
      ...doc,
      locale: 'en',
      catalogue: { ...doc.snapshot.catalogue!, web: { theme: 'editorial', pageCount: 0 }, pdfPageCount: 1 },
    },
  ]);
  vi.mocked(designApi.get).mockResolvedValue(doc);
  vi.mocked(designApi.source).mockResolvedValue(sourceFixture());
  vi.mocked(designApi.save).mockImplementation(async (id, title, snapshot, revision) => ({
    ...documentFixture(),
    id: id ?? 'new',
    title,
    snapshot,
    revision: (revision ?? 0) + 1,
  }));
});
describe('native catalogue screens', () => {
  it('keeps the last catalogue grid while a failed duplicate is pending', async () => {
    let reject!: (cause: Error) => void;
    vi.mocked(designApi.duplicate).mockReturnValue(
      new Promise((_resolve, failure) => {
        reject = failure;
      }),
    );
    const { wrapper } = await open(CatalogueGalleryPage, '/catalogues');
    const button = wrapper.findAll('button').find((item) => item.text() === 'Duplicate')!;
    await button.trigger('click');
    expect(button.attributes('aria-busy')).toBe('true');
    expect(wrapper.findAll('.catalogue-grid article')).toHaveLength(1);
    expect(designApi.list).toHaveBeenCalledTimes(1);
    reject(new Error('Duplicate rejected'));
    await flushPromises();
    expect(wrapper.findAll('.catalogue-grid article')).toHaveLength(1);
    expect(designApi.list).toHaveBeenCalledTimes(1);
    expect(button.attributes('aria-busy')).toBe('false');
    expect(messages).toContain('Unable to load catalogues.');
    wrapper.unmount();
  });
  it('refreshes the gallery only after a successful duplicate acknowledgement', async () => {
    let resolve!: (value: ReturnType<typeof documentFixture>) => void;
    vi.mocked(designApi.duplicate).mockReturnValue(
      new Promise((success) => {
        resolve = success;
      }),
    );
    const { wrapper } = await open(CatalogueGalleryPage, '/catalogues');
    await wrapper
      .findAll('button')
      .find((item) => item.text() === 'Duplicate')!
      .trigger('click');
    expect(designApi.list).toHaveBeenCalledTimes(1);
    resolve(documentFixture());
    await flushPromises();
    expect(designApi.list).toHaveBeenCalledTimes(2);
    wrapper.unmount();
  });
  it('finishes a failed retry without permanent skeletons and retains the last grid', async () => {
    const { wrapper } = await open(CatalogueGalleryPage, '/catalogues');
    vi.mocked(designApi.duplicate).mockResolvedValue(documentFixture());
    vi.mocked(designApi.list).mockRejectedValue(new Error('Refresh unavailable'));
    await wrapper
      .findAll('button')
      .find((item) => item.text() === 'Duplicate')!
      .trigger('click');
    await flushPromises();
    expect(wrapper.findAll('.catalogue-grid article')).toHaveLength(1);
    expect(wrapper.text()).not.toContain('Refresh unavailable');
    await wrapper
      .findAll('button')
      .find((item) => item.text() === 'Try again')!
      .trigger('click');
    await flushPromises();
    expect(wrapper.findAll('.catalogue-grid article')).toHaveLength(1);
    expect(wrapper.find('[aria-busy="true"]').exists()).toBe(false);
    expect(messages.filter((item) => item === 'Unable to load catalogues.')).toHaveLength(1);
    wrapper.unmount();
  });
  it('shows gallery format routes and last edited identity', async () => {
    const { wrapper } = await open(CatalogueGalleryPage, '/catalogues');
    expect(wrapper.text()).toContain('Woven days');
    expect(wrapper.text()).toContain('Last edited');
    expect(wrapper.text()).toContain('Web designer');
    expect(wrapper.text()).toContain('PDF designer');
    wrapper.unmount();
  });
  it('keeps failed gallery loading distinct from an empty catalogue', async () => {
    vi.mocked(designApi.list).mockRejectedValue(new Error('Service unavailable'));
    const { wrapper } = await open(CatalogueGalleryPage, '/catalogues');
    expect(wrapper.text()).not.toContain('Service unavailable');
    expect(wrapper.text()).toContain('Content unavailable');
    expect(wrapper.find('[aria-busy="true"]').exists()).toBe(false);
    expect(messages).toContain('Unable to load catalogues.');
    expect(wrapper.text()).toContain('Try again');
    expect(wrapper.text()).not.toContain('Create your first catalogue.');
    wrapper.unmount();
  });
  it.each([
    { locale: 'uz' as const, expected: 'Katalog xizmati 503 xatosini qaytardi.' },
    { locale: 'ru' as const, expected: 'Служба каталога вернула ошибку 503.' },
  ])('localizes numeric request failures in the $locale gallery and designer', async ({ locale, expected }) => {
    useUiLocale().setUiLocale(locale);
    const failure = new DesignRequestError(ApiFailureFactory.create('http', new Response(null, { status: 503 })));
    vi.mocked(designApi.list).mockRejectedValue(failure);
    vi.mocked(designApi.get).mockRejectedValue(failure);
    const gallery = await open(CatalogueGalleryPage, '/catalogues');
    expect(gallery.wrapper.text()).not.toContain(expected);
    expect(messages).toContain(expected);
    expect(gallery.wrapper.text()).not.toContain('The catalogue service returned error 503.');
    gallery.wrapper.unmount();
    const designer = await open(CatalogueWebDesignerPage, '/catalogues/' + documentFixture().id + '/web');
    expect(designer.wrapper.text()).not.toContain(expected);
    expect(messages).toContain(expected);
    expect(designer.wrapper.text()).not.toContain('The catalogue service returned error 503.');
    designer.wrapper.unmount();
  });

  it('adds a page and saves the same document through the native API', async () => {
    const { wrapper } = await open(CatalogueWebDesignerPage, '/catalogues/' + documentFixture().id + '/web');
    await wrapper
      .findAll('button')
      .find((button) => button.text().includes('Add page'))!
      .trigger('click');
    await wrapper
      .findAll('button')
      .find((button) => button.text() === 'Save catalogue')!
      .trigger('click');
    await flushPromises();
    expect(designApi.save).toHaveBeenCalled();
    const args = vi.mocked(designApi.save).mock.calls[0]!;
    expect(args[0]).toBe(documentFixture().id);
    expect(args[3]).toBe(2);
    expect(args[2].catalogue!.web.pages).toHaveLength(1);
    expect(args[2].pages).toHaveLength(1);
    wrapper.unmount();
  });
  it('changes catalogue content language while preserving the Uzbek interface', async () => {
    useUiLocale().setUiLocale('uz');
    const { wrapper } = await open(CatalogueWebDesignerPage, '/catalogues/' + documentFixture().id + '/web');
    const selector = wrapper.findAll('select')[0]!;
    await selector.setValue('fr');
    expect(useUiLocale().uiLocale.value).toBe('uz');
    const save = wrapper.findAll('button').find((button) => button.text() === useUiLocale().t('Save catalogue'))!;
    await save.trigger('click');
    await flushPromises();
    expect(vi.mocked(designApi.save).mock.calls[0]![2].locale).toBe('fr');
    wrapper.unmount();
  });
  it('renders only approved web siblings with layout-specific photo caps', () => {
    const a = productFixture(),
      b = productFixture('baska-v01');
    b.document.locales.en.status = 'draft';
    const snapshot = documentFixture().snapshot;
    snapshot.catalogue!.web.pages = [
      { id: 'web', title: 'Looks', layout: 'spread', isActive: true, productIds: [a.document.id, b.document.id] },
    ];
    const wrapper = mount(CatalogueWebPreview, { props: { snapshot, products: [a, b], title: 'Woven days' } });
    expect(wrapper.findAll('article')).toHaveLength(1);
    expect(wrapper.findAll('img')).toHaveLength(3);
    wrapper.unmount();
  });
  it('hides malformed confirmed prices from the web preview', () => {
    const product = productFixture();
    product.document.pricing!.tiers[0]!.maximumQuantity = 15;
    const snapshot = documentFixture().snapshot;
    snapshot.catalogue!.web.pages = [
      { id: 'web', title: 'Looks', layout: 'grid', isActive: true, productIds: [product.document.id] },
    ];
    const wrapper = mount(CatalogueWebPreview, { props: { snapshot, products: [product], title: 'Woven days' } });
    expect(wrapper.text()).toContain('Price on request');
    expect(wrapper.text()).not.toContain('150,000');
    wrapper.unmount();
  });
  it('clears the previous catalogue recovery when navigating to another design', async () => {
    const first = documentFixture(),
      second = { ...documentFixture(), id: '22222222-2222-2222-2222-222222222222', title: 'Second catalogue' };
    sessionStorage.setItem(
      'ocharo-catalogue-design:' + first.id,
      JSON.stringify({ title: 'First recovery', snapshot: first.snapshot }),
    );
    vi.mocked(designApi.get).mockImplementation(async (id) => (id === first.id ? first : second));
    const { wrapper, router } = await open(CatalogueWebDesignerPage, '/catalogues/' + first.id + '/web');
    expect(wrapper.text()).toContain('Restore recovery copy');
    await router.push('/catalogues/' + second.id + '/web');
    await flushPromises();
    expect(wrapper.text()).not.toContain('Restore recovery copy');
    expect(wrapper.find('input[maxlength="160"]').element).toHaveProperty('value', 'Second catalogue');
    wrapper.unmount();
  });
  it('does not keep catalogue A editable under a failed catalogue B route', async () => {
    const first = documentFixture();
    vi.mocked(designApi.get).mockImplementation(async (id) => {
      if (id !== first.id) throw new Error('Catalogue B unavailable');
      return first;
    });
    const { wrapper, router } = await open(CatalogueWebDesignerPage, '/catalogues/' + first.id + '/web');
    await router.push('/catalogues/22222222-2222-2222-2222-222222222222/web');
    await flushPromises();
    expect(wrapper.text()).not.toContain('Catalogue B unavailable');
    expect(messages).toContain('Unable to save this catalogue.');
    expect(wrapper.text()).not.toContain('Save catalogue');
    expect(wrapper.find('input[maxlength="160"]').exists()).toBe(false);
    wrapper.unmount();
  });
});
