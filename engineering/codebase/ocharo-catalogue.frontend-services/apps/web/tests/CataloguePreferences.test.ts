import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { DOMWrapper, flushPromises, type VueWrapper } from '@vue/test-utils';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import { catalogueApi } from '../src/integration/catalogue';
import { useCatalogueStore } from '../src/application/catalogue';
import { initializeUiLocale, useUiLocale } from '../src/application/shared/UseUiLocale';
import { ThemeStorageKey } from '../src/bootstrap/theme';
import type { CatalogueSettings } from '../src/domain/catalogue/CatalogueModels';
import { mountCatalogue, SelectStub } from './helpers/CatalogueApp';

const authoredDefaults: CatalogueSettings = {
  brandName: 'Ocharo authored name',
  currency: 'UZS',
  priceNote: 'Authored catalogue price note',
  sizePreset: ['S', 'M'],
  contactLines: ['@authored_contact'],
  garmentTypes: [{ key: 'nimcha', uz: 'Nimcha', ru: 'Жилет', en: 'Waistcoat' }],
};
const mounted: VueWrapper[] = [];

async function openApp(path = '/settings') {
  initializeUiLocale();
  const { wrapper } = await mountCatalogue(path, { stubs: { CatalogueSelect: SelectStub } });
  mounted.push(wrapper);
  return wrapper;
}

/** Opens the account menu at the end of the bar; without an Ocharo session it is the anonymous trigger. */
async function openAccountMenu(wrapper: VueWrapper) {
  const trigger = wrapper.get('header[role="banner"]').findAll('button').at(-1)!;
  await trigger.trigger('click');
  await flushPromises();
  const menu = document.querySelector<HTMLElement>('[role="menu"]');
  expect(menu).not.toBeNull();
  return new DOMWrapper(menu!);
}

/** The radio row of the open menu carrying `text`. */
function row(menu: DOMWrapper<HTMLElement>, text: string) {
  const match = menu.findAll('[role="menuitemradio"]').find((item) => item.text().trim() === text);
  expect(match, text).toBeDefined();
  return match!;
}

async function choose(wrapper: VueWrapper, text: string) {
  await row(await openAccountMenu(wrapper), text).trigger('click');
  await flushPromises();
}

beforeEach(() => {
  localStorage.clear();
  document.documentElement.classList.remove('dark');
  useUiLocale().setUiLocale('en');
  const store = useCatalogueStore();
  store.settings.value = null;
  store.settingsRevision.value = 0;
  store.sourceRoot.value = null;
  store.error.value = null;
  store.notice.value = null;
  vi.spyOn(catalogueApi, 'getSettings').mockResolvedValue(
    ResultExtensions.ok({ settings: structuredClone(authoredDefaults), revision: 7, sourceRoot: null }),
  );
  vi.spyOn(catalogueApi, 'saveSettings');
  vi.spyOn(catalogueApi, 'importWorkspace');
});
afterEach(() => {
  mounted.splice(0).forEach((wrapper) => wrapper.unmount());
  document.body.innerHTML = '';
  vi.unstubAllGlobals();
});

describe('Catalogue interface preferences', () => {
  it('keeps Settings to catalogue data; interface preferences live in the account menu', async () => {
    const wrapper = await openApp();
    expect(wrapper.find('.interface-preferences').exists()).toBe(false);
    expect(wrapper.find('#interface-language').exists()).toBe(false);
    expect(wrapper.findAll('h1')).toHaveLength(1);
    expect(wrapper.get('#catalogue-defaults-heading').text()).toBe('Catalogue defaults');
    expect(wrapper.get('#import-heading').text()).toBe('Import product records');
    expect(wrapper.get('#export-heading').text()).toBe('Download catalogue data');

    const menu = await openAccountMenu(wrapper);
    expect(menu.attributes('aria-label')).toBe('Account');
    expect(menu.findAll('[role="menuitemradio"]').map((item) => item.text().trim())).toEqual([
      'System',
      'Light',
      'Dark',
      'O‘zbekcha',
      'Русский',
      'English',
    ]);
    expect(row(menu, 'English').attributes('aria-checked')).toBe('true');
    expect(row(menu, 'System').attributes('aria-checked')).toBe('true');
  });

  it('updates English, Uzbek and Russian immediately without translating or saving authored defaults', async () => {
    const wrapper = await openApp();
    await wrapper.get('#brand').setValue('Unsaved authored name');
    await choose(wrapper, 'O‘zbekcha');
    expect(wrapper.get('h1').text()).toBe('Sozlamalar');
    expect(wrapper.get('#import-heading').text()).toBe('Mahsulot yozuvlarini import qilish');
    expect(wrapper.get('nav[aria-label="Siz shu yerdasiz"]').text()).toContain('Sozlamalar');
    expect(wrapper.get('#brand').element).toHaveProperty('value', 'Unsaved authored name');
    await choose(wrapper, 'Русский');
    expect(wrapper.get('h1').text()).toBe('Настройки');
    expect(document.title).toBe('Настройки · Ocharo Catalogue');
    expect(wrapper.get('#import-heading').text()).toBe('Импорт записей товаров');
    expect(wrapper.get('#export-heading').text()).toBe('Скачать данные каталога');
    expect(wrapper.get('#brand').element).toHaveProperty('value', 'Unsaved authored name');
    expect(wrapper.get('#note').element).toHaveProperty('value', authoredDefaults.priceNote);
    await choose(wrapper, 'Тёмная');
    expect(document.documentElement.classList.contains('dark')).toBe(true);
    expect(localStorage.getItem('ocharo.operator.locale')).toBe('ru');
    expect(localStorage.getItem(ThemeStorageKey)).toBe('dark');
    expect(useCatalogueStore().settings.value).toEqual(authoredDefaults);
    expect(catalogueApi.saveSettings).not.toHaveBeenCalled();
    expect(catalogueApi.importWorkspace).not.toHaveBeenCalled();
  });

  it('restores browser preferences on a new mount while catalogue defaults remain unchanged', async () => {
    const first = await openApp();
    await choose(first, 'Русский');
    await choose(first, 'Тёмная');
    first.unmount();
    mounted.splice(mounted.indexOf(first), 1);
    // Reset in-memory state to prove startup reads the persisted browser choice.
    useUiLocale().uiLocale.value = 'en';
    document.documentElement.classList.remove('dark');
    const second = await openApp();
    expect(second.get('h1').text()).toBe('Настройки');
    expect(document.documentElement.lang).toBe('ru');
    expect(document.documentElement.classList.contains('dark')).toBe(true);
    const menu = await openAccountMenu(second);
    expect(row(menu, 'Русский').attributes('aria-checked')).toBe('true');
    expect(row(menu, 'Тёмная').attributes('aria-checked')).toBe('true');
    expect(useCatalogueStore().settings.value).toEqual(authoredDefaults);
    expect(catalogueApi.saveSettings).not.toHaveBeenCalled();
    expect(catalogueApi.importWorkspace).not.toHaveBeenCalled();
  });

  it.each([
    ['dark', 'Dark', true],
    ['light', 'Light', false],
    ['system', 'System', false],
  ])('keeps the appearance earlier builds stored as %s', async (stored, shown, dark) => {
    localStorage.setItem(ThemeStorageKey, stored);
    const wrapper = await openApp();
    expect(document.documentElement.classList.contains('dark')).toBe(dark);
    expect(localStorage.getItem(ThemeStorageKey)).toBe(stored);
    expect(row(await openAccountMenu(wrapper), shown).attributes('aria-checked')).toBe('true');
    await row(new DOMWrapper(document.querySelector<HTMLElement>('[role="menu"]')!), 'System').trigger('click');
    await flushPromises();
    expect(localStorage.getItem(ThemeStorageKey)).toBe('system');
  });

  it('keeps public reader settings limited to browser preferences without the frame or operator data', async () => {
    const wrapper = await openApp('/c/published-example?locale=fr');
    const header = wrapper.get('header[role="banner"]');
    expect(wrapper.findAll('main')).toHaveLength(1);
    expect(header.findAll('nav')).toHaveLength(0);
    expect(header.find('button[aria-haspopup="dialog"]').exists()).toBe(false);
    expect(header.find('button[aria-haspopup="menu"]').exists()).toBe(false);
    expect(wrapper.find('aside').exists()).toBe(false);
    expect(wrapper.find('[aria-label="Catalogue sections"]').exists()).toBe(false);
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
    expect(document.documentElement.classList.contains('dark')).toBe(true);
    expect(catalogueApi.getSettings).not.toHaveBeenCalled();
    expect(catalogueApi.saveSettings).not.toHaveBeenCalled();
    expect(catalogueApi.importWorkspace).not.toHaveBeenCalled();
  });
});
