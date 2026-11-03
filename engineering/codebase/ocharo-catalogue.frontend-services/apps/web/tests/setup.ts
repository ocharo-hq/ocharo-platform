import { beforeEach, vi } from 'vitest';

// The app's session client starts signed in as an owner, so pages render their editing controls the way a signed-in
// owner sees them; it reads the global fetch per call, so a test that serves `/api/identity/session` and initializes it
// again sees that session instead. Navigation is recorded, never performed.
vi.mock('../src/integration/common/IdentitySessionClient', async (importOriginal) => {
  const original = await importOriginal<typeof import('../src/integration/common/IdentitySessionClient')>();
  const { OwnerSession, navigations } = await import('./helpers/IdentitySessions');
  return {
    ...original,
    identityClient: original.createIdentityClient({ initial: OwnerSession, navigate: (url) => navigations.push(url) }),
  };
});

beforeEach(async () => {
  const { navigations } = await import('./helpers/IdentitySessions');
  navigations.length = 0;
});
