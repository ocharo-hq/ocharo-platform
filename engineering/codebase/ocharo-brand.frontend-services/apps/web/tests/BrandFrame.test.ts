import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { DOMWrapper, flushPromises, type VueWrapper } from '@vue/test-utils';

import { useUiLocale } from '../src/application/shared/UseUiLocale';
import { brandApi } from '../src/integration/brand';
import { ApiVersion, mountBrand } from './helpers/BrandApp';
import { draftView, serveBrandReads, settleBrandReads } from './helpers/BrandFixtures';

/** Where this browser remembers the desktop sidebar's rail choice. */
const SidebarCollapsedKey = 'ocharo.brand.sidebar-collapsed';
let wrapper: VueWrapper | undefined;

async function renderAt(path: string, width: number, runtimeConfig?: Record<string, string>) {
  const mounted = await mountBrand(path, { width, runtimeConfig });
  wrapper = mounted.wrapper;
  await settleBrandReads();
  await flushPromises();
  return mounted;
}

/** Waits for a click's navigation; the first visit to a code-split page loads its module first. */
async function arrive(router: { currentRoute: { value: { path: string } } }, path: string) {
  await vi.waitFor(() => expect(router.currentRoute.value.path).toBe(path));
  await flushPromises();
}

/** The sidebar's section list; the phone's bottom bar carries the same name. */
function sidebar() {
  return wrapper!.get('aside').get('[aria-label="Brand sections"]');
}

beforeEach(() => {
  localStorage.clear();
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

describe('Ocharo frame in Brand', () => {
  it.each([390, 1440])('keeps one reachable main landmark and the suite controls at %ipx', async (width) => {
    await renderAt('/logos', width);

    const main = wrapper!.get('main');
    expect(wrapper!.findAll('main')).toHaveLength(1);
    expect(wrapper!.findAll(`a[href="#${main.attributes('id')}"]`)).toHaveLength(1);
    expect(wrapper!.get(`a[href="#${main.attributes('id')}"]`).text()).toBe('Skip to content');
    expect(main.attributes('tabindex')).toBe('-1');
    expect(main.findAll('h1')).toHaveLength(1);
    const header = wrapper!.get('header[role="banner"]');
    expect(header.find('button[aria-label="Ocharo apps — Brand"]').exists()).toBe(true);
    expect(header.get('[title="App version"]').text()).toBe(__APP_VERSION__);
    const controls = header.findAll('button');
    expect(controls.at(-1)!.attributes()).toMatchObject({ 'aria-label': 'Account menu', 'aria-haspopup': 'menu' });
  });

  it('reports the app and API versions and the appearance, with no languages or sign-out in the local pilot', async () => {
    await renderAt('/logos', 1440);

    await wrapper!.get('header button[aria-label="Account menu"]').trigger('click');
    await flushPromises();

    const menu = new DOMWrapper(document.querySelector<HTMLElement>('[role="menu"]')!);
    const versions = menu
      .findAll('[data-account-menu-version]')
      .map((entry) => [...entry.element.children].map((part) => part.textContent?.trim()));
    expect(versions).toEqual([
      ['Brand', __APP_VERSION__],
      ['API', `${ApiVersion.split('+')[0]} · 8f2c1d9`],
    ]);
    expect(fetch).toHaveBeenCalledWith('/api/system/status', expect.objectContaining({ credentials: 'omit' }));
    expect(menu.find('[data-account-menu-appearance]').exists()).toBe(true);
    // Brand's pages are Uzbek-only, so the menu offers no interface language until they are localized.
    expect(menu.find('[data-account-menu-languages]').exists()).toBe(false);
    expect(menu.find('[data-account-menu-sign-out]').exists()).toBe(false);
    expect(menu.find('[data-account-menu-identity]').exists()).toBe(false);
  });

  it.each([
    [390, true],
    [1440, false],
  ])('opens the Ocharo apps from the app button at %ipx, full screen only on a phone', async (width, fullScreen) => {
    await renderAt('/', width);

    await wrapper!.get('header button[aria-label="Ocharo apps — Brand"]').trigger('click');
    await flushPromises();

    expect(document.body.textContent).toContain('Ocharo apps');
    expect(document.querySelector('[aria-current="true"]')?.textContent).toContain('Brand');
    expect(document.querySelector('[aria-current="true"]')?.textContent).toContain('Logo and brand guide');
    expect(document.querySelector('a[href="https://localhost:8264/"]')?.textContent).toContain('Desk');
    expect(document.querySelector('a[href="https://localhost:8282/catalogues"]')?.textContent).toContain('Catalogue');
    expect(document.querySelector('a[target="_blank"][href="https://localhost:8284/atlas/"]')).not.toBeNull();
    expect(document.querySelector('[data-phone-layout="fullscreen"]') !== null).toBe(fullScreen);
  });

  it('links the apps menu to the Desk address Brand serves at runtime', async () => {
    await renderAt('/', 1440, { OCHARO_DESK_URL: 'https://ocharo-desk-dev.example.ts.net/' });

    await wrapper!.get('header button[aria-label="Ocharo apps — Brand"]').trigger('click');
    await flushPromises();

    expect(document.querySelector('a[href="https://ocharo-desk-dev.example.ts.net/"]')?.textContent).toContain('Desk');
    expect(document.querySelector('a[href="https://localhost:8264/"]')).toBeNull();
    expect(document.querySelector('a[href="https://localhost:8358/"]')?.textContent).toContain('Marketing');
  });

  it('lists every section in the sidebar, marks the open one and names the place in the breadcrumb', async () => {
    await renderAt('/logos', 1440);

    const links = sidebar().findAll('a');
    expect(links.map((link) => link.text())).toEqual(['Overview', 'Identity', 'Logos', 'Appearances', 'Connect']);
    expect(links.map((link) => link.attributes('href'))).toEqual([
      '/',
      '/identity',
      '/logos',
      '/appearances',
      '/connect',
    ]);
    expect(sidebar().get('[aria-current="page"]').text()).toBe('Logos');
    expect(sidebar().text()).toContain('Admin');
    expect(wrapper!.get('nav[aria-label="You are here"]').text()).toBe('Logos');
  });

  it('moves between sections and names each place in the breadcrumb and the document title', async () => {
    const { router } = await renderAt('/logos', 1440);

    await sidebar().get('a[href="/connect"]').trigger('click');
    await arrive(router, '/connect');
    expect(sidebar().get('[aria-current="page"]').text()).toBe('Connect');
    expect(wrapper!.get('nav[aria-label="You are here"]').text()).toContain('Admin');
    expect(document.title).toBe('Connect · Ocharo Brand');

    await sidebar().get('a[href="/identity"]').trigger('click');
    await arrive(router, '/identity');
    expect(sidebar().get('[aria-current="page"]').text()).toBe('Identity');
    expect(document.title).toBe('Identity · Ocharo Brand');
    expect(wrapper!.get('main').find('#name').exists()).toBe(true);
  });

  it('opens the overview for an unknown address, inside the frame', async () => {
    await renderAt('/no-such-place', 1440);

    expect(sidebar().get('[aria-current="page"]').text()).toBe('Overview');
    expect(document.title).toBe('Overview · Ocharo Brand');
  });

  it('keeps the sidebar rail choice this browser stored, and stores a change under the same key', async () => {
    localStorage.setItem(SidebarCollapsedKey, 'true');
    await renderAt('/logos', 1440);

    await wrapper!.get('button[aria-label="Expand sidebar"]').trigger('click');

    expect(wrapper!.find('button[aria-label="Collapse sidebar"]').exists()).toBe(true);
    expect(localStorage.getItem(SidebarCollapsedKey)).toBe('false');
  });

  it('shows the icon rail without a collapse control on a tablet', async () => {
    await renderAt('/logos', 900);

    expect(sidebar().text()).toContain('Logos');
    expect(wrapper!.find('button[aria-label="Expand sidebar"]').exists()).toBe(false);
    expect(wrapper!.find('button[aria-label="Collapse sidebar"]').exists()).toBe(false);
  });

  it('swaps the sidebar for a bottom bar with More on a phone', async () => {
    const { router } = await renderAt('/logos', 390);

    expect(wrapper!.find('aside').exists()).toBe(false);
    const bar = wrapper!.get('[aria-label="Brand sections"]');
    expect(bar.findAll('a').map((link) => link.text())).toEqual(['Overview', 'Identity', 'Logos', 'Appearances']);
    expect(bar.get('[aria-current="page"]').text()).toBe('Logos');

    await bar.get('button[aria-haspopup="dialog"]').trigger('click');
    await flushPromises();
    const element = document.querySelector<HTMLElement>('[role="dialog"]');
    expect(element).not.toBeNull();
    const dialog = new DOMWrapper(element!);
    expect(dialog.text()).toContain('More in Brand');
    await dialog.get('a[href="/connect"]').trigger('click');
    await arrive(router, '/connect');

    expect(document.querySelector('[role="dialog"]')).toBeNull();
    expect(
      wrapper!.get('[aria-label="Brand sections"]').get('button[aria-haspopup="dialog"]').attributes(),
    ).toHaveProperty('aria-current', 'page');
  });

  it('paints the phone bar with the SDK glass variant', async () => {
    await renderAt('/logos', 390);

    const bar = wrapper!.get('nav[aria-label="Brand sections"]').classes();
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

  it('shows the release the draft stands on and refreshes every Brand read from the bar', async () => {
    await renderAt('/', 1440);

    const header = wrapper!.get('header[role="banner"]');
    expect(header.text()).toContain('Release 3 · 2 unpublished changes');
    const reads = vi.mocked(brandApi.draft).mock.calls.length;
    const refresh = header.findAll('button').find((button) => button.text() === 'Refresh');
    await refresh!.trigger('click');
    await settleBrandReads();
    expect(brandApi.draft).toHaveBeenCalledTimes(reads + 1);
  });

  it('says when the brand has no release yet', async () => {
    serveBrandReads(draftView(null, []));
    await renderAt('/', 1440);

    expect(wrapper!.get('header[role="banner"]').text()).toContain('Not released yet');
  });
});

describe('Brand frame in Uzbek', () => {
  it('names the sections, the breadcrumb, the controls and the document in the interface language', async () => {
    useUiLocale().setUiLocale('uz');
    await renderAt('/logos', 1440);

    expect(document.documentElement.lang).toBe('uz');
    const sections = wrapper!.get('aside').get('[aria-label="Brend bo‘limlari"]');
    expect(sections.findAll('a').map((link) => link.text())).toEqual([
      'Umumiy',
      'Identitet',
      'Logotiplar',
      'Tadbirlar',
      'Ulanish',
    ]);
    expect(sections.text()).toContain('Boshqaruv');
    expect(wrapper!.get('nav[aria-label="Siz shu yerdasiz"]').text()).toBe('Logotiplar');
    const header = wrapper!.get('header[role="banner"]');
    expect(header.find('button[aria-label="Ocharo ilovalari — Brend"]').exists()).toBe(true);
    expect(header.text()).toContain('3-nashr · 2 ta nashr qilinmagan o‘zgarish');
    expect(header.text()).toContain('Yangilash');
    expect(document.title).toBe('Logotiplar · Ocharo Brand');
  });
});
