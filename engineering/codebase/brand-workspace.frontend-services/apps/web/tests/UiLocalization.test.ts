import { chooseDeskValue, deskSelect } from './helpers/DeskSelect';
import { mount, type VueWrapper } from '@vue/test-utils';
import { defineComponent, h, nextTick, reactive } from 'vue';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { PasswordInput } from '@wow-two-beta/fe/presentation/forms';

import { initializeUiLocale, provideUiLocale, useUiLocale } from '@/application/common/hooks/UseUiLocale';
import { sdkUiMessages } from '@/domain/common/SdkUiMessages';
import { ocharoApps } from '@/domain/common/models/OcharoApps';
import { translateUi, uiMessages, type UiLocale } from '@/domain/common/UiMessages';
import { emptyCopy, productCopy, type Product } from '@/domain/common/models/Commerce';
import ProductEditor from '@/presentation/commerce/ProductEditor.vue';

const mounted: VueWrapper[] = [];
const locale = useUiLocale();
beforeEach(() => { localStorage.clear(); locale.setUiLocale('en'); });
afterEach(() => { mounted.splice(0).forEach(wrapper => wrapper.unmount()); locale.setUiLocale('en'); localStorage.clear(); });

function productFixture(): Product {
  return {
    revision: 1, sourceRevision: null,
    translations: { fr: { ...emptyCopy(), name: 'Gilet', fabric: 'Soie', description: 'Une pièce légère.' } },
    document: {
      id: 'nimcha', garmentType: 'vest', variantCode: 'v01', status: 'draft', sortOrder: 0,
      sizes: ['M'], fabricCodes: ['silk'], decorationCodes: [], tags: [],
      locales: { uz: { ...emptyCopy(), name: 'Nimcha' }, ru: emptyCopy(), en: { ...emptyCopy(), name: 'Vest' } },
      pricing: { currency: 'UZS', status: 'confirmed', tiers: [{ minimumQuantity: 1, maximumQuantity: null, unitPrice: 350000 }], wording: null },
      media: [], issues: [], notes: null, stock: null, madeToOrder: false,
    },
  };
}

describe('operator and catalogue language boundaries', () => {
  it('keeps French product content and prices in the English interface, which a language change does not alter', async () => {
    const product = reactive(productFixture());
    const editor = mount(ProductEditor, { props: { product, canApprove: true, imageUrl: hash => hash } });
    mounted.push(editor);
    await chooseDeskValue(editor, 'Product content language', 'fr');
    const original = JSON.stringify(product);

    for (const other of ['uz', 'ru']) locale.setUiLocale(other);
    await nextTick();
    expect(editor.text()).toContain('Product translations');
    expect(deskSelect(editor, 'Product content language').props('modelValue')).toBe('fr');
    expect(editor.findAll('input').map(field => (field.element as HTMLInputElement).value)).toContain('Gilet');
    expect(document.documentElement.lang).toBe('en');
    expect(productCopy(product, 'fr').fabric).toBe('Soie');
    expect(product.document.pricing?.tiers[0]?.unitPrice).toBe(350000);
    expect(JSON.stringify(product)).toBe(original);
  });

  it('stays in English: a stored or browser language is ignored and left stored, and a change to another language is a no-op', async () => {
    localStorage.setItem('ocharo.operator.locale', 'ru');
    initializeUiLocale();
    await nextTick();
    expect(locale.uiLocale.value).toBe('en');
    expect(document.documentElement.lang).toBe('en');
    for (const other of ['uz', 'ru', 'fr']) locale.setUiLocale(other);
    expect(locale.uiLocale.value).toBe('en');
    expect(localStorage.getItem('ocharo.operator.locale')).toBe('ru');
    localStorage.clear();
    locale.setUiLocale('uz');
    expect(localStorage.getItem('ocharo.operator.locale')).toBeNull();
  });

  it('resolves variables and preserves authored wording absent from the operator dictionary', () => {
    expect(translateUi('{count} drafts ready for review', 'ru', { count: 3 })).toContain('3');
    expect(translateUi('Soie de Boukhara', 'uz')).toBe('Soie de Boukhara');
    expect(translateUi('Product translations', 'en')).toBe('Product translations');
  });
});

/** The interface is English only for now; the uz and ru entries stay in the table for when the choice returns. */
describe('operator translation tables', () => {
  it('keeps nonempty Uzbek and Russian text in every message the table declares; a new phrase needs English only', () => {
    expect(Object.keys(uiMessages).length).toBeGreaterThan(700);
    const missing = Object.entries(uiMessages).filter(([, message]) => !message.uz.trim() || !message.ru.trim());
    expect(missing).toEqual([]);
    expect('A phrase nobody translated' in uiMessages).toBe(false);
    for (const value of ['en', 'uz', 'ru'] as const) expect(translateUi('A phrase nobody translated', value)).toBe('A phrase nobody translated');
  });
});

describe('Ocharo app names', () => {
  it('names the Editorial app apart from the Editorial designer theme', () => {
    const [editorial] = ocharoApps({ editorial: 'https://example.test/' });
    const locales: UiLocale[] = ['en', 'uz', 'ru'];
    expect(locales.map((value) => translateUi(editorial!.label, value))).toEqual(['Editorial', 'Tahririyat', 'Редакция']);
    expect(locales.map((value) => translateUi(editorial!.summary, value))).toEqual(['Blog stories', 'Blog hikoyalari', 'Истории блога']);
    expect(locales.map((value) => translateUi('Editorial', value))).toEqual(['Editorial', 'Jurnal uslubi', 'Журнальный стиль']);
  });
});

describe('SDK component wording', () => {
  it('names the password visibility toggle in English, whatever other language is asked for', async () => {
    const host = mount(defineComponent({ setup() { provideUiLocale(); return () => h(PasswordInput, { modelValue: 'secret' }); } }));
    mounted.push(host);
    const toggle = () => host.get('button[aria-pressed]');
    expect(toggle().attributes('aria-label')).toBe('Show password');
    locale.setUiLocale('ru');
    await nextTick();
    expect(toggle().attributes('aria-label')).toBe('Show password');
    await toggle().trigger('click');
    expect(toggle().attributes('aria-label')).toBe('Hide password');
  });

  it('keeps Uzbek and Russian text for every SDK message key it overrides', () => {
    const accountMenuKeys = ['menuLabel', 'triggerLabel', 'anonymousTriggerLabel', 'appearanceLabel', 'systemModeLabel', 'lightModeLabel',
      'darkModeLabel', 'languageLabel', 'organizationLabel', 'versionsLabel', 'signOutLabel'].map((key) => `AccountMenu.${key}`);
    expect(Object.keys(sdkUiMessages)).toEqual(expect.arrayContaining(['PasswordInput.revealLabel', 'PasswordInput.concealLabel', 'ToastHost.notifications', ...accountMenuKeys]));
    expect(sdkUiMessages['AccountMenu.triggerLabel']).toEqual({ uz: expect.stringContaining('{name}'), ru: expect.stringContaining('{name}') });
    expect(Object.entries(sdkUiMessages).filter(([, message]) => !message.uz.trim() || !message.ru.trim())).toEqual([]);
  });
});
