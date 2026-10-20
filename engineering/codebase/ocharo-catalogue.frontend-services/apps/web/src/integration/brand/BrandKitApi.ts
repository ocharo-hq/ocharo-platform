import type { BrandKit } from '../../domain/catalogue/BrandKit';

/**
 * Reads the published brand kit from the Brand API root (`OCHARO_BRAND_API_URL`, not the Brand app's address); public
 * brand endpoints allow cross-origin reads. Null when no root is configured or the Brand service is not running, so
 * print falls back to text.
 */
export async function fetchBrandKit(
  brandApiUrl: string | null,
  fetchImplementation: typeof fetch = globalThis.fetch,
): Promise<BrandKit | null> {
  if (!brandApiUrl) return null;
  try {
    const response = await fetchImplementation(new URL('api/public/brand?app=catalogue', brandApiUrl), {
      headers: { Accept: 'application/json' },
    });
    if (!response.ok) return null;
    const kit = (await response.json()) as BrandKit;
    const logos = Object.fromEntries(
      Object.entries(kit.logos ?? {}).map(([slot, logo]) => [
        slot,
        {
          ...logo,
          url: new URL(logo.url, brandApiUrl).toString(),
          fileUrl: new URL(logo.fileUrl, brandApiUrl).toString(),
        },
      ]),
    );
    return { ...kit, logos };
  } catch {
    return null;
  }
}
