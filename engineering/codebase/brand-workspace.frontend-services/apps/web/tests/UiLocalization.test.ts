import { chooseDeskValue, deskSelect } from './helpers/DeskSelect';
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { mount, type VueWrapper } from '@vue/test-utils';
import { defineComponent, h, nextTick, reactive } from 'vue';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { PasswordInput } from '@wow-two-beta/fe/presentation/forms';

import { initializeUiLocale, provideUiLocale, useUiLocale } from '@/application/shared/UseUiLocale';
import { sdkUiMessages } from '@/domain/shared/SdkUiMessages';
import { translateUi, uiMessages } from '@/domain/shared/UiMessages';
import { emptyCopy, productCopy, type Product } from '@/domain/workspace/Commerce';
import ProductEditor from '@/presentation/workspace/ProductEditor.vue';
import UiLanguagePicker from '@/presentation/shared/UiLanguagePicker.vue';

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
  it('changes the operator interface without replacing French product content or prices', async () => {
    const product = reactive(productFixture());
    const editor = mount(ProductEditor, { props: { product, canApprove: true, imageUrl: hash => hash } });
    const picker = mount(UiLanguagePicker);
    mounted.push(editor, picker);
    await chooseDeskValue(editor, 'Product content language', 'fr');
    const original = JSON.stringify(product);

    await chooseDeskValue(picker, translateUi('Interface language', locale.uiLocale.value), 'uz');
    expect(editor.text()).toContain(translateUi('Product translations', 'uz'));
    expect(deskSelect(editor, translateUi('Product content language', 'uz')).props('modelValue')).toBe('fr');
    expect(editor.findAll('input').map(field => (field.element as HTMLInputElement).value)).toContain('Gilet');
    expect(document.documentElement.lang).toBe('uz');
    expect(localStorage.getItem('ocharo.operator.locale')).toBe('uz');
    expect(JSON.stringify(product)).toBe(original);

    await chooseDeskValue(picker, translateUi('Interface language', locale.uiLocale.value), 'ru');
    expect(editor.text()).toContain('Переводы товара');
    expect(productCopy(product, 'fr').fabric).toBe('Soie');
    expect(product.document.pricing?.tiers[0]?.unitPrice).toBe(350000);
    expect(JSON.stringify(product)).toBe(original);
    await chooseDeskValue(picker, translateUi('Interface language', locale.uiLocale.value), 'en');
    expect(editor.text()).toContain('Product translations');
  });

  it('restores the saved interface preference while rejecting content-only languages', async () => {
    localStorage.setItem('ocharo.operator.locale', 'ru');
    initializeUiLocale();
    await nextTick();
    expect(locale.uiLocale.value).toBe('ru');
    expect(document.documentElement.lang).toBe('ru');
    locale.setUiLocale('fr');
    expect(locale.uiLocale.value).toBe('ru');
    expect(localStorage.getItem('ocharo.operator.locale')).toBe('ru');
  });

  it('resolves variables and preserves authored wording absent from the operator dictionary', () => {
    expect(translateUi('{count} drafts ready for review', 'ru', { count: 3 })).toContain('3');
    expect(translateUi('Soie de Boukhara', 'uz')).toBe('Soie de Boukhara');
    expect(translateUi('Product translations', 'en')).toBe('Product translations');
  });
});

/** A missing message silently falls back to English, so enforce both supported operator translations. */
describe('operator translation coverage', () => {
  it('has nonempty Uzbek and Russian text for every declared message', () => {
    expect(Object.keys(uiMessages).length).toBeGreaterThan(700);
    const missing = Object.entries(uiMessages).filter(([, message]) => !message.uz.trim() || !message.ru.trim());
    expect(missing).toEqual([]);
  });

  it('covers every literal translated phrase used by presentation and application code', () => {
    const sourceRoot = join(dirname(fileURLToPath(import.meta.url)), '../src');
    const missing = new Set<string>();
    function inspect(directory: string): void {
      for (const entry of readdirSync(directory, { withFileTypes: true })) {
        const path = join(directory, entry.name);
        if (entry.isDirectory()) { inspect(path); continue; }
        if (!/\.(vue|ts)$/.test(entry.name)) continue;
        const source = readFileSync(path, 'utf8');
        for (const match of source.matchAll(/\bt\(\s*(?:'((?:\\.|[^'\\])*)'|"((?:\\.|[^"\\])*)")/g)) {
          const key = match[1]?.replaceAll("\\'", "'") ?? JSON.parse(`"${match[2]}"`) as string;
          if (!(key in uiMessages)) missing.add(key);
        }
      }
    }
    inspect(join(sourceRoot, 'application'));
    inspect(join(sourceRoot, 'presentation'));
    expect([...missing]).toEqual([]);
  });
});

describe('SDK component wording', () => {
  it('names the password visibility toggle in the operator language', async () => {
    const host = mount(defineComponent({ setup() { provideUiLocale(); return () => h(PasswordInput, { modelValue: 'secret' }); } }));
    mounted.push(host);
    const toggle = () => host.get('button[aria-pressed]');
    expect(toggle().attributes('aria-label')).toBe('Show password');
    locale.setUiLocale('ru');
    await nextTick();
    expect(toggle().attributes('aria-label')).toBe('Показать пароль');
    await toggle().trigger('click');
    expect(toggle().attributes('aria-label')).toBe('Скрыть пароль');
    locale.setUiLocale('uz');
    await nextTick();
    expect(toggle().attributes('aria-label')).toBe('Parolni yashirish');
  });

  it('declares Uzbek and Russian for every SDK message key it overrides', () => {
    expect(Object.keys(sdkUiMessages)).toEqual(expect.arrayContaining(['PasswordInput.revealLabel', 'PasswordInput.concealLabel', 'ToastHost.notifications']));
    expect(Object.entries(sdkUiMessages).filter(([, message]) => !message.uz.trim() || !message.ru.trim())).toEqual([]);
  });
});
