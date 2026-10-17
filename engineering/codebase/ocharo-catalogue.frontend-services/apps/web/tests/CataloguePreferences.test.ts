import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { DOMWrapper, flushPromises, mount } from '@vue/test-utils';
import { createMemoryHistory, createRouter } from 'vue-router';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import AppRoot from '../src/bootstrap/AppRoot.vue';
import SettingsPage from '../src/presentation/catalogue/SettingsPage.vue';
import { catalogueApi } from '../src/integration/catalogue';
import { useCatalogueStore } from '../src/application/catalogue';
import { initializeUiLocale, useUiLocale } from '../src/application/shared/UseUiLocale';
import { useAppearance } from '../src/application/shared/UseAppearance';
import type { CatalogueSettings } from '../src/domain/catalogue/CatalogueModels';

// Only replace the popup select interaction; keep the real shell, page and SDK modal.
const SelectStub = {
  props: ['id', 'modelValue', 'options', 'label'],
  emits: ['update:modelValue'],
  template:
    '<select :id="id" :value="modelValue" :aria-label="label" @change="$emit(\'update:modelValue\',$event.target.value)"><option v-for="option in options" :key="option.value" :value="option.value">{{option.label}}</option></select>',
};
const authoredDefaults: CatalogueSettings = {
  brandName: 'Ocharo authored name',
  currency: 'UZS',
  priceNote: 'Authored catalogue price note',
  sizePreset: ['S', 'M'],
  contactLines: ['@authored_contact'],
  garmentTypes: [{ key: 'nimcha', uz: 'Nimcha', ru: 'Жилет', en: 'Waistcoat' }],
};
const mounted: Array<{ unmount: () => void }> = [];

async function openApp(path = '/settings') {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/settings', component: SettingsPage },
      { path: '/:pathMatch(.*)*', component: { template: '<p>Published catalogue content</p>' } },
    ],
  });
  await router.push(path);
  await router.isReady();
  initializeUiLocale();
  const wrapper = mount(AppRoot, {
    attachTo: document.body,
    global: { plugins: [router], stubs: { CatalogueSelect: SelectStub } },
  });
  mounted.push(wrapper);
  await flushPromises();
  return wrapper;
}

beforeEach(() => {
  localStorage.clear();
  useUiLocale().setUiLocale('en');
  const store = useCatalogueStore();
  store.settings.value = null;
  store.settingsRevision.value = 0;
  store.sourceRoot.value = null;
  store.error.value = null;
  store.notice.value = null;
  useAppearance().initializeAppearance();
  vi.spyOn(catalogueApi, 'getSettings').mockResolvedValue(
    ResultExtensions.ok({ settings: structuredClone(authoredDefaults), revision: 7, sourceRoot: null }),
  );
  vi.spyOn(catalogueApi, 'saveSettings');
  vi.spyOn(catalogueApi, 'importWorkspace');
});
afterEach(() => {
  mounted.splice(0).forEach((wrapper) => wrapper.unmount());
  document.body.innerHTML = '';
});

describe('Catalogue interface preferences', () => {
  it('starts with catalogues and exposes operator preferences once inside Settings', async () => {
    const wrapper = await openApp();
    const header = wrapper.get('header[role="banner"]');
    const navigation = header.findAll('nav');
    expect(navigation).toHaveLength(2); // Desktop and mobile representations share the same order.
    for (const nav of navigation) {
      expect(nav.findAll('a').map((link) => link.text())).toEqual([
        'Catalogues',
        'Collections',
        'Products',
        'Stock',
        'Settings',
      ]);
      expect(nav.findAll('a').map((link) => link.attributes('href'))).toEqual([
        '/catalogues',
        '/collections',
        '/',
        '/stock',
        '/settings',
      ]);
    }
    expect(header.findAll('button')).toHaveLength(2);
    expect(header.find('[aria-label="Notifications"]').exists()).toBe(true);
    expect(header.get('button').attributes('aria-haspopup')).toBe('dialog');
    expect(wrapper.findAll('.interface-preferences')).toHaveLength(1);
    expect(wrapper.get('#interface-settings-heading').text()).toBe('Interface settings');
    expect(wrapper.get('#catalogue-defaults-heading').text()).toBe('Catalogue defaults');
    expect(wrapper.get('#import-heading').text()).toBe('Import product records');
    expect(wrapper.get('#export-heading').text()).toBe('Download catalogue data');
  });

  it('updates English, Uzbek and Russian immediately without translating or saving authored defaults', async () => {
    const wrapper = await openApp();
    await wrapper.get('#brand').setValue('Unsaved authored name');
    const language = wrapper.get('#interface-language');
    await language.setValue('uz');
    expect(wrapper.get('h1').text()).toBe('Sozlamalar');
    expect(wrapper.get('#interface-settings-heading').text()).toBe('Interfeys sozlamalari');
    expect(wrapper.get('#import-heading').text()).toBe('Mahsulot yozuvlarini import qilish');
    expect(wrapper.get('#brand').element).toHaveProperty('value', 'Unsaved authored name');
    await language.setValue('ru');
    expect(wrapper.get('h1').text()).toBe('Настройки');
    expect(wrapper.get('#interface-settings-heading').text()).toBe('Настройки интерфейса');
    expect(wrapper.get('#import-heading').text()).toBe('Импорт записей товаров');
    expect(wrapper.get('#export-heading').text()).toBe('Скачать данные каталога');
    expect(wrapper.get('#brand').element).toHaveProperty('value', 'Unsaved authored name');
    expect(wrapper.get('#note').element).toHaveProperty('value', authoredDefaults.priceNote);
    await wrapper.get('#interface-appearance').setValue('dark');
    expect(document.documentElement.classList.contains('dark')).toBe(true);
    expect(localStorage.getItem('ocharo.operator.locale')).toBe('ru');
    expect(localStorage.getItem('ocharo.catalogue.theme')).toBe('dark');
    expect(useCatalogueStore().settings.value).toEqual(authoredDefaults);
    expect(catalogueApi.saveSettings).not.toHaveBeenCalled();
    expect(catalogueApi.importWorkspace).not.toHaveBeenCalled();
  });

  it('restores browser preferences on a new mount while catalogue defaults remain unchanged', async () => {
    const first = await openApp();
    await first.get('#interface-language').setValue('ru');
    await first.get('#interface-appearance').setValue('dark');
    first.unmount();
    mounted.splice(mounted.indexOf(first), 1);
    // Reset in-memory state to prove startup reads the persisted browser choice.
    useUiLocale().uiLocale.value = 'en';
    useAppearance().appearance.value = 'light';
    document.documentElement.classList.remove('dark');
    const second = await openApp();
    expect(second.get('#interface-language').element).toHaveProperty('value', 'ru');
    expect(second.get('#interface-appearance').element).toHaveProperty('value', 'dark');
    expect(second.get('h1').text()).toBe('Настройки');
    expect(document.documentElement.lang).toBe('ru');
    expect(document.documentElement.classList.contains('dark')).toBe(true);
    expect(useCatalogueStore().settings.value).toEqual(authoredDefaults);
    expect(catalogueApi.saveSettings).not.toHaveBeenCalled();
    expect(catalogueApi.importWorkspace).not.toHaveBeenCalled();
  });

  it('keeps public reader settings limited to browser preferences without loading operator data', async () => {
    const wrapper = await openApp('/c/published-example?locale=fr');
    const header = wrapper.get('header[role="banner"]');
    expect(header.findAll('nav')).toHaveLength(0);
    expect(header.find('button[aria-haspopup="dialog"]').exists()).toBe(false);
    expect(catalogueApi.getSettings).not.toHaveBeenCalled();
    await header.get('button[aria-label="Settings"]').trigger('click');
    await flushPromises();
    const element = document.querySelector<HTMLElement>('[role="dialog"]');
    expect(element).not.toBeNull();
    const dialog = new DOMWrapper(element!);
    expect(dialog.findAll('select').map((select) => select.attributes('id'))).toEqual([
      'interface-language',
      'interface-appearance',
    ]);
    expect(dialog.text()).not.toContain('Catalogue defaults');
    expect(dialog.text()).not.toContain('Import product records');
    expect(dialog.find('#brand').exists()).toBe(false);
    await dialog.get('#interface-language').setValue('uz');
    await dialog.get('#interface-appearance').setValue('dark');
    expect(dialog.text()).toContain('Interfeys tili');
    expect(dialog.text()).toContain('Ko‘rinish');
    expect(wrapper.text()).toContain('Published catalogue content');
    expect(catalogueApi.getSettings).not.toHaveBeenCalled();
    expect(catalogueApi.saveSettings).not.toHaveBeenCalled();
    expect(catalogueApi.importWorkspace).not.toHaveBeenCalled();
  });
});
