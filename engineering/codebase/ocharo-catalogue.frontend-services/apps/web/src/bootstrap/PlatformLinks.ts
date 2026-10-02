/** Browser destinations for the Ocharo family; override with VITE_OCHARO_* variables. */
export interface PlatformLink {
  key: 'workspace' | 'brand' | 'catalogue' | 'marketing' | 'editorial' | 'studio' | 'atlas' | 'wear' | 'blog';
  label: string;
  url: string;
}

const env = import.meta.env as Record<string, string | undefined>;

const configuredApps: PlatformLink[] = [
  { key: 'workspace', label: 'Workspace', url: env.VITE_OCHARO_WORKSPACE_URL ?? 'https://localhost:8266/' },
  { key: 'brand', label: 'Brand', url: env.VITE_OCHARO_BRAND_URL ?? 'https://localhost:8288/' },
  { key: 'catalogue', label: 'Catalogue', url: env.VITE_OCHARO_CATALOGUE_URL ?? 'https://localhost:8282/catalogues' },
  { key: 'marketing', label: 'Marketing', url: env.VITE_OCHARO_MARKETING_URL ?? 'https://localhost:8358/' },
  { key: 'editorial', label: 'Blog studio', url: env.VITE_OCHARO_EDITORIAL_URL ?? 'https://localhost:8284/studio/' },
  { key: 'studio', label: 'Studio', url: env.VITE_OCHARO_STUDIO_URL ?? 'https://localhost:8262/studio' },
  { key: 'atlas', label: 'Atlas', url: env.VITE_OCHARO_ATLAS_URL ?? 'https://localhost:8284/atlas/' },
  { key: 'wear', label: 'Wear', url: env.VITE_OCHARO_WEAR_URL ?? 'https://localhost:8284/wear/' },
  { key: 'blog', label: 'Blog', url: env.VITE_OCHARO_BLOG_URL ?? 'https://localhost:8284/' },
];

/** Configured destinations are HTTPS browser links without credentials or embedded tokens. */
export function resolvePlatformUrl(value: string | undefined): string | null {
  try {
    const url = new URL(value ?? '');
    return url.protocol === 'https:' && !url.username && !url.password && !url.search && !url.hash ? url.href : null;
  } catch {
    return null;
  }
}

export const platformLinks: PlatformLink[] = configuredApps.flatMap((app) => {
  const url = resolvePlatformUrl(app.url);
  return url ? [{ ...app, url }] : [];
});

/** A configured canonical authoring destination; only trusted HTTPS links are exposed. */
export function resolveCatalogueDeskUrl(value: string | undefined): string | null {
  return resolvePlatformUrl(value ?? 'https://localhost:8266/catalogue');
}

export const catalogueDeskUrl = resolveCatalogueDeskUrl(env.VITE_OCHARO_DESK_URL);
