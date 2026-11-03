import type { IdentitySession } from '../../src/integration/common/IdentitySessionClient';

/** What `GET /api/identity/session` answers for an owner of the catalogue's organization, who edits and approves. */
export const OwnerSessionData = {
  isAuthenticated: true,
  person: { sub: 'person-owner', name: null, picture: null, email: null, phoneNumber: null },
  organization: { id: 'b8c672a6-c2e4-4516-82ad-5b178e71ace1', name: 'Ocharo', role: 'owner', slug: 'ocharo' },
  permissions: ['catalogue:edit', 'catalogue:approve'],
  csrfToken: 'csrf-token',
  expiresAt: '2026-11-04T08:00:00Z',
} as const;

/** The owner's session as the app reads it: every test starts signed in with it (`tests/setup.ts`). */
export const OwnerSession: IdentitySession = {
  reachable: true,
  isAuthenticated: true,
  person: { ...OwnerSessionData.person },
  organization: { ...OwnerSessionData.organization },
  permissions: [...OwnerSessionData.permissions],
  csrfToken: OwnerSessionData.csrfToken,
  expiresAt: OwnerSessionData.expiresAt,
};

/** The addresses the app's session client sent the browser to, oldest first; `tests/setup.ts` clears it per test. */
export const navigations: string[] = [];
