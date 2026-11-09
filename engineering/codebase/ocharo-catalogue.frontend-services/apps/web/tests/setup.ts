import { config } from '@vue/test-utils';
import { beforeEach, vi } from 'vitest';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import { queryPlugin } from '@wow-two-beta/fe/query';
import { catalogueApi } from '../src/integration/catalogue';
import { createCatalogueQueryClient } from '../src/bootstrap/CatalogueQueryClient';
import { navigations, OwnerUser, publishSession } from './helpers/IdentitySessions';

// Every test starts signed in as the organization's owner, so pages render their editing controls the way a signed-in
// owner sees them; a test that mounts the app root has its provider read `/api/identity/session` instead. Each test gets
// its own session-bound query client, so no read is cached from another test. The browser never leaves the page:
// sign-in and sign-out addresses are recorded. Pages read the catalogue settings beside their own data; a test that
// cares about them mocks the read again.
beforeEach(() => {
  navigations.length = 0;
  publishSession(OwnerUser);
  config.global.plugins = [queryPlugin(createCatalogueQueryClient())];
  vi.spyOn(catalogueApi, 'getSettings').mockResolvedValue(
    ResultExtensions.ok({
      settings: {
        brandName: 'Ocharo',
        currency: 'UZS',
        priceNote: '',
        sizePreset: [],
        contactLines: [],
        garmentTypes: [],
      },
      revision: 1,
      sourceRoot: null,
    }),
  );
  // Node-environment suites (the development proxy, the first-paint script) have no window to leave.
  if (typeof window === 'undefined') return;
  for (const method of ['assign', 'replace'] as const)
    vi.spyOn(window.location, method).mockImplementation((url: string | URL) => {
      navigations.push(String(url));
    });
});
