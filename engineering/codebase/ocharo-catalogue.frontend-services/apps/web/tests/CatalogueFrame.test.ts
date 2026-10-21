import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { DOMWrapper, flushPromises, type VueWrapper } from '@vue/test-utils';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import { catalogueApi } from '../src/integration/catalogue';
import { useCatalogueStore } from '../src/application/catalogue';
import { confirmation, resolveConfirmation } from '../src/application/common/UseFeedback';
import { useUiLocale } from '../src/application/shared/UseUiLocale';
import type { CatalogueSettings } from '../src/domain/catalogue/CatalogueModels';
import { ApiVersion, mountCatalogue, SelectStub } from './helpers/CatalogueApp';

/** Where this browser remembers the desktop sidebar's rail choice. */
const SidebarCollapsedKey = 'ocharo.catalogue.sidebar-collapsed';
const settings: CatalogueSettings = {
  brandName: 'Ocharo',
  currency: 'UZS',
  priceNote: '',
  sizePreset: ['S', 'M'],
  contactLines: [],
  garmentTypes: [{ key: 'nimcha', uz: 'Nimcha', ru: 'Жилет', en: 'Waistcoat' }],
};
let wrapper: VueWrapper | undefined;

async function renderAt(path: string, width: number, runtimeConfig?: Record<string, string>) {
  const mounted = await mountCatalogue(path, { width, runtimeConfig, stubs: { CatalogueSelect: SelectStub } });
  wrapper = mounted.wrapper;
  return mounted;
}

/** Waits for a click's navigation; the first visit to a code-split page loads its module first. */
async function arrive(router: { currentRoute: { value: { path: string } } }, path: string) {
  await vi.waitFor(() => expect(router.currentRoute.value.path).toBe(path));
  await flushPromises();
}

/** The sidebar's section list; the phone's bottom bar carries the same name. */
function sidebar() {
  return wrapper!.get('aside').get('[aria-label="Catalogue sections"]');
}

beforeEach(() => {
  localStorage.clear();
  useUiLocale().setUiLocale('en');
  const store = useCatalogueStore();
  store.settings.value = null;
  store.notice.value = null;
  vi.spyOn(catalogueApi, 'getSettings').mockResolvedValue(
    ResultExtensions.ok({ settings: structuredClone(settings), revision: 1, sourceRoot: null }),
  );
  vi.spyOn(catalogueApi, 'stockRows').mockResolvedValue(ResultExtensions.ok([]));
  vi.spyOn(catalogueApi, 'listProducts').mockResolvedValue(ResultExtensions.ok([]));
  vi.spyOn(catalogueApi, 'listCollections').mockResolvedValue(ResultExtensions.ok([]));
  vi.spyOn(catalogueApi, 'revisions').mockResolvedValue(ResultExtensions.ok([]));
});
afterEach(() => {
  if (confirmation.value) resolveConfirmation(false);
  wrapper?.unmount();
  wrapper = undefined;
  document.body.innerHTML = '';
  vi.unstubAllGlobals();
});

describe('Ocharo frame in Catalogue', () => {
  it.each([360, 1440])('keeps one reachable main landmark and the suite controls at %ipx', async (width) => {
    await renderAt('/stock', width);

    const main = wrapper!.get('main');
    expect(wrapper!.findAll('main')).toHaveLength(1);
    expect(wrapper!.findAll(`a[href="#${main.attributes('id')}"]`)).toHaveLength(1);
    expect(wrapper!.get(`a[href="#${main.attributes('id')}"]`).text()).toBe('Skip to content');
    expect(main.attributes('tabindex')).toBe('-1');
    expect(main.findAll('h1')).toHaveLength(1);
    const header = wrapper!.get('header[role="banner"]');
    expect(header.find('button[aria-label="Ocharo apps — Catalogue"]').exists()).toBe(true);
    expect(header.get('[title="App version"]').text()).toBe(__APP_VERSION__);
    expect(header.find('[aria-label="Notifications"]').exists()).toBe(true);
    const controls = header.findAll('button');
    expect(controls.at(-1)!.attributes()).toMatchObject({ 'aria-label': 'Account menu', 'aria-haspopup': 'menu' });
  });

  it('reports the app and API versions in the account menu, with no sign-out outside an Ocharo session', async () => {
    await renderAt('/stock', 1440);

    await wrapper!.get('header button[aria-label="Account menu"]').trigger('click');
    await flushPromises();

    const menu = new DOMWrapper(document.querySelector<HTMLElement>('[role="menu"]')!);
    const versions = menu
      .findAll('[data-account-menu-version]')
      .map((entry) => [...entry.element.children].map((part) => part.textContent?.trim()));
    expect(versions).toEqual([
      ['Catalogue', __APP_VERSION__],
      ['API', `${ApiVersion.split('+')[0]} · 8f2c1d9`],
    ]);
    expect(fetch).toHaveBeenCalledWith('/api/system/status', expect.objectContaining({ credentials: 'omit' }));
    expect(menu.find('[data-account-menu-sign-out]').exists()).toBe(false);
    expect(menu.find('[data-account-menu-identity]').exists()).toBe(false);
    expect(menu.find('[data-account-menu-appearance]').exists()).toBe(true);
    expect(menu.find('[data-account-menu-languages]').exists()).toBe(true);
  });

  it.each([
    [360, true],
    [1440, false],
  ])('opens the Ocharo apps from the app button at %ipx, full screen only on a phone', async (width, fullScreen) => {
    await renderAt('/stock', width);

    await wrapper!.get('header button[aria-label="Ocharo apps — Catalogue"]').trigger('click');
    await flushPromises();

    expect(document.body.textContent).toContain('Ocharo apps');
    expect(document.querySelector('[aria-current="true"]')?.textContent).toContain('Catalogue');
    expect(document.querySelector('a[href="https://localhost:8264/"]')?.textContent).toContain('Desk');
    expect(document.querySelector('a[target="_blank"][href="https://localhost:8284/atlas/"]')).not.toBeNull();
    expect(document.querySelector('[data-phone-layout="fullscreen"]') !== null).toBe(fullScreen);
  });

  it('links the apps menu to the Desk address Catalogue serves at runtime', async () => {
    await renderAt('/stock', 1440, { OCHARO_DESK_URL: 'https://ocharo-desk-dev.example.ts.net/' });

    await wrapper!.get('header button[aria-label="Ocharo apps — Catalogue"]').trigger('click');
    await flushPromises();

    expect(document.querySelector('a[href="https://ocharo-desk-dev.example.ts.net/"]')?.textContent).toContain('Desk');
    expect(document.querySelector('a[href="https://localhost:8264/"]')).toBeNull();
    expect(document.querySelector('a[href="https://localhost:8358/"]')?.textContent).toContain('Marketing');
  });

  it('lists every section in the sidebar, marks the open one and names the place in the breadcrumb', async () => {
    await renderAt('/stock', 1440);

    const links = sidebar().findAll('a');
    expect(links.map((link) => link.text())).toEqual(['Catalogues', 'Collections', 'Products', 'Stock', 'Settings']);
    expect(links.map((link) => link.attributes('href'))).toEqual([
      '/catalogues',
      '/collections',
      '/',
      '/stock',
      '/settings',
    ]);
    expect(sidebar().get('[aria-current="page"]').text()).toBe('Stock');
    expect(sidebar().text()).toContain('Admin');
    expect(wrapper!.get('nav[aria-label="You are here"]').text()).toBe('Stock');
  });

  it('moves between sections and keeps a product-wide tool under Products', async () => {
    const { router } = await renderAt('/stock', 1440);

    await sidebar().get('a[href="/settings"]').trigger('click');
    await arrive(router, '/settings');
    expect(sidebar().get('[aria-current="page"]').text()).toBe('Settings');
    expect(wrapper!.get('nav[aria-label="You are here"]').text()).toContain('Admin');
    expect(document.title).toBe('Settings · Ocharo Catalogue');

    await router.push('/products/new');
    await flushPromises();
    expect(sidebar().get('[aria-current="page"]').text()).toBe('Products');
    expect(document.title).toBe('New product · Ocharo Catalogue');
    expect(wrapper!.get('nav[aria-label="You are here"]').text()).toContain('New product');
  });

  it('keeps the sidebar rail choice this browser stored, and stores a change under the same key', async () => {
    localStorage.setItem(SidebarCollapsedKey, 'true');
    await renderAt('/stock', 1440);

    await wrapper!.get('button[aria-label="Expand sidebar"]').trigger('click');

    expect(wrapper!.find('button[aria-label="Collapse sidebar"]').exists()).toBe(true);
    expect(localStorage.getItem(SidebarCollapsedKey)).toBe('false');
  });

  it('shows the icon rail without a collapse control on a tablet', async () => {
    await renderAt('/stock', 900);

    expect(sidebar().text()).toContain('Stock');
    expect(wrapper!.find('button[aria-label="Expand sidebar"]').exists()).toBe(false);
    expect(wrapper!.find('button[aria-label="Collapse sidebar"]').exists()).toBe(false);
  });

  it('swaps the sidebar for a bottom bar with More on a phone', async () => {
    const { router } = await renderAt('/stock', 360);

    expect(wrapper!.find('aside').exists()).toBe(false);
    const bar = wrapper!.get('[aria-label="Catalogue sections"]');
    expect(bar.findAll('a').map((link) => link.text())).toEqual(['Catalogues', 'Collections', 'Products', 'Stock']);
    expect(bar.get('[aria-current="page"]').text()).toBe('Stock');

    await bar.get('button[aria-haspopup="dialog"]').trigger('click');
    await flushPromises();
    const element = document.querySelector<HTMLElement>('[role="dialog"]');
    expect(element).not.toBeNull();
    const dialog = new DOMWrapper(element!);
    expect(dialog.text()).toContain('More in Catalogue');
    await dialog.get('a[href="/settings"]').trigger('click');
    await arrive(router, '/settings');

    expect(document.querySelector('[role="dialog"]')).toBeNull();
    expect(
      wrapper!.get('[aria-label="Catalogue sections"]').get('button[aria-haspopup="dialog"]').attributes(),
    ).toHaveProperty('aria-current', 'page');
  });

  it('paints the phone bar with the SDK glass variant', async () => {
    await renderAt('/stock', 390);

    const bar = wrapper!.get('nav[aria-label="Catalogue sections"]').classes();
    // `variant="glass"`: the chrome glass, its edge on the top border, the upward shadow and the safe-area padding.
    expect(bar).toEqual(
      expect.arrayContaining([
        'surface-glass',
        'border-t',
        'border-glass-edge',
        'shadow-(--glass-bottom-shadow)',
        'pb-[env(safe-area-inset-bottom)]',
      ]),
    );
    expect(bar).not.toContain('bg-background');
    expect(bar).not.toContain('surface-glass!');
    expect(bar).toEqual(expect.arrayContaining(['no-print', 'z-sticky', '[grid-area:footer]', 'md:hidden']));
  });

  it('still asks before a section change discards unsaved product edits', async () => {
    const { router } = await renderAt('/products/new', 1440);
    // The editor is a dialog over the product list, so its fields render outside the frame's main region.
    const variant = () => new DOMWrapper(document.querySelector<HTMLInputElement>('#variant')!);
    await vi.waitFor(() => expect(document.querySelector('#variant')).not.toBeNull());
    await variant().setValue('v99');

    await sidebar().get('a[href="/stock"]').trigger('click');
    await vi.waitFor(() => expect(confirmation.value).not.toBeNull());

    expect(confirmation.value?.message).toBe('Discard unsaved changes?');
    resolveConfirmation(false);
    await flushPromises();
    expect(router.currentRoute.value.path).toBe('/products/new');
    expect(variant().element.value).toBe('v99');

    await sidebar().get('a[href="/stock"]').trigger('click');
    await vi.waitFor(() => expect(confirmation.value).not.toBeNull());
    resolveConfirmation(true);
    await arrive(router, '/stock');
  });

  it('renders the published reader without the operator frame', async () => {
    await renderAt('/c/published-example', 1440);

    expect(wrapper!.findAll('main')).toHaveLength(1);
    expect(wrapper!.find('button[aria-label="Ocharo apps — Catalogue"]').exists()).toBe(false);
    expect(wrapper!.find('aside').exists()).toBe(false);
    expect(wrapper!.find('[aria-label="Catalogue sections"]').exists()).toBe(false);
    expect(wrapper!.get('header[role="banner"]').find('button[aria-label="Settings"]').exists()).toBe(true);
    expect(catalogueApi.getSettings).not.toHaveBeenCalled();
  });
});
