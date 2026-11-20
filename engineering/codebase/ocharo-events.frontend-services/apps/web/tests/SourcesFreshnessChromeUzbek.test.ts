import { ref, shallowRef } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { IntakeFreshnessModel } from '@wow-two-beta/fe-events/contracts';

import { SourcesFreshnessKey } from '@/application/events/UseSourcesFreshness';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import SourcesFreshness from '@/presentation/events/SourcesFreshness.vue';

/**
 * Chrome's ICU has no Uzbek relative-time patterns: `Intl.RelativeTimeFormat('uz')` answers "-4 h". The stub below answers
 * like Chrome for `uz`, and the line must still read in words through the SDK's fallback. It lives in its own file because
 * the SDK memoises its probe of the runtime per locale, so the stub has to stand before the first Uzbek call.
 */
const RealRelativeTimeFormat = Intl.RelativeTimeFormat;
class ChromeLikeRelativeTimeFormat {
  private readonly inner: Intl.RelativeTimeFormat;
  private readonly bare: boolean;
  constructor(locale?: string | string[], options?: Intl.RelativeTimeFormatOptions) {
    const tag = Array.isArray(locale) ? locale[0] : locale;
    this.bare = tag?.startsWith('uz') ?? false;
    this.inner = new RealRelativeTimeFormat(this.bare ? 'en' : locale, options);
  }
  format(value: number, unit: Intl.RelativeTimeFormatUnit): string {
    if (!this.bare) return this.inner.format(value, unit);
    const short = unit.replace(/s$/, '').charAt(0);
    return `${value < 0 ? '-' : '+'}${Math.abs(value)} ${short}`;
  }
  formatToParts(value: number, unit: Intl.RelativeTimeFormatUnit) {
    return this.inner.formatToParts(value, unit);
  }
  resolvedOptions() {
    return this.inner.resolvedOptions();
  }
}

let wrapper: VueWrapper | undefined;
const freshness = shallowRef<IntakeFreshnessModel | null>(null);
const now = ref(Date.now());
const hoursAgo = (hours: number) => new Date(Date.now() - hours * 3_600_000).toISOString();

beforeEach(() => {
  (Intl as { RelativeTimeFormat: unknown }).RelativeTimeFormat = ChromeLikeRelativeTimeFormat;
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(2026, 9, 8, 10, 0));
  freshness.value = { lastReceivedAtUtc: hoursAgo(4), failingSources: 0 };
  now.value = Date.now();
});
afterEach(() => {
  wrapper?.unmount();
  wrapper = undefined;
  vi.useRealTimers();
  (Intl as { RelativeTimeFormat: unknown }).RelativeTimeFormat = RealRelativeTimeFormat;
});

describe('sources freshness on a runtime without Uzbek relative-time patterns', () => {
  it('reads the age in Uzbek words, never as a signed bare number', async () => {
    useUiLocale().setUiLocale('uz');
    wrapper = mount(SourcesFreshness, {
      global: { provide: { [SourcesFreshnessKey as symbol]: { freshness, now } } },
      attachTo: document.body,
    });
    await flushPromises();

    const text = wrapper.get('[data-test="sources-freshness"]').text();
    expect(text).toContain('4 soat oldin');
    expect(text).not.toContain('-4 h');
  });

  it('leaves English unchanged', async () => {
    useUiLocale().setUiLocale('en');
    wrapper = mount(SourcesFreshness, {
      global: { provide: { [SourcesFreshnessKey as symbol]: { freshness, now } } },
      attachTo: document.body,
    });
    await flushPromises();

    expect(wrapper.get('[data-test="sources-freshness"]').text()).toBe('Sources updated 4 hours ago');
  });
});
