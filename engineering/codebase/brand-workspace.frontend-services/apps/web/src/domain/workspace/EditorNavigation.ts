import type { DesignKind } from './EditorModels';

const identifier = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const sections = new Set(['overview', 'catalogue', 'business-cards', 'content', 'brands', 'media', 'products', 'sourcing', 'integrations']);

/** Decodes a saved-design destination without treating URL content as authority. */
export function readDesignTarget(search: string) {
  const query = new URLSearchParams(search);
  const documentId = query.get('design');
  const pageId = query.get('page');
  const mode = query.get('mode');
  const invalid = query.getAll('design').length > 1 || query.getAll('page').length > 1 || query.getAll('mode').length > 1 || (mode !== null && (!documentId || !['web','pdf'].includes(mode)))
    || (documentId !== null && !identifier.test(documentId))
    || (pageId !== null && (!documentId || !pageId || pageId.length > 120 || /[\u0000-\u001f\u007f]/.test(pageId)));
  return invalid
    ? { documentId: null, pageId: null, mode: null, error: 'This saved-design link is invalid. Choose a design from the library.' }
    : { documentId, pageId, mode: mode as 'web'|'pdf'|null, error: null };
}

/** Builds the address of an organization-owned editor and its current saved page. */
export function designEditorUrl(organizationId: string, kind: DesignKind, documentId?: string | null, pageId?: string | null, mode?: 'web'|'pdf'|null): string {
  if (!identifier.test(organizationId)) throw new Error('Invalid workspace destination.');
  const query = new URLSearchParams();
  if (documentId) query.set('design', documentId);
  if (documentId && pageId) query.set('page', pageId);
  if (documentId && mode) query.set('mode', mode);
  if (readDesignTarget(query.toString()).error) throw new Error('Invalid saved-design destination.');
  return `/organizations/${organizationId}/${kind === 'catalogue' ? 'catalogue' : 'business-cards'}${query.size ? '?' + query : ''}`;
}

/** Keeps sign-in returns within recognized workspace routes and bounded editor parameters. */
export function safeWorkspaceDestination(path: string): string {
  if (!path.startsWith('/') || path.startsWith('//') || /[\\\u0000-\u0020\u007f]/.test(path)) return '/';
  const url = new URL(path, 'https://workspace.invalid');
  if (url.origin !== 'https://workspace.invalid') return '/';
  const parts = url.pathname.split('/').filter(Boolean);
  let editor = false;
  if (parts.length === 0) return '/';
  if (parts.length === 1 && parts[0] === 'system') return '/system';
  if (parts.length === 1 && parts[0] === 'catalogue') editor = true;
  else if (parts[0] === 'organizations' && identifier.test(parts[1] ?? '')) {
    if (parts.length === 2) return `/organizations/${parts[1]}`;
    if (parts.length === 4 && parts[2] === 'drafts' && (parts[3] === 'new' || identifier.test(parts[3] ?? '')))
      return `/organizations/${parts[1]}/drafts/${parts[3]}`;
    if (parts.length === 4 && parts[2] === 'sourcing' && (parts[3] === 'inbox' || identifier.test(parts[3] ?? '')))
      return `/organizations/${parts[1]}/sourcing/${parts[3]}`;
    if (parts.length !== 3 || !sections.has(parts[2]!)) return '/';
    editor = parts[2] === 'catalogue' || parts[2] === 'business-cards';
  } else return '/';
  const pathname = '/' + parts.join('/');
  if (!editor) return pathname;
  const target = readDesignTarget(url.search);
  if (target.error) return pathname;
  const query = new URLSearchParams();
  if (target.documentId) query.set('design', target.documentId);
  if (target.pageId) query.set('page', target.pageId);
  if (target.mode) query.set('mode', target.mode);
  return pathname + (query.size ? '?' + query : '');
}
