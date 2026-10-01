import type { DesignElement, DesignPage } from './EditorModels';

export interface DesignPreflightIssue {
  pageId: string; pageNumber: number; pageTitle: string;
  elementId: string; elementLabel: string;
  severity: 'warning' | 'error';
  code: 'outside-page' | 'text-overflow' | 'missing-image';
  message: string;
}
export interface DesignPreflightReport { pageCount: number; issues: DesignPreflightIssue[] }
/** Rotated frames can cross an edge even when their unrotated coordinates fit. */
export function crossesPageEdge(element: DesignElement, page: DesignPage): boolean {
  const angle = (element.rotation ?? 0) * Math.PI / 180;
  const width = Math.abs(element.width * Math.cos(angle)) + Math.abs(element.height * Math.sin(angle));
  const height = Math.abs(element.width * Math.sin(angle)) + Math.abs(element.height * Math.cos(angle));
  const x = element.x + element.width / 2, y = element.y + element.height / 2;
  const tolerance = 0.05; // Import rounding below 0.05 mm is not a meaningful crop.
  return x - width / 2 < -tolerance || y - height / 2 < -tolerance ||
    x + width / 2 > page.width + tolerance || y + height / 2 > page.height + tolerance;
}
