import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { initializeUiLocale, useUiLocale } from '../src/application/shared/UseUiLocale';

/** The key the suite's apps once shared for the operator's language choice. */
const LegacyKey = 'ocharo.operator.locale';

beforeEach(() => {
  localStorage.clear();
});
afterEach(() => {
  localStorage.clear();
  vi.restoreAllMocks();
  initializeUiLocale();
});

describe('interface language (English only until the SDKs’ translation layer improves)', () => {
  it('starts in English, naming the document language, whatever the browser or a stored choice says', () => {
    localStorage.setItem(LegacyKey, 'uz');
    vi.spyOn(navigator, 'language', 'get').mockReturnValue('ru-RU');

    initializeUiLocale();

    expect(useUiLocale().uiLocale.value).toBe('en');
    expect(document.documentElement.lang).toBe('en');
    // The stored choice is left as it is, only not read.
    expect(localStorage.getItem(LegacyKey)).toBe('uz');
  });

  it('ignores a change to any language but English and stores nothing', () => {
    const { uiLocale, setUiLocale, t } = useUiLocale();

    for (const value of ['uz', 'ru', 'de', '']) setUiLocale(value);

    expect(uiLocale.value).toBe('en');
    expect(document.documentElement.lang).toBe('en');
    expect(localStorage.getItem(LegacyKey)).toBeNull();
    expect(t('Sign out')).toBe('Sign out');
  });
});
