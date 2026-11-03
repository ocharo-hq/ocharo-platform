import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { DOMWrapper, flushPromises, type VueWrapper } from '@vue/test-utils';

import { useUiLocale } from '../src/application/shared/UseUiLocale';
import { identitySession } from '../src/integration/common/IdentitySessionClient';
import { EndSessionUrl, mountBrand, OwnerSession } from './helpers/BrandApp';
import { serveBrandReads, settleBrandReads } from './helpers/BrandFixtures';

let wrapper: VueWrapper | undefined;

/** Mounts the frame with an Ocharo session, as startup resolves it before mounting. */
async function renderSignedIn(person: Record<string, unknown> = {}, session: Record<string, unknown> = {}) {
  const mounted = await mountBrand('/logos', {
    identitySession: { ...OwnerSession, ...session, person: { ...OwnerSession.person, ...person } },
  });
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
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  useUiLocale().setUiLocale('uz');
});

describe('Account menu with an Ocharo session', () => {
  it('says where a session without a name came from and ends with sign out', async () => {
    await renderSignedIn();

    const menu = await openAccountMenu();

    expect(identityLines(menu)).toEqual(['Signed in to Ocharo']);
    expect(wrapper!.get('header button[aria-haspopup="menu"]').attributes('aria-label')).toBe('Account menu');
    expect(menu.findAll('[role="menuitem"]').map((item) => item.text().trim())).toEqual(['Sign out']);
    expect(menu.findAll('[role="menuitem"], [role="menuitemradio"]').at(-1)!.attributes()).toHaveProperty(
      'data-account-menu-sign-out',
    );
  });

  it.each([
    [{ name: 'Malika Operator', email: 'operator@example.test' }, ['Malika Operator', 'operator@example.test']],
    [{ name: 'operator@example.test', email: 'operator@example.test' }, ['operator@example.test']],
    [{ name: 'Malika Operator', phoneNumber: '+998901234567' }, ['Malika Operator', '+998901234567']],
    [{ name: 'Malika Operator' }, ['Malika Operator']],
  ])('names the signed-in person in the account menu (%o)', async (person, lines) => {
    await renderSignedIn(person);

    const menu = await openAccountMenu();

    expect(identityLines(menu)).toEqual(lines);
    expect(menu.text()).not.toContain('Signed in to Ocharo');
    expect(wrapper!.get('header button[aria-haspopup="menu"]').attributes('aria-label')).toBe(
      `Account menu for ${person.name}`,
    );
  });

  it('names a person Identity knows only by phone in the second line', async () => {
    await renderSignedIn({ phoneNumber: '+998901234567' });

    const menu = await openAccountMenu();

    expect(identityLines(menu)).toEqual(['+998901234567']);
    expect(wrapper!.get('header button[aria-haspopup="menu"]').attributes('aria-label')).toBe('Account menu');
  });

  it('signs out with the CSRF token and leaves through Identity’s end-session address', async () => {
    const navigate = vi.spyOn(window.location, 'assign').mockImplementation(() => {});
    await renderSignedIn();

    const menu = await openAccountMenu();
    await menu.get('[data-account-menu-sign-out]').trigger('click');
    await flushPromises();

    expect(fetch).toHaveBeenCalledWith(
      '/api/identity/sign-out',
      expect.objectContaining({ method: 'POST', headers: expect.objectContaining({ 'X-Csrf-Token': 'csrf-token' }) }),
    );
    expect(identitySession.current.authenticated).toBe(false);
    expect(navigate).toHaveBeenCalledWith(EndSessionUrl);
  });

  it('offers to reconnect in another tab when the session ends, keeping the page', async () => {
    await renderSignedIn();
    vi.mocked(fetch).mockImplementation(() => Promise.resolve(Response.json({ title: 'Sign in' }, { status: 401 })));

    await identitySession.fetch('/api/brand/draft');
    await flushPromises();

    expect(wrapper!.text()).toContain('Reconnect to Ocharo. Your unsaved changes stay in this tab.');
    const signIn = wrapper!.get('a[href^="/api/identity/sign-in"]');
    expect(signIn.attributes()).toMatchObject({ target: '_blank', href: '/api/identity/sign-in?returnUrl=%2F' });
    expect(wrapper!.find('main h1').exists()).toBe(true);
  });

  it('marks a reader as read-only and an account of another organization as refused', async () => {
    await renderSignedIn({}, { permissions: [] });
    expect(wrapper!.text()).toContain('You have read-only access.');

    vi.mocked(fetch).mockImplementation((input) =>
      Promise.resolve(
        String(input).endsWith('/api/identity/session')
          ? Response.json({ data: { ...OwnerSession, permissions: [] } })
          : Response.json({ title: 'Forbidden', code: 'organization_access_required' }, { status: 403 }),
      ),
    );
    await identitySession.fetch('/api/brand/draft');
    await flushPromises();

    expect(identitySession.current.organizationDenied).toBe(true);
    expect(wrapper!.text()).toContain('This Ocharo account has no access to Brand.');
  });
});
