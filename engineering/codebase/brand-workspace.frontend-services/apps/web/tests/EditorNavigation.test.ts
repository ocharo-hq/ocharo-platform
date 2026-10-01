import { describe, expect, it } from 'vitest';
import { designEditorUrl, readDesignTarget, safeWorkspaceDestination } from '@/domain/workspace/EditorNavigation';
import { readWorkspaceLocation } from '@/domain/workspace/Models';

const organization = '11111111-1111-4111-8111-111111111111';
const design = '22222222-2222-4222-8222-222222222222';
const editor = `/organizations/${organization}/catalogue`;

describe('authenticated catalogue destinations', () => {
  it('retains the organization, saved design and encoded page through a sign-in return', () => {
    const url = designEditorUrl(organization, 'catalogue', design, 'a page & detail');
    expect(safeWorkspaceDestination(url + '&return=https://outside.example&token=secret')).toBe(url);
    expect(readDesignTarget(url.slice(url.indexOf('?')))).toEqual({ documentId: design, pageId: 'a page & detail', error: null });
  });
  it('recognizes the catalogue entry without assigning an organization', () => {
    expect(readWorkspaceLocation('/catalogue')).toEqual({ organizationId: null, section: 'catalogue', draftId: null });
    expect(safeWorkspaceDestination('/catalogue')).toBe('/catalogue');
  });
  it.each(['https://outside.example/path', '//outside.example', '/\\outside.example', '/%2f%2foutside.example', '/organizations/not-an-id/catalogue', '/unknown', '/catalogue\n'])('rejects unsafe or unrecognized destinations: %s', value => {
    expect(safeWorkspaceDestination(value)).toBe('/');
  });
  it('rejects duplicate identifiers and orphaned or unbounded page values', () => {
    for (const query of [`?design=${design}&design=${design}`, '?design=bad', '?page=first', `?design=${design}&page=${'a'.repeat(121)}`, `?design=${design}&page=%00`]) {
      expect(readDesignTarget(query).error).toBeTruthy();
      expect(safeWorkspaceDestination(editor + query)).toBe(editor);
    }
  });
  it('retains recognized non-editor routes without arbitrary query or fragment data', () => {
    expect(safeWorkspaceDestination(`/organizations/${organization}/products?token=hidden#private`)).toBe(`/organizations/${organization}/products`);
    expect(safeWorkspaceDestination(`/organizations/${organization}/drafts/new`)).toBe(`/organizations/${organization}/drafts/new`);
    expect(designEditorUrl(organization, 'business-card')).toBe(`/organizations/${organization}/business-cards`);
    expect(() => designEditorUrl('outside.example', 'catalogue', design)).toThrow();
  });
});
