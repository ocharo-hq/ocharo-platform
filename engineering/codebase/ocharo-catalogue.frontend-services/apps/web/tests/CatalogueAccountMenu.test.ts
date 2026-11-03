import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { DOMWrapper, flushPromises, type VueWrapper } from '@vue/test-utils';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import { catalogueApi } from '../src/integration/catalogue';
import { identityClient } from '../src/integration/common/IdentitySessionClient';
import { useUiLocale } from '../src/application/shared/UseUiLocale';
import { EndSessionUrl, mountCatalogue } from './helpers/CatalogueApp';
import { navigations, OwnerSessionData } from './helpers/IdentitySessions';

const SignedIn = { ...OwnerSessionData, permissions: ['catalogue:edit'] };
type Person = Partial<(typeof OwnerSessionData)['person']> | Record<string, string | null>;
const signedInAs = (person: Person) => ({ ...SignedIn, person: { ...SignedIn.person, ...person } });
let wrapper: VueWrapper | undefined;

/** Mounts the frame with an Ocharo session, as startup resolves it before mounting. */
async function renderSignedIn(session: Record<string, unknown> = SignedIn) {
  const mounted = await mountCatalogue('/stock', { session });
  await identityClient.initialize(true);
  await flushPromises();
  wrapper = mounted.wrapper;
}

async function openAccountMenu() {
  await wrapper!.get('header button[aria-haspopup="menu"]').trigger('click');
  await flushPromises();
  return new DOMWrapper(document.querySelector<HTMLElement>('[role="menu"]')!);
}

/** The identity lines at the top of the open menu. */
function identityLines(menu: DOMWrapper<HTMLElement>) {
  return menu.findAll('[data-account-menu-identity] p').map((line) => line.text().trim());
}

beforeEach(() => {
  useUiLocale().setUiLocale('en');
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
  vi.spyOn(catalogueApi, 'stockRows').mockResolvedValue(ResultExtensions.ok([]));
});
afterEach(() => {
  wrapper?.unmount();
  wrapper = undefined;
  document.body.innerHTML = '';
  vi.unstubAllGlobals();
});

describe('Account menu with an Ocharo session', () => {
  it('says a session without a name or contact is signed in, and ends with sign out', async () => {
    await renderSignedIn();

    const menu = await openAccountMenu();

    expect(identityLines(menu)).toEqual(['Signed in to Ocharo']);
    expect(wrapper!.get('header button[aria-haspopup="menu"]').attributes('aria-label')).toBe('Account menu');
    const rows = menu.findAll('[role="menuitem"]');
    expect(rows.map((item) => item.text().trim())).toEqual(['Sign out']);
    expect(menu.findAll('[role="menuitem"], [role="menuitemradio"]').at(-1)!.attributes()).toHaveProperty(
      'data-account-menu-sign-out',
    );
  });

  it.each([
    [{ name: 'Malika Operator', email: 'operator@example.test' }, ['Malika Operator', 'operator@example.test']],
    [{ name: 'operator@example.test', email: 'operator@example.test' }, ['operator@example.test']],
    [{ name: 'Malika Operator', email: null }, ['Malika Operator']],
    [{ name: 'Malika Operator', email: null, phoneNumber: '+998901234567' }, ['Malika Operator', '+998901234567']],
  ])('names the Identity person in the account menu (%o)', async (identity, lines) => {
    await renderSignedIn(signedInAs(identity));

    const menu = await openAccountMenu();

    expect(identityLines(menu)).toEqual(lines);
    expect(menu.text()).not.toContain('Signed in to Ocharo');
    expect(wrapper!.get('header button[aria-haspopup="menu"]').attributes('aria-label')).toBe(
      `Account menu for ${identity.name}`,
    );
    expect(menu.findAll('[role="menuitem"]').at(-1)!.attributes()).toHaveProperty('data-account-menu-sign-out');
  });

  it('names a person Identity knows only by email in the second line', async () => {
    await renderSignedIn(signedInAs({ name: null, email: 'operator@example.test' }));

    const menu = await openAccountMenu();

    expect(identityLines(menu)).toEqual(['operator@example.test']);
    expect(wrapper!.get('header button[aria-haspopup="menu"]').attributes('aria-label')).toBe('Account menu');
  });

  it('signs out at the API with its CSRF token, then leaves for Identity to end the Ocharo sign-in', async () => {
    await renderSignedIn();

    const menu = await openAccountMenu();
    await menu.get('[data-account-menu-sign-out]').trigger('click');
    await flushPromises();

    expect(fetch).toHaveBeenCalledWith(
      '/api/identity/sign-out',
      expect.objectContaining({ method: 'POST', headers: { 'X-Csrf-Token': 'csrf-token' } }),
    );
    expect(identityClient.current.isAuthenticated).toBe(false);
    expect(navigations).toEqual([EndSessionUrl]);
  });

  it('offers sign-in when the session ends, keeping the page and its unsaved changes', async () => {
    await renderSignedIn({ isAuthenticated: false });

    expect(wrapper!.text()).toContain('Reconnect to Ocharo. Your unsaved changes stay in this tab.');
    const signIn = wrapper!.get('a[target="_blank"]');
    expect(signIn.text()).toBe('Sign in to Ocharo');
    expect(signIn.attributes('href')).toMatch(/^\/api\/identity\/sign-in\?returnUrl=%2F/);
    expect(wrapper!.find('main h1').text()).toBe('Stock');
    await wrapper!.get('header button[aria-haspopup="menu"]').trigger('click');
    await flushPromises();
    expect(document.querySelector('[data-account-menu-sign-out]')).toBeNull();
  });

  it('says a signed-in person without catalogue:edit has read-only access', async () => {
    await renderSignedIn({ ...SignedIn, permissions: [] });

    expect(wrapper!.text()).toContain('You have read-only access.');
  });
});
