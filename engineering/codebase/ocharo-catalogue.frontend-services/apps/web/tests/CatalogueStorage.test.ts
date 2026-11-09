import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { initializeUiLocale, useUiLocale } from '../src/application/shared/UseUiLocale';
import { sessionStorageBroker } from '../src/integration/common/SessionStorageBroker';

/** Where the operator's interface language is remembered. */
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
  it('remembers the interface language as JSON and restores it on the next start', () => {
    useUiLocale().setUiLocale('ru');
    expect(localStorage.getItem(LocaleKey)).toBe('"ru"');
    useUiLocale().uiLocale.value = 'en';

    initializeUiLocale();

    expect(useUiLocale().uiLocale.value).toBe('ru');
    expect(document.documentElement.lang).toBe('ru');
  });

  it('reads an unreadable or unknown remembered language as no choice and follows the browser', () => {
    vi.stubGlobal('navigator', { language: 'uz-UZ' });
    for (const stored of ['ru', '"fr"', '{']) {
      localStorage.setItem(LocaleKey, stored);
      useUiLocale().uiLocale.value = 'en';

      initializeUiLocale();

      expect(useUiLocale().uiLocale.value).toBe('uz');
    }
  });

  it('keeps a designer recovery copy in this tab only, and degrades a broken one to a miss', () => {
    expect(sessionStorageBroker.write('recovery', { title: 'Draft', snapshot: { pages: [] } })).toBe(true);
    expect(sessionStorageBroker.read('recovery')).toEqual({ title: 'Draft', snapshot: { pages: [] } });
    expect(localStorage.length).toBe(0);
    sessionStorage.setItem('broken', '{');
    expect(sessionStorageBroker.read('broken')).toBeNull();
    expect(sessionStorageBroker.remove('recovery')).toBe(true);
    expect(sessionStorageBroker.read('recovery')).toBeNull();
  });

  it('answers a miss and a refused write when the browser withholds the tab’s storage', () => {
    vi.spyOn(window, 'sessionStorage', 'get').mockImplementation(() => {
      throw new Error('Storage denied');
    });
    expect(sessionStorageBroker.read('recovery')).toBeNull();
    expect(sessionStorageBroker.write('recovery', { title: 'Draft' })).toBe(false);
    expect(sessionStorageBroker.remove('recovery')).toBe(false);
  });
});
