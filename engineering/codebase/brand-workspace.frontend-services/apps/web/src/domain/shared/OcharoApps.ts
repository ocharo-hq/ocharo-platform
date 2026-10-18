/** The Ocharo apps a signed-in operator moves between, and the public sites they publish to. */
export const OcharoAppKind = { Workspace: 'workspace', PublicSite: 'public-site' } as const;

export type OcharoAppKind = (typeof OcharoAppKind)[keyof typeof OcharoAppKind];

/** One destination in the app menu. */
export interface OcharoApp {
  readonly key: 'desk' | 'catalogue' | 'studio' | 'marketing' | 'editorial' | 'brand' | 'atlas' | 'wear' | 'blog';
  readonly label: string;
  /** One line under the label naming what the app is for. */
  readonly summary: string;
  readonly kind: OcharoAppKind;
  readonly url: string;
}

/** Accepts only plain HTTPS links: no credentials, query or fragment that could carry a token. */
export function resolveOcharoAppUrl(value: string | undefined): string | null {
  try {
    const url = new URL(value ?? '');
    return url.protocol === 'https:' && !url.username && !url.password && !url.search && !url.hash ? url.href : null;
  } catch {
    return null;
  }
}

/** Builds the menu from configured addresses, dropping any that are not plain HTTPS links. */
export function ocharoApps(env: Readonly<Record<string, string | undefined>>): OcharoApp[] {
  const configured: OcharoApp[] = [
    { key: 'desk', label: 'Desk', summary: 'Events, sourcing and products', kind: OcharoAppKind.Workspace, url: env['VITE_OCHARO_DESK_URL'] ?? 'https://localhost:8264/' },
    { key: 'catalogue', label: 'Catalogue', summary: 'Products, prices and editions', kind: OcharoAppKind.Workspace, url: env['VITE_OCHARO_CATALOGUE_URL'] ?? 'https://localhost:8282/catalogues' },
    { key: 'studio', label: 'Studio', summary: 'Garments and fabric', kind: OcharoAppKind.Workspace, url: env['VITE_OCHARO_STUDIO_URL'] ?? 'https://localhost:8262/studio' },
    { key: 'marketing', label: 'Marketing', summary: 'Posts and leads', kind: OcharoAppKind.Workspace, url: env['VITE_OCHARO_MARKETING_URL'] ?? 'https://localhost:8358/' },
    { key: 'editorial', label: 'Editorial', summary: 'Blog stories', kind: OcharoAppKind.Workspace, url: env['VITE_OCHARO_EDITORIAL_URL'] ?? 'https://localhost:8284/studio/' },
    { key: 'brand', label: 'Brand', summary: 'Logo and brand guide', kind: OcharoAppKind.Workspace, url: env['VITE_OCHARO_BRAND_URL'] ?? 'https://localhost:8288/' },
    { key: 'atlas', label: 'Atlas', summary: 'Public site', kind: OcharoAppKind.PublicSite, url: env['VITE_OCHARO_ATLAS_URL'] ?? 'https://localhost:8284/atlas/' },
    { key: 'wear', label: 'Wear', summary: 'Public site', kind: OcharoAppKind.PublicSite, url: env['VITE_OCHARO_WEAR_URL'] ?? 'https://localhost:8284/wear/' },
    { key: 'blog', label: 'Blog', summary: 'Public site', kind: OcharoAppKind.PublicSite, url: env['VITE_OCHARO_BLOG_URL'] ?? 'https://localhost:8284/' },
  ];
  return configured.flatMap((app) => {
    const url = resolveOcharoAppUrl(app.url);
    return url ? [{ ...app, url }] : [];
  });
}
