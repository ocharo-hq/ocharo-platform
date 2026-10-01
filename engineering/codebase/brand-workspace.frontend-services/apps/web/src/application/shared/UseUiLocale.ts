import { ref } from 'vue';

import { translateUi, type UiLocale } from '@/domain/shared/UiMessages';

const uiLocale = ref<UiLocale>('en');
const storageKey = 'ocharo.operator.locale';

/** Restores only the interface preference; document content language is owned by each design. */
export function initializeUiLocale(): void {
  let saved: string | null = null;
  try { saved = localStorage.getItem(storageKey); } catch { /* Storage is optional in restricted browser modes. */ }
  const preferred = saved ?? navigator.language.split('-')[0];
  if (preferred === 'uz' || preferred === 'ru' || preferred === 'en') uiLocale.value = preferred;
  document.documentElement.lang = uiLocale.value;
}

/** Shares the operator's explicit language choice without touching authored product or catalogue copy. */
export function useUiLocale() {
  function setUiLocale(value: string): void {
    if (value !== 'uz' && value !== 'ru' && value !== 'en') return;
    uiLocale.value = value;
    if (typeof document !== 'undefined') document.documentElement.lang = value;
    try { localStorage.setItem(storageKey, value); } catch { /* The current-tab choice still works. */ }
  }
  return { uiLocale, setUiLocale, t: (source: string, variables?: Record<string, string | number>) => translateUi(source, uiLocale.value, variables) };
}
