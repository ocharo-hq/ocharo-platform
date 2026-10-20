/** Catalogue's places as the frame lists them: the sidebar, the icon rail, the phone bar and the breadcrumb. */
export const CatalogueSectionGroup = { Main: 'main', Admin: 'admin' } as const;

export type CatalogueSectionGroup = (typeof CatalogueSectionGroup)[keyof typeof CatalogueSectionGroup];

/** Names one section of Catalogue. */
export type CatalogueSection = 'catalogues' | 'collections' | 'products' | 'stock' | 'settings';

/** One sidebar destination. */
export interface CatalogueSectionEntry {
  readonly id: CatalogueSection;
  /** The English source label; the frame translates it. */
  readonly label: string;
  readonly path: string;
  readonly group: CatalogueSectionGroup;
  /** Whether the phone's bottom bar carries it; the rest open from More. */
  readonly onPhoneBar: boolean;
}

/** The sections in display order. Prices, translations and the print sheet are product-wide tools under Products. */
export const CatalogueSections: readonly CatalogueSectionEntry[] = [
  { id: 'catalogues', label: 'Catalogues', path: '/catalogues', group: CatalogueSectionGroup.Main, onPhoneBar: true },
  {
    id: 'collections',
    label: 'Collections',
    path: '/collections',
    group: CatalogueSectionGroup.Main,
    onPhoneBar: true,
  },
  { id: 'products', label: 'Products', path: '/', group: CatalogueSectionGroup.Main, onPhoneBar: true },
  { id: 'stock', label: 'Stock', path: '/stock', group: CatalogueSectionGroup.Main, onPhoneBar: true },
  { id: 'settings', label: 'Settings', path: '/settings', group: CatalogueSectionGroup.Admin, onPhoneBar: false },
];

/** The heading each group carries in the sidebar; the main group has none. */
export const CatalogueGroupLabels: Readonly<Record<CatalogueSectionGroup, string | null>> = {
  main: null,
  admin: 'Admin',
};

/** Finds a section by its id. */
export function catalogueSection(id: CatalogueSection | null | undefined): CatalogueSectionEntry | null {
  return CatalogueSections.find((section) => section.id === id) ?? null;
}

/** Reads a route's section from its metadata; a place outside every section yields `null`. */
export function readCatalogueSection(meta: Readonly<Record<string, unknown>>): CatalogueSection | null {
  const value = meta.section;
  return CatalogueSections.some((section) => section.id === value) ? (value as CatalogueSection) : null;
}
