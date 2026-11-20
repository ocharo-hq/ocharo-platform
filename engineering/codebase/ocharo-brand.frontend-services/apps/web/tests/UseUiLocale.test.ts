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
  it('leaves the interface English when a stored uz choice is there, and does not overwrite the stored key', () => {
    localStorage.setItem(LegacyKey, 'uz');
    vi.spyOn(navigator, 'language', 'get').mockReturnValue('uz-UZ');

    initializeUiLocale();
    const { uiLocale, t } = useUiLocale();

    expect(uiLocale.value).toBe('en');
    expect(document.documentElement.lang).toBe('en');
    expect(t('Sign out')).toBe('Sign out');
    // The stored choice is left as it is, only not read.
    expect(localStorage.getItem(LegacyKey)).toBe('uz');
  });

  it('ignores a change to any language but English, and stores nothing', () => {
    localStorage.setItem(LegacyKey, 'uz');
    const { uiLocale, setUiLocale } = useUiLocale();

    for (const value of ['uz', 'ru', 'de', '']) setUiLocale(value);

    expect(uiLocale.value).toBe('en');
    expect(document.documentElement.lang).toBe('en');
    expect(localStorage.getItem(LegacyKey)).toBe('uz');
  });
});
