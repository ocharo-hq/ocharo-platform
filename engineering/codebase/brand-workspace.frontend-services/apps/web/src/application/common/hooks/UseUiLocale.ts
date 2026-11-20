import { computed, ref } from 'vue';

import { provideLocale } from '@wow-two-beta/fe/foundation/i18n';

import { sdkMessages } from '@/domain/common/SdkUiMessages';
import { translateUi, type UiLocale } from '@/domain/common/UiMessages';

/**
 * English-only until the SDKs' translation layer is improved (developer, 2026-11-20): the interface starts and stays
 * `en`, ignoring a stored (`ocharo.operator.locale`, left as it is) or browser preference. The uz and ru message tables
 * stay in the code for when the choice returns.
 */
const uiLocale = ref<UiLocale>('en');

/** Names the document language: always English. Document content language is owned by each design. */
export function initializeUiLocale(): void {
  uiLocale.value = 'en';
  document.documentElement.lang = uiLocale.value;
}

/** Shares the operator language without touching authored product or catalogue copy; any language but English is ignored. */
export function useUiLocale() {
  function setUiLocale(value: string): void {
    if (value !== 'en') return;
    uiLocale.value = value;
    if (typeof document !== 'undefined') document.documentElement.lang = value;
  }
  return { uiLocale, setUiLocale, t: (source: string, variables?: Record<string, string | number>) => translateUi(source, uiLocale.value, variables) };
}

/** Provides the operator language to SDK components, with their English wording (their Uzbek and Russian stay in the tables). */
export function provideUiLocale(): void {
  provideLocale(uiLocale, computed(() => sdkMessages(uiLocale.value)));
}
