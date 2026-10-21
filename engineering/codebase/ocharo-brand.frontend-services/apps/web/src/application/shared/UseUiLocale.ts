import { ref } from 'vue';

import { translateUi, type UiLocale } from '../../domain/shared/UiMessages';

/**
 * The interface language. Brand's pages are still written in Uzbek, so the interface stays Uzbek and the account menu
 * offers no language rows; the frame's copy already resolves through English keys with Uzbek and Russian entries.
 * Localizing the pages is what lets Brand join the suite's shared operator locale.
 */
const uiLocale = ref<UiLocale>('uz');

/** Names the document language before the first mount. */
export function initializeUiLocale(): void {
  document.documentElement.lang = uiLocale.value;
}

/** Shares the interface language without touching authored brand text, which keeps its own uz/ru/en fields. */
export function useUiLocale() {
  /** Switches the interface language for this tab; nothing is stored. */
  function setUiLocale(value: string): void {
    if (value !== 'uz' && value !== 'ru' && value !== 'en') return;
    uiLocale.value = value;
    if (typeof document !== 'undefined') document.documentElement.lang = value;
  }
  return {
    uiLocale,
    setUiLocale,
    t: (source: string, variables?: Record<string, string | number>) => translateUi(source, uiLocale.value, variables),
  };
}
