import { ref } from 'vue';

export type Appearance = 'system' | 'light' | 'dark';
const appearance = ref<Appearance>('system');
const dark = ref(false);
const storageKey = 'ocharo.catalogue.theme';

function valid(value: string | null): value is Appearance {
  return value === 'system' || value === 'light' || value === 'dark';
}

function applyAppearance(): void {
  if (typeof window === 'undefined' || typeof document === 'undefined') return;
  dark.value =
    appearance.value === 'dark' ||
    (appearance.value === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches);
  document.documentElement.classList.toggle('dark', dark.value);
  document.documentElement.style.colorScheme = dark.value ? 'dark' : 'light';
}

/** Browser-local appearance never changes catalogue content or backend defaults. */
export function useAppearance() {
  function initializeAppearance(): void {
    let saved: string | null = null;
    try {
      saved = localStorage.getItem(storageKey);
    } catch {
      /* Storage is optional. */
    }
    appearance.value = valid(saved) ? saved : 'system';
    applyAppearance();
  }
  function setAppearance(value: string): void {
    if (!valid(value)) return;
    appearance.value = value;
    applyAppearance();
    try {
      localStorage.setItem(storageKey, value);
    } catch {
      /* The current tab still updates. */
    }
  }
  return { appearance, dark, initializeAppearance, setAppearance };
}
