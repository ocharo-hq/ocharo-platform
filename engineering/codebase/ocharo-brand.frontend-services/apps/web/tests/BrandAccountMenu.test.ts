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
async function renderSignedIn(session: Record<string, unknown> = SignedIn) {
  const mounted = await mountBrand('/logos', { ssoSession: session });
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

/** The identity lines at the top of the open menu. */
function identityLines(menu: DOMWrapper<HTMLElement>) {
  return menu.findAll('[data-account-menu-identity] p').map((line) => line.text().trim());
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
  it('says where a session without a name came from, links the Desk account and ends with sign out', async () => {
    await renderSignedIn();
    const open = vi.fn();
    vi.stubGlobal('open', open);

    const menu = await openAccountMenu();

    expect(identityLines(menu)).toEqual(['Signed in through Ocharo Desk']);
    expect(wrapper!.get('header button[aria-haspopup="menu"]').attributes('aria-label')).toBe('Account menu');
    const rows = menu.findAll('[role="menuitem"]');
    expect(rows.map((item) => item.text().trim())).toEqual(['Account (opens in a new tab)', 'Sign out']);
    expect(menu.findAll('[role="menuitem"], [role="menuitemradio"]').at(-1)!.attributes()).toHaveProperty(
      'data-account-menu-sign-out',
    );
    // The old header link to the Desk account is now this menu item.
    expect(wrapper!.get('header').find('a[href="https://desk.example.test/"]').exists()).toBe(false);

    // A real link: the browser opens Desk's account page in a new tab, so unsaved edits here stay.
    const account = rows[0]!;
    expect(account.element.tagName).toBe('A');
    expect(account.attributes()).toMatchObject({
      href: 'https://desk.example.test/',
      target: '_blank',
      rel: 'noopener noreferrer',
    });
    const preventNavigation = (event: Event) => event.preventDefault();
    document.addEventListener('click', preventNavigation);
    await account.trigger('click');
    await flushPromises();
    document.removeEventListener('click', preventNavigation);
    expect(open).not.toHaveBeenCalled();
    expect(wrapper!.get('header button[aria-haspopup="menu"]').attributes('aria-expanded')).toBe('false');
  });

  it.each([
    [{ name: 'Malika Operator', email: 'operator@example.test' }, ['Malika Operator', 'operator@example.test']],
    [{ name: 'operator@example.test', email: 'operator@example.test' }, ['operator@example.test']],
    [{ name: 'Malika Operator', email: null }, ['Malika Operator']],
  ])('names the Desk operator in the account menu (%o)', async (identity, lines) => {
    await renderSignedIn({ ...SignedIn, ...identity });

    const menu = await openAccountMenu();

    expect(identityLines(menu)).toEqual(lines);
    expect(menu.text()).not.toContain('Signed in through Ocharo Desk');
    expect(wrapper!.get('header button[aria-haspopup="menu"]').attributes('aria-label')).toBe(
      `Account menu for ${identity.name}`,
    );
    expect(menu.findAll('[role="menuitem"]').at(-1)!.attributes()).toHaveProperty('data-account-menu-sign-out');
  });

  it('names an operator Desk knows only by email in the second line', async () => {
    await renderSignedIn({ ...SignedIn, name: null, email: 'operator@example.test' });

    const menu = await openAccountMenu();

    expect(identityLines(menu)).toEqual(['operator@example.test']);
    expect(wrapper!.get('header button[aria-haspopup="menu"]').attributes('aria-label')).toBe('Account menu');
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
