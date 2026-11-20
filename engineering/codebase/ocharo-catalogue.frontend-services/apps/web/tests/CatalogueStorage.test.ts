import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { initializeUiLocale, useUiLocale } from '../src/application/shared/UseUiLocale';
import { sessionStorageStorageBroker } from '@wow-two-beta/fe/foundation/storage';

/** Where the operator's interface language was remembered before the interface became English only; no longer read. */
const LocaleKey = 'ocharo.operator.locale';

beforeEach(() => {
  localStorage.clear();
  sessionStorage.clear();
});
afterEach(() => {
  useUiLocale().setUiLocale('en');
  vi.unstubAllGlobals();
});

describe('Catalogue local state through the SDK storage seam', () => {
  it('stays in English: a remembered or browser language is ignored and the stored key is left as it is', () => {
    vi.stubGlobal('navigator', { language: 'uz-UZ' });
    for (const stored of ['"ru"', '"uz"', '"fr"', '{']) {
      localStorage.setItem(LocaleKey, stored);
      useUiLocale().uiLocale.value = 'ru';

      initializeUiLocale();

      expect(useUiLocale().uiLocale.value).toBe('en');
      expect(document.documentElement.lang).toBe('en');
      expect(localStorage.getItem(LocaleKey)).toBe(stored);
    }
  });

  it('ignores a change to any language but English and stores nothing', () => {
    for (const value of ['uz', 'ru', 'fr', '']) useUiLocale().setUiLocale(value);

    expect(useUiLocale().uiLocale.value).toBe('en');
    expect(document.documentElement.lang).toBe('en');
    expect(localStorage.getItem(LocaleKey)).toBeNull();
  });

  it('keeps a designer recovery copy in this tab only, and degrades a broken one to a miss', () => {
    expect(sessionStorageStorageBroker.write('recovery', { title: 'Draft', snapshot: { pages: [] } })).toBe(true);
    expect(sessionStorageStorageBroker.read('recovery')).toEqual({ title: 'Draft', snapshot: { pages: [] } });
    expect(localStorage.length).toBe(0);
    sessionStorage.setItem('broken', '{');
    expect(sessionStorageStorageBroker.read('broken')).toBeNull();
    expect(sessionStorageStorageBroker.remove('recovery')).toBe(true);
    expect(sessionStorageStorageBroker.read('recovery')).toBeNull();
  });

  it('answers a miss and a refused write when the browser withholds the tab’s storage', () => {
    vi.spyOn(window, 'sessionStorage', 'get').mockImplementation(() => {
      throw new Error('Storage denied');
    });
    expect(sessionStorageStorageBroker.read('recovery')).toBeNull();
    expect(sessionStorageStorageBroker.write('recovery', { title: 'Draft' })).toBe(false);
    expect(sessionStorageStorageBroker.remove('recovery')).toBe(false);
  });
});
