import { ref } from 'vue';

import { translateUi, type UiLocale } from '../../domain/shared/UiMessages';

/** The operator language every Ocharo app shares: one browser key, so a choice in one app holds in the others. */
export const UiLocaleStorageKey = 'ocharo.operator.locale';
const uiLocale = ref<UiLocale>('uz');

/** Restores the saved interface language, Uzbek until the operator picks another, and names the document language. */
export function initializeUiLocale(): void {
  let saved: string | null = null;
  try {
    saved = localStorage.getItem(UiLocaleStorageKey);
  } catch {
    /* Storage is optional in restricted browser modes. */
  }
  uiLocale.value = saved === 'uz' || saved === 'ru' || saved === 'en' ? saved : 'uz';
  document.documentElement.lang = uiLocale.value;
}

/** Shares the interface language; the account menu's choice is kept under the shared operator key. */
export function useUiLocale() {
  function setUiLocale(value: string): void {
    if (value !== 'uz' && value !== 'ru' && value !== 'en') return;
    uiLocale.value = value;
    if (typeof document !== 'undefined') document.documentElement.lang = value;
    try {
      localStorage.setItem(UiLocaleStorageKey, value);
    } catch {
      /* The current-tab choice still works. */
    }
  }
  return {
    uiLocale,
    setUiLocale,
    t: (source: string, variables?: Record<string, string | number>) => translateUi(source, uiLocale.value, variables),
  };
}
