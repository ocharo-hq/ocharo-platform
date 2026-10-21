import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { DOMWrapper, flushPromises, type VueWrapper } from '@vue/test-utils';

import { useUiLocale } from '../src/application/shared/UseUiLocale';
import { ssoClient } from '../src/integration/common/OcharoSsoClient';
import { mountBrand } from './helpers/BrandApp';
import { serveBrandReads, settleBrandReads } from './helpers/BrandFixtures';

// The app's session client reads the global fetch on every call, so each test serves its own Ocharo session.
vi.mock('../src/integration/common/OcharoSsoClient', async (importOriginal) => {
  const original = await importOriginal<typeof import('../src/integration/common/OcharoSsoClient')>();
  return { ...original, ssoClient: original.createSsoClient((input, init) => globalThis.fetch(input, init)) };
});

const SignedIn = {
  enabled: true,
  authenticated: true,
  canEdit: true,
  canApprove: false,
  csrfToken: 'csrf-token',
  accountUrl: 'https://desk.example.test/',
};
let wrapper: VueWrapper | undefined;

/** Mounts the frame with an Ocharo session, as startup resolves it before mounting. */
async function renderSignedIn() {
  const mounted = await mountBrand('/logos', { ssoSession: SignedIn });
  await ssoClient.initialize(true);
  await settleBrandReads();
  await flushPromises();
  wrapper = mounted.wrapper;
}

async function openAccountMenu() {
  await wrapper!.get('header button[aria-haspopup="menu"]').trigger('click');
  await flushPromises();
  return new DOMWrapper(document.querySelector<HTMLElement>('[role="menu"]')!);
}

beforeEach(() => {
  useUiLocale().setUiLocale('en');
  serveBrandReads();
});
afterEach(() => {
  wrapper?.unmount();
  wrapper = undefined;
  document.body.innerHTML = '';
  vi.unstubAllGlobals();
  useUiLocale().setUiLocale('uz');
});

describe('Account menu with an Ocharo session', () => {
  it('names the session, offers the Desk account as an item and ends with sign out', async () => {
    await renderSignedIn();
    const open = vi.fn();
    vi.stubGlobal('open', open);

    const menu = await openAccountMenu();

    expect(menu.get('[data-account-menu-identity]').text()).toBe('Signed in through Ocharo Desk');
    const rows = menu.findAll('[role="menuitem"]');
    expect(rows.map((item) => item.text().trim())).toEqual(['Account', 'Sign out']);
    expect(menu.findAll('[role="menuitem"], [role="menuitemradio"]').at(-1)!.attributes()).toHaveProperty(
      'data-account-menu-sign-out',
    );
    // The old header link to the Desk account is now this menu item.
    expect(wrapper!.get('header').find('a[href="https://desk.example.test/"]').exists()).toBe(false);

    await rows[0]!.trigger('click');
    expect(open).toHaveBeenCalledWith('https://desk.example.test/', '_blank', 'noopener,noreferrer');
  });

  it('signs this app out of Ocharo with its CSRF token and keeps the tab, offering to reconnect', async () => {
    await renderSignedIn();

    const menu = await openAccountMenu();
    await menu.get('[data-account-menu-sign-out]').trigger('click');
    await flushPromises();

    expect(fetch).toHaveBeenCalledWith(
      '/api/sso/logout',
      expect.objectContaining({ method: 'POST', headers: { 'X-CSRF-Token': 'csrf-token' } }),
    );
    expect(ssoClient.current.authenticated).toBe(false);
    expect(wrapper!.text()).toContain('Reconnect to Ocharo. Your unsaved changes stay in this tab.');
    expect(wrapper!.find('main h1').exists()).toBe(true);
    await wrapper!.get('header button[aria-haspopup="menu"]').trigger('click');
    await flushPromises();
    expect(document.querySelector('[data-account-menu-sign-out]')).toBeNull();
  });
});
