import type { DesignSnapshot } from './EditorModels';

/** Immutable metadata for a saved design revision. */
export interface DesignRevision {
  revision: number;
  title: string;
  archived: boolean;
  savedBy: string;
  savedAtUtc: string;
}

/** A historical snapshot requested as a separate, unsaved editor document. */
export interface DesignRevisionCopy {
  snapshot: DesignSnapshot;
  title: string;
  revision: number;
}
