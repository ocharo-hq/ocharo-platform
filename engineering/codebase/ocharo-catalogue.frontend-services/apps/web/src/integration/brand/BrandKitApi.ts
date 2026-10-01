import type { BrandKit } from '../../domain/catalogue/BrandKit';

const env = import.meta.env as Record<string, string | undefined>;

/** The Brand service root; public brand endpoints allow cross-origin reads. */
export const brandBaseUrl = env.VITE_OCHARO_BRAND_URL ?? 'http://localhost:8287/';

/** Reads the published brand kit; null when the Brand service is not running (print falls back to text). */
export async function fetchBrandKit(fetchImplementation: typeof fetch = globalThis.fetch): Promise<BrandKit | null> {
  try {
    const response = await fetchImplementation(new URL('api/public/brand?app=catalogue', brandBaseUrl), {
      headers: { Accept: 'application/json' },
    });
    if (!response.ok) return null;
    const kit = (await response.json()) as BrandKit;
    const logos = Object.fromEntries(
      Object.entries(kit.logos ?? {}).map(([slot, logo]) => [
        slot,
        {
          ...logo,
          url: new URL(logo.url, brandBaseUrl).toString(),
          fileUrl: new URL(logo.fileUrl, brandBaseUrl).toString(),
        },
      ]),
    );
    return { ...kit, logos };
  } catch {
    return null;
  }
}
