import { cloneSnapshot, validateSnapshot, type DesignSnapshot } from './EditorModels';

/** Selects existing pages in edition order without changing their physical dimensions or printed folios. */
export function selectDesignPages(snapshot: DesignSnapshot, pageIds?: readonly string[]): DesignSnapshot {
  const checked = validateSnapshot(snapshot);
  if (pageIds === undefined) return checked;
  if (!pageIds.length) throw new Error('Select at least one page to export.');
  const ids = new Set(pageIds);
  if (ids.size !== pageIds.length || pageIds.some(id => !checked.pages.some(page => page.id === id)))
    throw new Error('The selected pages changed. Select them again before exporting.');
  const selected = cloneSnapshot(checked);
  selected.pages = selected.pages.filter(page => ids.has(page.id));
  return selected;
}
