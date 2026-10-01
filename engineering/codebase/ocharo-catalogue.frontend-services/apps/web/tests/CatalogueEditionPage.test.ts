import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { createMemoryHistory, createRouter } from 'vue-router';
import { ApiFailureFactory } from '@wow-two-beta/ui-vue/foundation/http';
import { ResultExtensions } from '@wow-two-beta/ui-vue/foundation/results';
import CatalogueEditionPage from '../src/presentation/public/CatalogueEditionPage.vue';
import { publishedCatalogueApi } from '../src/integration/public/PublishedCatalogueApi';
import { feedbackBus } from '../src/application/common/UseFeedback';
import { useUiLocale } from '../src/application/shared/UseUiLocale';
import { decodePublishedCatalogue } from '../src/domain/public/PublishedCatalogue';
import { fixture, releaseId, pinned } from './PublishedCatalogueFixture';

vi.mock('../src/integration/public/PublishedCatalogueApi', () => ({ publishedCatalogueApi: { read: vi.fn() } }));
const read = vi.mocked(publishedCatalogueApi.read);
const notices = vi.fn(),
  clipboard = vi.fn();
const SelectStub = {
  props: ['modelValue', 'options', 'label'],
  emits: ['update:modelValue'],
  template:
    '<select :value="modelValue" :aria-label="label" @change="$emit(\'update:modelValue\',$event.target.value)"><option v-for="option in options" :key="option.value" :value="option.value">{{option.label}}</option></select>',
};
async function open(path = '/c/woven-days?locale=en') {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/c/:slug', component: CatalogueEditionPage }],
  });
  await router.push(path);
  await router.isReady();
  const wrapper = mount(CatalogueEditionPage, {
    global: { plugins: [router], stubs: { CatalogueSelect: SelectStub } },
  });
  await flushPromises();
  return { wrapper, router };
}
const success = () => ResultExtensions.ok(decodePublishedCatalogue(fixture(), 'woven-days', 'en')!);
beforeEach(() => {
  read.mockReset();
  read.mockResolvedValue(success());
  localStorage.clear();
  useUiLocale().setUiLocale('en');
  notices.mockClear();
  vi.spyOn(feedbackBus, 'notify').mockImplementation(notices);
  clipboard.mockReset();
  clipboard.mockResolvedValue(undefined);
  Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: clipboard } });
});

describe('native published catalogue reader', () => {
  it('shows one accessible skeleton until the published edition arrives', async () => {
    let finish: ((value: Awaited<ReturnType<typeof publishedCatalogueApi.read>>) => void) | undefined;
    read.mockImplementation(
      () =>
        new Promise((resolve) => {
          finish = resolve;
        }),
    );
    const { wrapper } = await open();
    expect(
      wrapper.findAll('[role="status"]').filter((element) => element.text().includes('Loading catalogue')),
    ).toHaveLength(1);
    expect(wrapper.find('.web-reader').exists()).toBe(false);
    finish?.(success());
    await flushPromises();
    expect(wrapper.find('.web-reader').exists()).toBe(true);
    wrapper.unmount();
  });
  it('renders semantic web content, selected media and wholesale prices independently from PDF pages', async () => {
    const { wrapper } = await open();
    expect(wrapper.find('.web-reader').text()).toContain('Woven days');
    expect(wrapper.find('.web-reader').text()).toContain('Made with care');
    expect(wrapper.find('.web-reader').text()).toContain('100,000 UZS');
    expect(wrapper.find('.web-reader').text()).not.toContain('130,000');
    expect(wrapper.find('.web-reader img').attributes('src')).toContain('b'.repeat(64));
    expect(wrapper.find('.pdf-reader').exists()).toBe(false);
    const download = wrapper.findAll('a').find((link) => link.text().includes('Download catalogue PDF'));
    expect(download?.attributes('href')).toBe(pinned('catalogue.pdf'));
    const button = wrapper.findAll('button').find((item) => item.text() === 'PDF pages')!;
    await button.trigger('click');
    expect(wrapper.find('.pdf-reader').exists()).toBe(true);
    expect(wrapper.find('.pdf-reader img').attributes('src')).toBe(pinned('page-one.png'));
    expect(wrapper.find('.pdf-reader a[download]').attributes('href')).toBe(pinned('page-one.pdf'));
    wrapper.unmount();
  });
  it('only offers published languages and never alters the interface locale', async () => {
    const { wrapper, router } = await open(`/c/woven-days?locale=en&release=${releaseId}`);
    expect(wrapper.findAll('option').map((option) => option.attributes('value'))).toEqual(['en', 'fr']);
    read.mockResolvedValue(
      ResultExtensions.fail(ApiFailureFactory.create('http', new Response(null, { status: 404 }))),
    );
    await wrapper.find('select').setValue('fr');
    await flushPromises();
    expect(router.currentRoute.value.query).toEqual({ locale: 'fr' });
    expect(useUiLocale().uiLocale.value).toBe('en');
    expect(wrapper.find('.web-reader').exists()).toBe(false);
    expect(wrapper.text()).toContain('not published in this language');
    wrapper.unmount();
  });
  it('keeps 500 failures as toast plus retry, never a successful empty catalogue', async () => {
    read.mockResolvedValueOnce(
      ResultExtensions.fail(ApiFailureFactory.create('http', new Response(null, { status: 500 }))),
    );
    const { wrapper } = await open();
    expect(wrapper.find('.catalogue-unavailable').exists()).toBe(true);
    expect(notices).toHaveBeenCalledOnce();
    expect(notices.mock.calls[0]?.[0].description).toContain('Please retry');
    await wrapper.find('button').trigger('click');
    await flushPromises();
    expect(wrapper.find('.web-reader').exists()).toBe(true);
    wrapper.unmount();
  });
  it('copies the exact published release link and saves a piece locally', async () => {
    const { wrapper } = await open();
    await wrapper.find('.save-piece').trigger('click');
    expect(wrapper.find('.save-piece').attributes('aria-pressed')).toBe('true');
    expect(JSON.parse(localStorage.getItem('ocharo.catalogue.saved-pieces')!)).toEqual(['nimcha-v01']);
    const share = wrapper.findAll('button').find((button) => button.text() === 'Share catalogue')!;
    await share.trigger('click');
    await flushPromises();
    expect(clipboard.mock.calls[0]?.[0]).toContain(`release=${releaseId}`);
    expect(clipboard.mock.calls[0]?.[0]).toContain('locale=en');
    expect(wrapper.text()).toContain('Link copied');
    wrapper.unmount();
  });
  it('rejects unsupported language before requesting an edition', async () => {
    const { wrapper } = await open('/c/woven-days?locale=invalid');
    expect(read).not.toHaveBeenCalled();
    expect(wrapper.find('.catalogue-unavailable').exists()).toBe(true);
    wrapper.unmount();
  });
});
