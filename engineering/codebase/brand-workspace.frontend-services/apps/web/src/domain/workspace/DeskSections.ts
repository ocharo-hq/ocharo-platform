import type { WorkspaceLocation } from '@/domain/workspace/Models';

/** One Desk section, as the address bar names it. */
export type DeskSection = WorkspaceLocation['section'];

/** The sidebar's groups, in the order they appear. */
export const DeskSectionGroup = {
  Home: 'home',
  Operations: 'operations',
  Products: 'products',
  Brand: 'brand',
  Admin: 'admin',
} as const;

export type DeskSectionGroup = (typeof DeskSectionGroup)[keyof typeof DeskSectionGroup];

/** A section's place in the frame: its label, its group and whether the phone's bottom bar carries it. */
export interface DeskSectionEntry {
  readonly id: DeskSection;
  readonly label: string;
  /** The bottom bar's shorter label; the sidebar always shows the full one. */
  readonly shortLabel: string;
  readonly group: DeskSectionGroup;
  /** Shown in the phone's bottom bar; every other section opens from More. */
  readonly onPhoneBar: boolean;
}

/** Every Desk section in sidebar order. The phone's bottom bar takes the four marked ones, then More. */
export const DeskSections: readonly DeskSectionEntry[] = [
  { id: 'overview', label: 'Overview', shortLabel: 'Overview', group: DeskSectionGroup.Home, onPhoneBar: false },
  { id: 'events', label: 'Events', shortLabel: 'Events', group: DeskSectionGroup.Operations, onPhoneBar: true },
  { id: 'sourcing', label: 'Sourcing', shortLabel: 'Sourcing', group: DeskSectionGroup.Operations, onPhoneBar: true },
  { id: 'products', label: 'Products & releases', shortLabel: 'Products', group: DeskSectionGroup.Products, onPhoneBar: true },
  { id: 'catalogue', label: 'Catalogues', shortLabel: 'Catalogues', group: DeskSectionGroup.Products, onPhoneBar: false },
  { id: 'media', label: 'Media', shortLabel: 'Media', group: DeskSectionGroup.Products, onPhoneBar: true },
  { id: 'brands', label: 'Brands', shortLabel: 'Brands', group: DeskSectionGroup.Brand, onPhoneBar: false },
  { id: 'business-cards', label: 'Business cards', shortLabel: 'Cards', group: DeskSectionGroup.Brand, onPhoneBar: false },
  { id: 'content', label: 'Content', shortLabel: 'Content', group: DeskSectionGroup.Brand, onPhoneBar: false },
  { id: 'integrations', label: 'Connected apps', shortLabel: 'Apps', group: DeskSectionGroup.Admin, onPhoneBar: false },
  { id: 'system', label: 'System', shortLabel: 'System', group: DeskSectionGroup.Admin, onPhoneBar: false },
];

/** The sidebar heading over each group; the home group has none. */
export const DeskGroupLabels: Readonly<Record<DeskSectionGroup, string | null>> = {
  home: null,
  operations: 'Operations',
  products: 'Products',
  brand: 'Brand and content',
  admin: 'Admin',
};

/** The address of a section inside one organization; System sits outside every organization. */
export function deskSectionPath(organizationId: string, section: DeskSection): string {
  if (section === 'system') return '/system';
  return `/organizations/${organizationId}${section === 'overview' ? '' : `/${section}`}`;
}

/** A section's full label, for the breadcrumb and the document title. */
export function deskSectionLabel(section: DeskSection): string {
  return DeskSections.find((entry) => entry.id === section)?.label ?? 'Overview';
}
