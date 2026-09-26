export interface Session {
  authenticated: boolean;
  user: { id: string; email: string; displayName: string } | null;
  sessionId: string | null;
  csrfToken: string;
  setupRequired: boolean;
}
export interface Organization {
  id: string;
  name: string;
  slug: string;
  role: 'owner' | 'editor' | 'viewer';
}
export interface Brand {
  id: string;
  organizationId: string;
  name: string;
  timeZone: string;
}
export interface MediaAsset {
  id: string;
  organizationId: string;
  fileName: string;
  contentType: string;
  byteLength: number;
  sha256: string;
  provenance: string;
  createdAtUtc: string;
}
export interface DraftInput {
  brandId: string;
  title: string;
  caption: string;
  language: string;
  destination: string;
  sourceNotes: string;
  mediaIds: string[];
}
export interface DraftSummary {
  id: string;
  brandId: string;
  title: string;
  revision: number;
  status: 'draft' | 'in-review' | 'approved';
  updatedAtUtc: string;
}
export interface Draft extends DraftSummary {
  caption: string;
  language: string;
  destination: string;
  sourceNotes: string;
  media: MediaAsset[];
  requestedReviewRevision: number | null;
  approval: { id: string; revision: number; approvedAtUtc: string; approvedBy: string } | null;
}
export interface ManualExport {
  id: string;
  draftId: string;
  revision: number;
  createdAtUtc: string;
  downloadUrl: string;
  packageKind: 'manual-content-package';
  mediaIncluded: true;
  publicationStatus: 'unverified';
}
export interface WorkspaceLocation {
  organizationId: string | null;
  section: 'content' | 'brands' | 'media' | 'system';
  draftId: string | null;
}

/** Resolves only the product's known document routes. */
export function readWorkspaceLocation(pathname: string): WorkspaceLocation {
  const parts = pathname.split('/').filter(Boolean);
  if (parts[0] === 'system') return { organizationId: null, section: 'system', draftId: null };
  const organizationId = parts[0] === 'organizations' && /^[0-9a-f-]{36}$/i.test(parts[1] ?? '') ? parts[1]! : null;
  const section = parts[2] === 'media' ? 'media' : parts[2] === 'brands' ? 'brands' : 'content';
  return { organizationId, section, draftId: organizationId && parts[2] === 'drafts' ? (parts[3] ?? null) : null };
}
