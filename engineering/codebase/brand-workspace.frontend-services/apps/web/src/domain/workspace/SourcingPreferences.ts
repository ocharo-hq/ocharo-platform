/** Personal presentation only; supplier facts remain organization-owned. */
export const sourcingSections = ['overview', 'media', 'contacts', 'people'] as const;
export type SourcingSection = typeof sourcingSections[number];
export interface SourcingPreferences {
  start: 'suppliers' | 'materials';
  order: 'materials-first' | 'contacts-first';
  visible: SourcingSection[];
}
export interface SourcingPreferenceScope { userId: string; organizationId: string }
export const sourcingSectionLabels: Record<SourcingSection, string> = {
  overview: 'Supplier details', media: 'Shared materials and files', contacts: 'Contacts', people: 'People',
};
export function defaultSourcingPreferences(): SourcingPreferences {
  return { start: 'suppliers', order: 'materials-first', visible: [...sourcingSections] };
}
export function isSourcingPreferences(value: unknown): value is SourcingPreferences {
  if (!value || typeof value !== 'object') return false;
  const v = value as Partial<SourcingPreferences>;
  return (v.start === 'suppliers' || v.start === 'materials') &&
    (v.order === 'materials-first' || v.order === 'contacts-first') && Array.isArray(v.visible) &&
    v.visible.every(section => sourcingSections.includes(section)) && new Set(v.visible).size === v.visible.length;
}
export function orderedSourcingSections(value: SourcingPreferences): SourcingSection[] {
  const order: SourcingSection[] = value.order === 'materials-first'
    ? ['media', 'contacts', 'people', 'overview'] : ['contacts', 'people', 'overview', 'media'];
  return order.filter(section => value.visible.includes(section));
}
