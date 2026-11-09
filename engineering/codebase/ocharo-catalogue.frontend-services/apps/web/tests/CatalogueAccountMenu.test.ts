import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { DOMWrapper, flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { AuthStatus } from '@wow-two-beta/fe/auth';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import { RouterHistoryMode } from '@wow-two-beta/fe/router';
import { catalogueApi } from '../src/integration/catalogue';
import { catalogueHttp, identityBridge } from '../src/integration/common/IdentitySession';
import { useUiLocale } from '../src/application/shared/UseUiLocale';
import AppRoot from '../src/bootstrap/AppRoot.vue';
import { createCatalogueRouter } from '../src/bootstrap/CatalogueRouter';
import { EndSessionUrl, mountCatalogue, stubCatalogueHost, stubViewport } from './helpers/CatalogueApp';
import { navigations, OwnerSessionData, SignedOutSessionData } from './helpers/IdentitySessions';

const SignedIn = { ...OwnerSessionData, permissions: ['catalogue:edit'] };
type Person = Partial<(typeof OwnerSessionData)['person']> | Record<string, string | null>;
const signedInAs = (person: Person) => ({ ...SignedIn, person: { ...SignedIn.person, ...person } });
let wrapper: VueWrapper | undefined;

/** Mounts the frame with an Ocharo session, which the root's provider reads before the first place opens. */
async function renderSignedIn(session: Record<string, unknown> = SignedIn) {
  const mounted = await mountCatalogue('/stock', { session });
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

    const signOut = requestTo('/api/identity/sign-out');
    expect(signOut?.init.method).toBe('POST');
    expect(new Headers(signOut?.init.headers).get('X-Csrf-Token')).toBe('csrf-token');
    expect(identityBridge.getSession().status).toBe(AuthStatus.Anonymous);
    expect(navigations).toEqual([EndSessionUrl]);
  });

  it('reports a failed sign-out and reads the session again, which stays', async () => {
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
    expect(navigations).toEqual([]);
  });

  it('sends the session’s CSRF token on Catalogue writes and none on reads', async () => {
    await renderSignedIn();

    await catalogueHttp.post('/api/catalogue/products/bulk/status', { body: {} });
    await catalogueHttp.get('/api/catalogue/products');

    expect(new Headers(requestTo('/bulk/status')?.init.headers).get('X-Csrf-Token')).toBe('csrf-token');
    expect(new Headers(requestTo('/api/catalogue/products')?.init.headers).has('X-Csrf-Token')).toBe(false);
  });

  it('offers sign-in in another tab when the session ends, keeping the page and its unsaved changes', async () => {
    await renderSignedIn();
    vi.mocked(fetch).mockImplementation(() => Promise.resolve(Response.json({ title: 'Sign in' }, { status: 401 })));

    await catalogueHttp.get('/api/catalogue/products/stock');
    await flushPromises();

    expect(identityBridge.getSession().status).toBe(AuthStatus.Anonymous);
    expect(wrapper!.text()).toContain('Reconnect to Ocharo. Your unsaved changes stay in this tab.');
    const signIn = wrapper!.get('a[target="_blank"]');
    expect(signIn.text()).toBe('Sign in to Ocharo');
    expect(signIn.attributes('href')).toMatch(/^\/api\/identity\/sign-in\?returnUrl=%2F/);
    expect(wrapper!.find('main h1').text()).toBe('Stock');
    expect(navigations).toEqual([]);
    await wrapper!.get('header button[aria-haspopup="menu"]').trigger('click');
    await flushPromises();
    expect(document.querySelector('[data-account-menu-sign-out]')).toBeNull();
  });

  it('reads the session again after a 403 or a stale CSRF token, without retrying the write', async () => {
    await renderSignedIn();
    const sessionReads = () =>
      vi.mocked(fetch).mock.calls.filter(([input]) => String(input).endsWith('/api/identity/session')).length;
    const before = sessionReads();
    const serve = vi.mocked(fetch).getMockImplementation()!;
    vi.mocked(fetch).mockImplementation((input, init) =>
      String(input).includes('/api/catalogue/')
        ? Promise.resolve(Response.json({ title: 'Forbidden' }, { status: 403 }))
        : serve(input, init),
    );

    const refused = await catalogueHttp.post('/api/catalogue/products/bulk/status', { body: {} });
    await flushPromises();
    expect(refused.ok).toBe(false);
    expect(sessionReads()).toBe(before + 1);

    vi.mocked(fetch).mockImplementation((input, init) =>
      String(input).includes('/api/catalogue/')
        ? Promise.resolve(Response.json({ code: 'AntiforgeryValidationFailed' }, { status: 400 }))
        : serve(input, init),
    );
    await catalogueHttp.post('/api/catalogue/products/bulk/status', { body: {} });
    await flushPromises();
    expect(sessionReads()).toBe(before + 2);
    expect(
      vi.mocked(fetch).mock.calls.filter(([input]) => String(input).endsWith('/products/bulk/status')),
    ).toHaveLength(2);
  });

  it('says a signed-in person without catalogue:edit has read-only access', async () => {
    await renderSignedIn({ ...SignedIn, permissions: [] });

    expect(wrapper!.text()).toContain('You have read-only access.');
  });
});

describe('Catalogue sign-in', () => {
  it('starts sign-in at Ocharo’s Identity for a visit without a session, keeping the splash up', async () => {
    stubViewport(1440);
    stubCatalogueHost({ session: SignedOutSessionData });
    const router = createCatalogueRouter({ history: RouterHistoryMode.Memory });
    wrapper = mount(AppRoot, { attachTo: document.body, global: { plugins: [router] } });
    // The guard holds the navigation while the browser leaves for sign-in.
    void router.replace('/stock');
    await vi.waitFor(() => expect(navigations).toEqual(['/api/identity/sign-in?returnUrl=%2Fstock']));
    await flushPromises();

    expect(window.location.replace).toHaveBeenCalled();
    expect(document.querySelector('main')).toBeNull();
    expect(document.querySelector('[role="status"] [role="progressbar"]')).not.toBeNull();
  });

  it('opens the frame after a failed sign-in, says so and offers it again', async () => {
    window.history.replaceState(null, '', '/stock?signInError=callback_failed');
    try {
      const mounted = await mountCatalogue('/stock?signInError=callback_failed', { session: SignedOutSessionData });
      wrapper = mounted.wrapper;

      expect(wrapper.text()).toContain('Sign-in did not complete. Try again.');
      expect(wrapper.get('a[href^="/api/identity/sign-in"]').attributes('href')).toBe(
        '/api/identity/sign-in?returnUrl=%2Fstock',
      );
      expect(navigations).toEqual([]);
    } finally {
      window.history.replaceState(null, '', '/');
    }
  });

  it('opens the frame and offers sign-in when the session route does not answer', async () => {
    stubViewport(1440);
    stubCatalogueHost();
    const serve = vi.mocked(fetch).getMockImplementation()!;
    vi.mocked(fetch).mockImplementation((input, init) =>
      String(input).endsWith('/api/identity/session')
        ? Promise.resolve(Response.json({ title: 'Unavailable' }, { status: 503 }))
        : serve(input, init),
    );
    const router = createCatalogueRouter({ history: RouterHistoryMode.Memory });
    wrapper = mount(AppRoot, { attachTo: document.body, global: { plugins: [router] } });
    await router.replace('/stock');
    await flushPromises();

    expect(navigations).toEqual([]);
    expect(wrapper.find('main h1').text()).toBe('Stock');
    expect(wrapper.text()).toContain('Reconnect to Ocharo. Your unsaved changes stay in this tab.');
  });

  it('reads no session on the published reader, and reads it once the operator frame is entered', async () => {
    vi.spyOn(catalogueApi, 'getSettings');
    const mounted = await mountCatalogue('/c/published-example?locale=en');
    wrapper = mounted.wrapper;
    const router = mounted.router;
    const sessionReads = () =>
      vi.mocked(fetch).mock.calls.filter(([input]) => String(input).endsWith('/api/identity/session')).length;
    expect(sessionReads()).toBe(0);

    await router.push('/stock');
    await flushPromises();

    expect(sessionReads()).toBe(1);
    expect(navigations).toEqual([]);
    expect(document.querySelector('main h1')?.textContent).toBe('Stock');
  });
});
