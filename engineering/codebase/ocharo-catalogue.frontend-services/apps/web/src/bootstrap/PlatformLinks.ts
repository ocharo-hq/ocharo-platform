/** The Ocharo platform family; override per environment with VITE_OCHARO_* variables. */
export interface PlatformLink {
  key: 'brand' | 'catalogue' | 'marketing' | 'blog' | 'studio';
  label: string;
  hint: string;
  url: string;
}

const env = import.meta.env as Record<string, string | undefined>;

export const platformLinks: PlatformLink[] = [
  {
    key: 'brand',
    label: 'Brend',
    hint: 'Logotip, ranglar, havolalar',
    url: env.VITE_OCHARO_BRAND_URL ?? 'https://localhost:8286/',
  },
  {
    key: 'catalogue',
    label: 'Katalog',
    hint: 'Mahsulot, narx, rasm',
    url: env.VITE_OCHARO_CATALOGUE_URL ?? 'https://localhost:8278/',
  },
  {
    key: 'marketing',
    label: 'Marketing',
    hint: 'Post va avtomatika',
    url: env.VITE_OCHARO_MARKETING_URL ?? 'https://localhost:8276/',
  },
  {
    key: 'blog',
    label: 'Blog',
    hint: 'Hikoyalar studiyasi',
    url: env.VITE_OCHARO_BLOG_URL ?? 'https://localhost:8280/studio/',
  },
  {
    key: 'studio',
    label: 'Studio',
    hint: '3D kiyim studiyasi',
    url: env.VITE_OCHARO_STUDIO_URL ?? 'https://localhost:8262/',
  },
];

/** A configured canonical authoring destination; only trusted HTTPS links are exposed. */
export function resolveCatalogueDeskUrl(value: string | undefined): string | null {
  try {
    const url = new URL(value ?? 'https://localhost:8266/catalogue');
    return url.protocol === 'https:' && !url.username && !url.password && !url.search && !url.hash ? url.href : null;
  } catch {
    return null;
  }
}

export const catalogueDeskUrl = resolveCatalogueDeskUrl(env.VITE_OCHARO_DESK_URL);
