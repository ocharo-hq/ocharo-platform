import { defineComponent, h, nextTick } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { ColorModeProvider } from '@wow-two-beta/fe/foundation/primitives';
import { DefaultRetryPolicy } from '@wow-two-beta/fe/foundation/resilience';
import { createQueryClient, queryPlugin } from '@wow-two-beta/fe/query';

import { loadOcharoApps, ocharoAppsPlugin } from '@/application/shared/UseOcharoApps';
import { provideUiLocale, useUiLocale } from '@/application/shared/UseUiLocale';
import { useAccess } from '@/application/workspace/UseAccess';
import DeskFrame from '@/presentation/workspace/frame/DeskFrame.vue';
import WorkspacePage from '@/presentation/workspace/WorkspacePage.vue';

import { deskRouter } from './helpers/DeskRouter';

const ORG = '11111111-1111-4111-8111-111111111111';
/** The single membership most tests sign in with. */
const OwnerMembership = [{ id: ORG, name: 'Ocharo', slug: 'ocharo', role: 'owner' }];
/** The API version `/api/system/version` reports. */
const ApiVersion = '0.4.2';
/** The key earlier Desk builds stored the sidebar rail choice under. */
const SidebarCollapsedKey = 'ocharo.desk.sidebar-collapsed';
let wrapper: VueWrapper | undefined;
let client: ReturnType<typeof createQueryClient> | undefined;

afterEach(() => {
  wrapper?.unmount();
  useUiLocale().setUiLocale('en');
  localStorage.removeItem('ocharo.operator.locale');
  client?.clear();
  localStorage.removeItem(SidebarCollapsedKey);
  vi.unstubAllGlobals();
  window.history.replaceState(null, '', '/');
});

/**
 * Mounts the workspace at `path` for a viewport `width` wide, by default with one organization the operator owns.
 * Given `runtimeConfig`, Desk serves it from `/api/runtime-config` and the app menu resolves it the way startup does.
 */
async function renderAt(
  path: string,
  width: number,
  runtimeConfig?: Record<string, string>,
  organizations: readonly { id: string; name: string; slug: string; role: string }[] = OwnerMembership,
) {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: width >= Number(query.match(/min-width:\s*(\d+)px/)?.[1] ?? Infinity),
    media: query,
    onchange: null,
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    addListener: vi.fn(),
    removeListener: vi.fn(),
    dispatchEvent: vi.fn(),
  }));
  const fetch = vi.fn((input: RequestInfo | URL) => {
    const url = String(input instanceof Request ? input.url : input);
    const pathname = new URL(url, 'https://localhost').pathname;
    if (pathname === '/api/runtime-config') return Promise.resolve(Response.json(runtimeConfig ?? {}));
    if (pathname === '/api/system/version') return Promise.resolve(Response.json({ version: ApiVersion }));
    if (pathname === '/api/auth/logout') return Promise.resolve(new Response(null, { status: 204 }));
    return Promise.resolve(Response.json({ data: pathname === '/api/organizations' ? organizations : [] }));
  });
  vi.stubGlobal('fetch', fetch);
  window.history.replaceState(null, '', path);
  const router = await deskRouter();
  const assign = vi.fn();
  const ocharoApps = runtimeConfig ? [ocharoAppsPlugin((await loadOcharoApps()).addresses)] : [];
  client = createQueryClient({ retry: { ...DefaultRetryPolicy, maxRetries: 0 } });
  wrapper = mount(
    defineComponent({
      setup() {
        provideUiLocale();
        const access = useAccess({ assign, reload: vi.fn(), router });
        access.session.value = {
          authenticated: true,
          user: { id: 'operator', email: 'operator@example.test', displayName: 'Local verification operator' },
          sessionId: 'session',
          csrfToken: 'token',
          setupRequired: false,
        };
        return () => h(ColorModeProvider, { storageKey: 'ocharo.desk.theme.test' }, () => h(WorkspacePage, { access }));
      },
    }),
    { global: { plugins: [queryPlugin(client), router, ...ocharoApps] }, attachTo: document.body },
  );
  await flushPromises();
  return { router, assign, fetch };
}

/**
 * Lets the menu's teleport and focus settle, then lets the clock move on: Vue's event invoker ignores an event
 * stamped in the same millisecond its listener was attached.
 */
async function settle(): Promise<void> {
  await flushPromises();
  for (let tick = 0; tick < 4; tick += 1) await nextTick();
  const settledAt = Date.now();
  while (Date.now() === settledAt) await Promise.resolve();
}

/** The account menu's trigger in the top bar: the button around the account avatar. */
function accountTrigger(): HTMLButtonElement {
  const trigger = wrapper!.get('header [data-account-menu-avatar]').element.closest('button');
  if (!trigger) throw new Error('The top bar has no account menu.');
  return trigger;
}

/** Opens the account menu and returns its panel. */
async function openAccountMenu(): Promise<HTMLElement> {
  accountTrigger().click();
  await settle();
  const panel = [...document.querySelectorAll<HTMLElement>('[role="menu"]')].find((menu) =>
    menu.querySelector('[data-account-menu-identity]'));
  if (!panel) throw new Error('The account menu did not open.');
  return panel;
}

/** The rows of the open account menu, in order. */
function accountRows(panel: HTMLElement): HTMLElement[] {
  return [...panel.querySelectorAll<HTMLElement>('[role^="menuitem"]')];
}

/** The open menu's row whose text includes `text`. */
function accountRow(panel: HTMLElement, text: string): HTMLElement {
  const row = accountRows(panel).find((node) => node.textContent?.includes(text));
  if (!row) throw new Error(`No account menu row reads ${text}.`);
  return row;
}

describe('Ocharo frame in Desk', () => {
  it.each([360, 1440])('keeps one reachable main landmark and the suite controls at %ipx', async (width) => {
    await renderAt(`/organizations/${ORG}/brands`, width);

    const main = wrapper!.get('main');
    expect(wrapper!.findAll('main')).toHaveLength(1);
    expect(wrapper!.findAll(`a[href="#${main.attributes('id')}"]`)).toHaveLength(1);
    expect(main.attributes('tabindex')).toBe('-1');
    const header = wrapper!.get('header');
    expect(header.find('button[aria-label="Ocharo apps — Desk"]').exists()).toBe(true);
    expect(header.find('button[aria-label="Account menu for Local verification operator"]').exists()).toBe(true);
  });

  it.each([
    [360, true],
    [1440, false],
  ])('opens the Ocharo apps from the app button at %ipx, full screen only on a phone', async (width, fullScreen) => {
    await renderAt(`/organizations/${ORG}/brands`, width);

    await wrapper!.get('header button[aria-label="Ocharo apps — Desk"]').trigger('click');
    await flushPromises();

    expect(document.body.textContent).toContain('Ocharo apps');
    expect(document.querySelector('[aria-current="true"]')?.textContent).toContain('Desk');
    expect(document.querySelector('a[href="https://localhost:8282/catalogues"]')?.textContent).toContain('Catalogue');
    expect(document.querySelector('a[target="_blank"][href="https://localhost:8284/atlas/"]')).not.toBeNull();
    expect(document.querySelector('[data-phone-layout="fullscreen"]') !== null).toBe(fullScreen);
  });

  it('links the apps menu to the Catalogue address Desk serves at runtime', async () => {
    await renderAt(`/organizations/${ORG}/brands`, 1440, { OCHARO_CATALOGUE_URL: 'https://ocharo-catalogue-dev.example.ts.net/catalogues' });

    await wrapper!.get('header button[aria-label="Ocharo apps — Desk"]').trigger('click');
    await flushPromises();

    expect(document.querySelector('a[href="https://ocharo-catalogue-dev.example.ts.net/catalogues"]')?.textContent).toContain('Catalogue');
    expect(document.querySelector('a[href="https://localhost:8282/catalogues"]')).toBeNull();
    expect(document.querySelector('a[target="_blank"][href="https://localhost:8284/atlas/"]')).not.toBeNull();
  });

  it('paints the phone bar with the SDK glass variant', async () => {
    await renderAt(`/organizations/${ORG}/brands`, 390);

    expect(wrapper!.findAll('nav[aria-label="Desk sections"]')).toHaveLength(1);
    const bar = wrapper!.get('nav[aria-label="Desk sections"]').classes();
    // `variant="glass"`: the chrome glass, its edge on the top border, the upward shadow and the safe-area padding.
    expect(bar).toEqual(expect.arrayContaining(['surface-glass', 'border-t', 'border-glass-edge', 'shadow-(--glass-bottom-shadow)', 'pb-[env(safe-area-inset-bottom)]']));
    expect(bar).not.toContain('bg-background');
    expect(bar).not.toContain('surface-glass!');
    expect(bar).toEqual(expect.arrayContaining(['z-sticky', '[grid-area:footer]', 'md:hidden']));
  });

  it('keeps the sidebar rail choice this browser already stored, and stores a change under the same key', async () => {
    localStorage.setItem(SidebarCollapsedKey, 'true');
    await renderAt(`/organizations/${ORG}/brands`, 1440);

    await wrapper!.get('button[aria-label="Expand sidebar"]').trigger('click');

    expect(wrapper!.find('button[aria-label="Collapse sidebar"]').exists()).toBe(true);
    expect(localStorage.getItem(SidebarCollapsedKey)).toBe('false');
  });

  it('lists every section in the sidebar and marks the open one', async () => {
    await renderAt(`/organizations/${ORG}/brands`, 1440);

    const nav = wrapper!.get('nav[aria-label="Desk sections"]');
    const links = nav.findAll('a').map((link) => link.attributes('href'));
    expect(links).toContain(`/organizations/${ORG}/events`);
    expect(links).toContain(`/organizations/${ORG}/sourcing`);
    expect(links).toContain('/system');
    expect(nav.get('[aria-current="page"]').text()).toBe('Brands');
  });

  it('moves between sections without replacing the document', async () => {
    const { router, assign } = await renderAt(`/organizations/${ORG}/brands`, 1440);

    await router.push(`/organizations/${ORG}/media`);
    await flushPromises();

    expect(window.location.pathname).toBe(`/organizations/${ORG}/media`);
    expect(wrapper!.get('nav[aria-label="Desk sections"] [aria-current="page"]').text()).toBe('Media');
    expect(assign).not.toHaveBeenCalled();
  });
});

describe('account menu in Desk', () => {
  it('names the operator and the sole organization, with every service version and sign-out last', async () => {
    await renderAt(`/organizations/${ORG}/brands`, 1440);

    expect(accountTrigger().getAttribute('aria-label')).toBe('Account menu for Local verification operator');
    const panel = await openAccountMenu();

    expect(panel.getAttribute('aria-label')).toBe('Account');
    const identity = panel.querySelector<HTMLElement>('[data-account-menu-identity]')!;
    expect(identity.textContent).toContain('Local verification operator');
    expect(identity.textContent).toContain('operator@example.test');
    expect(identity.textContent).toContain('Ocharo · owner');
    expect(panel.querySelector('[data-account-menu-organizations]')).toBeNull();
    const versions = [...panel.querySelectorAll('[data-account-menu-version]')].map((row) =>
      [...row.children].map((cell) => cell.textContent?.trim()).join(' '));
    expect(versions).toEqual([`Desk ${__APP_VERSION__}`, `API ${ApiVersion}`]);
    const rows = accountRows(panel).map((row) => row.textContent?.trim());
    expect(rows).toEqual(['Account settings', 'System', 'Light', 'Dark', 'O‘zbekcha', 'Русский', 'English', 'Sign out']);
  });

  it('opens account settings from its own row', async () => {
    await renderAt(`/organizations/${ORG}/brands`, 1440);

    accountRow(await openAccountMenu(), 'Account settings').click();
    await settle();

    expect(wrapper!.findComponent(DeskFrame).emitted('settings')).toHaveLength(1);
  });

  it('switches organization by replacing the document, and keeps a long membership list in its own band', async () => {
    const memberships = Array.from({ length: 15 }, (_, index) => ({
      id: index === 0 ? ORG : `2222222${index.toString(16)}-2222-4222-8222-222222222222`,
      name: index === 0 ? 'Ocharo' : `Workshop ${index}`,
      slug: `workshop-${index}`,
      role: index === 0 ? 'owner' : 'editor',
    }));
    const { assign } = await renderAt(`/organizations/${ORG}/brands`, 1440, undefined, memberships);

    const panel = await openAccountMenu();
    const list = panel.querySelector<HTMLElement>('[data-account-menu-organization-list]')!;
    expect(list.querySelectorAll('[role="menuitemradio"]')).toHaveLength(15);
    expect(list.className).toContain('max-h-44');
    expect(list.className).toContain('overflow-y-auto');
    // The SDK menu glass paints the surface; the scroll box inside it keeps the panel within the viewport.
    expect(panel.className).toContain('surface-glass-menu');
    expect(panel.className).toContain('border-glass-edge');
    const scrollBox = list.parentElement!.closest<HTMLElement>('[class*="max-h-[calc(100dvh-5rem)]"]')!;
    expect(panel.contains(scrollBox)).toBe(true);
    expect(scrollBox.className).toContain('overflow-y-auto');
    expect(accountRows(panel).at(-1)?.textContent?.trim()).toBe('Sign out');
    expect(accountRow(panel, 'Ocharo').getAttribute('aria-checked')).toBe('true');

    accountRow(panel, 'Workshop 3').click();
    await settle();

    expect(assign).toHaveBeenCalledWith(`/organizations/${memberships[3]!.id}`);
  });

  it('switches the interface language and renames its own controls', async () => {
    await renderAt(`/organizations/${ORG}/brands`, 1440);

    accountRow(await openAccountMenu(), 'Русский').click();
    await settle();

    expect(useUiLocale().uiLocale.value).toBe('ru');
    expect(document.documentElement.lang).toBe('ru');
    expect(accountTrigger().getAttribute('aria-label')).toBe('Меню учётной записи Local verification operator');
    const panel = await openAccountMenu();
    expect(panel.getAttribute('aria-label')).toBe('Учётная запись');
    expect(accountRow(panel, 'Выйти')).toBe(accountRows(panel).at(-1));
    expect(accountRows(panel).map((row) => row.textContent?.trim())).toEqual(
      expect.arrayContaining(['Системная', 'Светлая', 'Тёмная']),
    );
  });

  it('signs out through the server from the last row', async () => {
    const { assign, fetch } = await renderAt(`/organizations/${ORG}/brands`, 1440);

    const panel = await openAccountMenu();
    accountRows(panel).at(-1)!.click();
    await settle();

    const logout = fetch.mock.calls.find(([input]) => String(input instanceof Request ? input.url : input).endsWith('/api/auth/logout'));
    // The client passes its request options as a second argument the mock's own signature leaves out.
    expect(((logout as unknown[] | undefined)?.[1] as RequestInit | undefined)?.method).toBe('POST');
    expect(assign).toHaveBeenCalledTimes(1);
  });
});
