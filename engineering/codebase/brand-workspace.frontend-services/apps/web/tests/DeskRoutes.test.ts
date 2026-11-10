import { describe, expect, it } from 'vitest';

import { deskPageProps, readDeskPlace, type DeskPageContext } from '@/bootstrap/router/DeskPlaces';
import { createDeskRouter, NotFoundLabel } from '@/bootstrap/router/DeskRoutes';
import { readWorkspaceLocation } from '@/domain/common/models/Workspace';

const ORG = '11111111-1111-4111-8111-111111111111';
const ITEM = '22222222-2222-4222-8222-222222222222';
const context = { access: {}, workspace: {} } as unknown as DeskPageContext;

/** Resolves an address the way a direct hit does, without loading any page code. */
function resolve(path: string) {
  const router = createDeskRouter({ history: 'memory' });
  return router.resolve(path);
}

describe('Desk places', () => {
  it('opens every address Desk serves in the section its workspace location names', () => {
    const addresses = [
      '/', '/catalogue', '/system', `/organizations/${ORG}`, `/organizations/${ORG}/events`, `/organizations/${ORG}/events/anything`,
      `/organizations/${ORG}/sourcing`, `/organizations/${ORG}/sourcing/inbox`, `/organizations/${ORG}/sourcing/materials`,
      `/organizations/${ORG}/sourcing/${ITEM}`, `/organizations/${ORG}/sourcing/${ITEM}/extra`, `/organizations/${ORG}/products`,
      `/organizations/${ORG}/catalogue?design=${ITEM}&mode=web`, `/organizations/${ORG}/business-cards`, `/organizations/${ORG}/media`,
      `/organizations/${ORG}/brands`, `/organizations/${ORG}/content`, `/organizations/${ORG}/drafts`,
      `/organizations/${ORG}/drafts/new`, `/organizations/${ORG}/drafts/${ITEM}`, `/organizations/${ORG}/integrations`,
    ];
    for (const address of addresses) {
      const route = resolve(address);
      expect(readDeskPlace(route)?.label, address).not.toBe(NotFoundLabel);
      expect(readDeskPlace(route)?.section, address).toBe(readWorkspaceLocation(route.path).section);
    }
  });

  it('opens the not-found place for an address outside every place', () => {
    for (const address of ['/unknown', `/organizations/${ORG}/unknown`, '/organizations/not-an-id/events', `/organizations/${ORG}/media/extra`]) {
      const place = readDeskPlace(resolve(address));
      expect(place, address).toMatchObject({ section: null, label: NotFoundLabel });
      expect(deskPageProps(resolve(address), context), address).toEqual({});
    }
  });

  it('decodes each page’s address into typed page props', () => {
    expect(deskPageProps(resolve(`/organizations/${ORG}/sourcing/${ITEM}`), context))
      .toMatchObject({ initialLocation: { view: 'supplier', supplierId: ITEM } });
    expect(deskPageProps(resolve(`/organizations/${ORG}/catalogue?design=${ITEM}&page=cover&mode=pdf`), context))
      .toMatchObject({ initialDocumentId: ITEM, initialPageId: 'cover', initialMode: 'pdf', navigationError: null });
    expect(deskPageProps(resolve(`/organizations/${ORG}/business-cards?design=nope`), context))
      .toMatchObject({ kind: 'business-card', initialDocumentId: null, navigationError: expect.any(String) });
    expect(deskPageProps(resolve('/catalogue'), context)).toMatchObject({ catalogueEntry: true });
    expect(deskPageProps(resolve('/'), context)).toMatchObject({ catalogueEntry: false });
    expect(deskPageProps(resolve('/system'), context)).toEqual({});
  });
});
