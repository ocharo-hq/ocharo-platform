import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { DOMWrapper, flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { AuthStatus } from '@wow-two-beta/fe/auth';
import { ApiFailureFactory } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import { queryPlugin } from '@wow-two-beta/fe/query';
import { RouterHistoryMode } from '@wow-two-beta/fe/router';

import { useUiLocale } from '../src/application/shared/UseUiLocale';
import AppRoot from '../src/bootstrap/AppRoot.vue';
import { createBrandQueryClient } from '../src/bootstrap/BrandQueryClient';
import { createBrandRouter } from '../src/bootstrap/BrandRouter';
import { brandApi } from '../src/integration/brand';
import { brandHttp, identityBridge } from '../src/integration/common/IdentitySession';
import {
  EndSessionUrl,
  mountBrand,
  OwnerSession,
  SignedOutSession,
  stubBrandHost,
  stubViewport,
} from './helpers/BrandApp';
import { serveBrandReads, settleBrandReads } from './helpers/BrandFixtures';

let wrapper: VueWrapper | undefined;

/** Mounts the frame with an Ocharo session, as startup resolves it before the first place opens. */
async function renderSignedIn(person: Record<string, unknown> = {}, session: Record<string, unknown> = {}) {
  const mounted = await mountBrand('/logos', {
    identitySession: { ...OwnerSession, ...session, person: { ...OwnerSession.person, ...person } },
  });
  await settleBrandReads();
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

/** The request the mocked transport received for `path`. */
function requestTo(path: string) {
  const call = vi.mocked(fetch).mock.calls.find(([input]) => String(input).endsWith(path));
  return call ? { url: String(call[0]), init: call[1] ?? {} } : undefined;
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
    [{ name: ' Malika Operator ' }, ['Malika Operator']],
  ])('names the signed-in person in the account menu (%o)', async (person, lines) => {
    await renderSignedIn(person);

    const menu = await openAccountMenu();

    expect(identityLines(menu)).toEqual(lines);
    expect(menu.text()).not.toContain('Signed in to Ocharo');
    expect(wrapper!.get('header button[aria-haspopup="menu"]').attributes('aria-label')).toBe(
      `Account menu for ${person.name.trim()}`,
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

    const signOut = requestTo('/api/identity/sign-out');
    expect(signOut?.init.method).toBe('POST');
    expect(new Headers(signOut?.init.headers).get('X-Csrf-Token')).toBe('csrf-token');
    expect(identityBridge.getSession().status).toBe(AuthStatus.Anonymous);
    expect(navigate).toHaveBeenCalledWith(EndSessionUrl);
  });

  it('reports a failed sign-out and reads the session again, which stays', async () => {
    const navigate = vi.spyOn(window.location, 'assign').mockImplementation(() => {});
    await renderSignedIn();
    const serve = vi.mocked(fetch).getMockImplementation()!;
    vi.mocked(fetch).mockImplementation((input, init) =>
      String(input).endsWith('/api/identity/sign-out')
        ? Promise.resolve(Response.json({ title: 'Unavailable' }, { status: 503 }))
        : serve(input, init),
    );

    const menu = await openAccountMenu();
    await menu.get('[data-account-menu-sign-out]').trigger('click');
    await flushPromises();

    expect(document.body.textContent).toContain('Could not sign out. Try again.');
    expect(identityBridge.getSession().status).toBe(AuthStatus.Authenticated);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('sends the session’s CSRF token on Brand writes and none on reads', async () => {
    await renderSignedIn();

    await brandHttp.post('/api/brand/appearances/a-1/cancel', { body: {} });
    await brandHttp.get('/api/brand/draft');

    expect(new Headers(requestTo('/cancel')?.init.headers).get('X-Csrf-Token')).toBe('csrf-token');
    expect(new Headers(requestTo('/api/brand/draft')?.init.headers).has('X-Csrf-Token')).toBe(false);
  });

  it('offers to reconnect in another tab when the session ends, keeping the page', async () => {
    await renderSignedIn();
    vi.mocked(fetch).mockImplementation(() => Promise.resolve(Response.json({ title: 'Sign in' }, { status: 401 })));

    await brandHttp.get('/api/brand/draft');
    await flushPromises();

    expect(identityBridge.getSession().status).toBe(AuthStatus.Anonymous);
    expect(wrapper!.text()).toContain('Reconnect to Ocharo. Your unsaved changes stay in this tab.');
    const signIn = wrapper!.get('a[href^="/api/identity/sign-in"]');
    expect(signIn.attributes()).toMatchObject({ target: '_blank', href: '/api/identity/sign-in?returnUrl=%2F' });
    expect(wrapper!.find('main h1').exists()).toBe(true);
  });

  it('marks a reader as read-only and an account of another organization as refused', async () => {
    await renderSignedIn({}, { permissions: [] });
    expect(wrapper!.text()).toContain('You have read-only access.');

    vi.mocked(brandApi.draft).mockResolvedValue(
      ResultExtensions.fail({
        ...ApiFailureFactory.create('http'),
        status: 403,
        problem: { title: 'Forbidden', code: 'Forbidden', brandCode: 'organization_access_required' },
      }),
    );
    const refresh = wrapper!
      .get('header')
      .findAll('button')
      .find((button) => button.text() === 'Refresh');
    await refresh!.trigger('click');
    await settleBrandReads();

    expect(wrapper!.text()).toContain('This Ocharo account has no access to Brand.');
    expect(requestTo('/api/identity/session')).toBeDefined();
  });
});

describe('Brand sign-in', () => {
  it('starts sign-in at Ocharo’s Identity for a visit without a session, keeping the splash up', async () => {
    const navigate = vi.spyOn(window.location, 'assign').mockImplementation(() => {});
    stubViewport(1440);
    stubBrandHost({ identitySession: SignedOutSession });
    const router = createBrandRouter({ history: RouterHistoryMode.Memory });
    wrapper = mount(AppRoot, {
      attachTo: document.body,
      global: { plugins: [router, queryPlugin(createBrandQueryClient())] },
    });
    // The guard holds the navigation while the browser leaves for sign-in.
    void router.replace('/logos');
    await vi.waitFor(() => expect(navigate).toHaveBeenCalledWith('/api/identity/sign-in?returnUrl=%2Flogos'));
    await flushPromises();

    expect(document.querySelector('main')).toBeNull();
    expect(document.querySelector('[role="status"] [role="progressbar"]')).not.toBeNull();
  });

  it('opens the frame after a failed sign-in, says so and offers it again', async () => {
    const navigate = vi.spyOn(window.location, 'assign').mockImplementation(() => {});
    window.history.replaceState(null, '', '/logos?signInError=callback_failed');
    try {
      const mounted = await mountBrand('/logos?signInError=callback_failed', { identitySession: SignedOutSession });
      wrapper = mounted.wrapper;
      await settleBrandReads();

      expect(wrapper.text()).toContain('Sign-in was not completed. Try again.');
      expect(wrapper.get('a[href^="/api/identity/sign-in"]').attributes('href')).toBe(
        '/api/identity/sign-in?returnUrl=%2Flogos',
      );
      expect(navigate).not.toHaveBeenCalledWith(expect.stringContaining('returnUrl=%2Flogos%3FsignInError'));
    } finally {
      window.history.replaceState(null, '', '/');
    }
  });
});
