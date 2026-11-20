import { ref } from 'vue';

import { translateUi, type UiLocale } from '../../domain/shared/UiMessages';

/**
 * English-only until the SDKs' translation layer is improved (developer, 2026-11-20): the interface starts and stays
 * `en`, ignoring a stored (`ocharo.operator.locale`, left as it is) or browser preference. The uz and ru message tables
 * stay in the code for when the choice returns.
 */
const uiLocale = ref<UiLocale>('en');

/** Names the document language: always English. */
export function initializeUiLocale(): void {
  uiLocale.value = 'en';
  document.documentElement.lang = uiLocale.value;
}

/** Shares the interface language without touching authored brand text, which keeps its own uz/ru/en fields; any language but English is ignored. */
export function useUiLocale() {
  function setUiLocale(value: string): void {
    if (value !== 'en') return;
    uiLocale.value = value;
    if (typeof document !== 'undefined') document.documentElement.lang = value;
  }
  return {
    uiLocale,
    setUiLocale,
    t: (source: string, variables?: Record<string, string | number>) => translateUi(source, uiLocale.value, variables),
  };
}
