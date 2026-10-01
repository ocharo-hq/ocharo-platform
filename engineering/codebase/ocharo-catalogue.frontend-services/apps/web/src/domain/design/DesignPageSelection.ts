import { cloneSnapshot, validateSnapshot, type DesignSnapshot } from './EditorModels';

/** Selects existing pages in edition order without changing their physical dimensions or printed folios. */
export function selectDesignPages(snapshot: DesignSnapshot, pageIds?: readonly string[]): DesignSnapshot {
  const checked = validateSnapshot(snapshot);
  if (checked.kind === 'catalogue') {
    if (checked.catalogue && !checked.catalogue.outputs.pdf)
      throw new Error('PDF output is disabled for this catalogue.');
    checked.pages = checked.pages.filter((page) => page.isActive !== false);
    if (!checked.pages.length) throw new Error('Activate at least one PDF page before exporting.');
  }
  if (pageIds === undefined) return checked;
  if (!pageIds.length) throw new Error('Select at least one page to export.');
  const ids = new Set(pageIds);
  if (ids.size !== pageIds.length || pageIds.some((id) => !checked.pages.some((page) => page.id === id)))
    throw new Error('The selected pages changed. Select them again before exporting.');
  const selected = cloneSnapshot(checked);
  selected.pages = selected.pages.filter((page) => ids.has(page.id));
  return selected;
}
