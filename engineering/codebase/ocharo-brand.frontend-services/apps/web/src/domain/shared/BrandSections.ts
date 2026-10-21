/** Brand's places as the frame lists them: the sidebar, the icon rail, the phone bar and the breadcrumb. */
export const BrandSectionGroup = { Main: 'main', Admin: 'admin' } as const;

export type BrandSectionGroup = (typeof BrandSectionGroup)[keyof typeof BrandSectionGroup];

/** Names one section of Brand. */
export type BrandSection = 'overview' | 'identity' | 'logos' | 'appearances' | 'connect';

/** One sidebar destination. */
export interface BrandSectionEntry {
  readonly id: BrandSection;
  /** The English source label; the frame translates it. */
  readonly label: string;
  readonly path: string;
  readonly group: BrandSectionGroup;
  /** Whether the phone's bottom bar carries it; the rest open from More. */
  readonly onPhoneBar: boolean;
}

/**
 * The sections in display order: the overview with publishing, then the three parts of the brand kit an operator edits.
 * Connect — how other apps read the published kit — sits under Admin, as Catalogue's Settings does.
 */
export const BrandSections: readonly BrandSectionEntry[] = [
  { id: 'overview', label: 'Overview', path: '/', group: BrandSectionGroup.Main, onPhoneBar: true },
  { id: 'identity', label: 'Identity', path: '/identity', group: BrandSectionGroup.Main, onPhoneBar: true },
  { id: 'logos', label: 'Logos', path: '/logos', group: BrandSectionGroup.Main, onPhoneBar: true },
  { id: 'appearances', label: 'Appearances', path: '/appearances', group: BrandSectionGroup.Main, onPhoneBar: true },
  { id: 'connect', label: 'Connect', path: '/connect', group: BrandSectionGroup.Admin, onPhoneBar: false },
];

/** The heading each group carries in the sidebar; the main group has none. */
export const BrandGroupLabels: Readonly<Record<BrandSectionGroup, string | null>> = {
  main: null,
  admin: 'Admin',
};

/** Finds a section by its id. */
export function brandSection(id: BrandSection | null | undefined): BrandSectionEntry | null {
  return BrandSections.find((section) => section.id === id) ?? null;
}

/** Reads a route's section from its metadata; a place outside every section yields `null`. */
export function readBrandSection(meta: Readonly<Record<string, unknown>>): BrandSection | null {
  const value = meta.section;
  return BrandSections.some((section) => section.id === value) ? (value as BrandSection) : null;
}
