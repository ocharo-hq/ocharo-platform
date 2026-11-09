import { AuthStatus, type BffUser } from '@wow-two-beta/fe/auth';
import { identityBridge } from '../../src/integration/common/IdentitySession';

/** What `GET /api/identity/session` answers for an owner of the catalogue's organization, who edits and approves. */
export const OwnerSessionData = {
  isAuthenticated: true,
  person: { sub: 'person-owner', name: null, picture: null, email: null, phoneNumber: null },
  organization: { id: 'b8c672a6-c2e4-4516-82ad-5b178e71ace1', name: 'Ocharo', role: 'owner', slug: 'ocharo' },
  permissions: ['catalogue:edit', 'catalogue:approve'],
  csrfToken: 'csrf-token',
  expiresAt: '2099-01-01T00:00:00Z',
} as const;

/** The session route's answer without a session. */
export const SignedOutSessionData = { isAuthenticated: false } as const;

/** The owner's session as the SDK strategy decodes it: every test starts signed in with it (`tests/setup.ts`). */
export const OwnerUser: BffUser = {
  person: { sub: OwnerSessionData.person.sub, name: null, picture: null, email: null, phoneNumber: null },
  organization: { id: OwnerSessionData.organization.id, role: OwnerSessionData.organization.role },
  permissions: [...OwnerSessionData.permissions],
  expiresAt: OwnerSessionData.expiresAt,
};

/** Publishes `user` (signed out for `null`) as the settled session, the way the app root's provider does once it resolves. */
export function publishSession(user: BffUser | null): void {
  identityBridge.publishSession(
    user ? { status: AuthStatus.Authenticated, user } : { status: AuthStatus.Anonymous, user: null },
  );
}

/** The addresses the app sent the browser to, oldest first; `tests/setup.ts` records them and clears it per test. */
export const navigations: string[] = [];
