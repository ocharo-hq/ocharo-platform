import { ref } from 'vue';
import { localStorageStorageBroker } from '@wow-two-beta/fe/foundation/storage';

import { translateUi, type UiLocale } from '../../domain/shared/UiMessages';

const uiLocale = ref<UiLocale>('uz');
/** Where this browser remembers the operator's interface language, as JSON through the SDK storage seam. */
const StorageKey = 'ocharo.operator.locale';

/** Whether `value` names an interface language. */
function isUiLocale(value: unknown): value is UiLocale {
  return value === 'uz' || value === 'ru' || value === 'en';
}

/**
 * Restores only the interface preference; document content language is owned by each design. A choice this browser
 * remembers wins, then the browser's language; storage a browser withholds reads as no choice.
 */
export function initializeUiLocale(): void {
  const saved = localStorageStorageBroker.read<unknown>(StorageKey);
  const preferred = isUiLocale(saved) ? saved : navigator.language.split('-')[0];
  if (isUiLocale(preferred)) uiLocale.value = preferred;
  document.documentElement.lang = uiLocale.value;
}

/** Shares the operator's explicit language choice without touching authored product or catalogue copy. */
export function useUiLocale() {
  /** Switches the interface language and remembers it; a write the browser refuses still switches this tab. */
  function setUiLocale(value: string): void {
    if (!isUiLocale(value)) return;
    uiLocale.value = value;
    if (typeof document !== 'undefined') document.documentElement.lang = value;
    localStorageStorageBroker.write(StorageKey, value);
  }
  return {
    uiLocale,
    setUiLocale,
    t: (source: string, variables?: Record<string, string | number>) => translateUi(source, uiLocale.value, variables),
  };
}
