import type { ProductLocale, ProductLocales } from './CatalogueModels';
import { emptyLocale } from './CatalogueRules';

/** Authored catalogue languages; these never change the interface preference. */
export const catalogueLanguages = [
  { code: 'uz', label: 'O‘zbekcha', dir: 'ltr' },
  { code: 'ru', label: 'Русский', dir: 'ltr' },
  { code: 'en', label: 'English', dir: 'ltr' },
  { code: 'tg', label: 'Тоҷикӣ', dir: 'ltr' },
  { code: 'kk', label: 'Қазақша', dir: 'ltr' },
  { code: 'ky', label: 'Кыргызча', dir: 'ltr' },
  { code: 'tk', label: 'Türkmençe', dir: 'ltr' },
  { code: 'tr', label: 'Türkçe', dir: 'ltr' },
  { code: 'az', label: 'Azərbaycanca', dir: 'ltr' },
  { code: 'fr', label: 'Français', dir: 'ltr' },
  { code: 'it', label: 'Italiano', dir: 'ltr' },
  { code: 'de', label: 'Deutsch', dir: 'ltr' },
  { code: 'es', label: 'Español', dir: 'ltr' },
  { code: 'pt', label: 'Português', dir: 'ltr' },
  { code: 'pl', label: 'Polski', dir: 'ltr' },
  { code: 'uk', label: 'Українська', dir: 'ltr' },
  { code: 'ar', label: 'العربية', dir: 'rtl' },
  { code: 'fa', label: 'فارسی', dir: 'rtl' },
  { code: 'ur', label: 'اردو', dir: 'rtl' },
  { code: 'zh-Hans', label: '简体中文', dir: 'ltr' },
  { code: 'ko', label: '한국어', dir: 'ltr' },
  { code: 'ja', label: '日本語', dir: 'ltr' },
  { code: 'id', label: 'Bahasa Indonesia', dir: 'ltr' },
  { code: 'ms', label: 'Bahasa Melayu', dir: 'ltr' },
  { code: 'hi', label: 'हिन्दी', dir: 'ltr' },
] as const;
export type CatalogueLanguage = (typeof catalogueLanguages)[number]['code'];

/** Returns exact-language copy without silently substituting a different language. */
export function productLocale(locales: ProductLocales, code: string): ProductLocale | undefined {
  if (code === 'uz' || code === 'ru' || code === 'en') return locales[code];
  return locales.additional?.[code];
}

/** Starts an explicit translation draft only when its language is opened for editing. */
export function ensureProductLocale(locales: ProductLocales, code: CatalogueLanguage): ProductLocale {
  if (code === 'uz' || code === 'ru' || code === 'en') return locales[code];
  locales.additional ??= {};
  return (locales.additional[code] ??= emptyLocale());
}

/** Counts the core languages and explicitly authored extra translations. */
export function translationReview(locales: ProductLocales) {
  return catalogueLanguages
    .filter(
      ({ code }) =>
        ['uz', 'ru', 'en'].includes(code) ||
        Object.values(productLocale(locales, code) ?? {}).some(
          (value) => typeof value === 'string' && value !== 'draft' && value.trim().length > 0,
        ),
    )
    .map((language) => ({
      ...language,
      approved:
        productLocale(locales, language.code)?.status === 'approved' &&
        !!productLocale(locales, language.code)?.name?.trim(),
    }));
}
