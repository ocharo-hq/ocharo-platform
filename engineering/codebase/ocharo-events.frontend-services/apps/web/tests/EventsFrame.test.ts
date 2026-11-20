import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { DOMWrapper, flushPromises, type VueWrapper } from '@vue/test-utils';

import { useUiLocale } from '../src/application/shared/UseUiLocale';
import { AdminSession, ApiVersion, EditorSession, OrganizationId, mountEvents } from './helpers/EventsApp';

let wrapper: VueWrapper | undefined;
const eventsPath = `/organizations/${OrganizationId}/events`;
const placePath = (place: string) => `/organizations/${OrganizationId}/${place}`;
/** Where this browser remembers the desktop sidebar's rail choice. */
const SidebarCollapsedKey = 'ocharo.events.sidebar-collapsed';

async function renderAt(path: string, width = 1440, identitySession?: Record<string, unknown>) {
  const mounted = await mountEvents(path, { width, ...(identitySession ? { identitySession } : {}) });
  wrapper = mounted.wrapper;
  await flushPromises();
  return mounted;
}

/** Waits for a click's navigation; the first visit to a code-split page loads its module first. */
async function arrive(router: { currentRoute: { value: { path: string } } }, path: string) {
  await vi.waitFor(() => expect(router.currentRoute.value.path).toBe(path));
  await flushPromises();
}

/** The sidebar's section list; the phone's bottom bar carries the same name. */
const sidebar = () => wrapper!.get('aside').get('[aria-label="Events sections"]');

beforeEach(() => {
  localStorage.clear();
  useUiLocale().setUiLocale('en');
});
afterEach(() => {
  wrapper?.unmount();
  wrapper = undefined;
  document.body.innerHTML = '';
  vi.unstubAllGlobals();
  localStorage.clear();
});

describe('Ocharo frame in Events', () => {
  it.each([390, 1440])('renders the bar with one main landmark and the sections at %ipx', async (width) => {
    await renderAt(eventsPath, width);

    const main = wrapper!.get('main');
    expect(wrapper!.findAll('main')).toHaveLength(1);
    expect(wrapper!.get(`a[href="#${main.attributes('id')}"]`).text()).toBe('Skip to content');
    expect(main.findAll('h1')).toHaveLength(1);
    // The sections are a sidebar from a tablet up and a bottom bar on a phone.
    expect(wrapper!.find('aside').exists()).toBe(width >= 768);
    expect(wrapper!.find('[data-app-frame-phone-nav]').exists()).toBe(width < 768);
    const header = wrapper!.get('header[role="banner"]');
    const switcher = header.get('button[aria-label="Ocharo apps — Events"]');
    // The bar names the app and the organization where it has room; a phone's bar has room for the mark only.
    if (width >= 768) {
      expect(switcher.text()).toContain('Events');
      expect(header.text()).toContain('Ocharo Atelier');
    }
    // The version badge stands beside the brand; a phone's bar has room for the mark only.
    const badge = wrapper!.find('[data-version-badge]');
    expect(badge.exists()).toBe(width >= 768);
    if (badge.exists()) expect(badge.text()).toBe(__APP_VERSION__);
    expect(header.findAll('button').at(-1)!.attributes()).toMatchObject({
      'aria-label': 'Account menu for Dilnoza Karimova',
      'aria-haspopup': 'menu',
    });
    expect(wrapper!.findAll('[aria-label="Events sections"]')).toHaveLength(1);
  });

  it('names the place in the trail', async () => {
    await renderAt(eventsPath);

    expect(wrapper!.get('nav[aria-label="You are here"]').text()).toBe('Feed');
    expect(document.title).toBe('Feed · Ocharo Events');
  });

  it('reads the API version only when the account menu opens, then offers sign-out and the versions, and no language choice', async () => {
    await renderAt(eventsPath);
    expect(fetch).not.toHaveBeenCalledWith('/api/system/status', expect.anything());

    await wrapper!.get('header button[aria-haspopup="menu"]').trigger('click');
    await flushPromises();

    const menu = new DOMWrapper(document.querySelector<HTMLElement>('[role="menu"]')!);
    const versions = menu
      .findAll('[data-account-menu-version]')
      .map((entry) => [...entry.element.children].map((part) => part.textContent?.trim()));
    expect(versions).toEqual([
      ['Events', __APP_VERSION__],
      ['API', `${ApiVersion.split('+')[0]} · 8f2c1d9`],
    ]);
    expect(fetch).toHaveBeenCalledWith('/api/system/status', expect.objectContaining({ credentials: 'omit' }));
    expect(menu.find('[data-account-menu-appearance]').exists()).toBe(true);
    expect(menu.find('[data-account-menu-languages]').exists()).toBe(false);
    expect(menu.find('[data-account-menu-sign-out]').exists()).toBe(true);
    expect(menu.find('[data-account-menu-identity]').text()).toContain('Dilnoza Karimova');
  });

  it('stays in English: a language change is ignored, nothing is stored and the frame keeps its English names', async () => {
    await renderAt(eventsPath);

    useUiLocale().setUiLocale('ru');
    await flushPromises();

    expect(localStorage.getItem('ocharo.operator.locale')).toBeNull();
    expect(document.documentElement.lang).toBe('en');
    expect(wrapper!.find('button[aria-label="Ocharo apps — Events"]').exists()).toBe(true);
    expect(wrapper!.get('main h1').text()).toBe('Feed');
  });

  it.each([
    [390, true],
    [1440, false],
  ])('lists Events among the Ocharo apps and opens them from the app button at %ipx', async (width, fullScreen) => {
    await renderAt(eventsPath, width);

    await wrapper!.get('header button[aria-label="Ocharo apps — Events"]').trigger('click');
    await flushPromises();

    expect(document.body.textContent).toContain('Ocharo apps');
    const current = document.querySelector('[aria-current="true"]');
    expect(current?.textContent).toContain('Events');
    expect(current?.textContent).toContain('Trade events and deadlines');
    // The current app is the open entry, not a link; its address is the Events build's own.
    expect(current?.tagName).toBe('BUTTON');
    expect(document.querySelector('a[href="https://localhost:8292/"]')).toBeNull();
    expect(document.querySelector('a[href="https://localhost:8264/"]')?.textContent).toContain('Desk');
    expect(document.querySelector('a[href="https://localhost:8288/"]')?.textContent).toContain('Brand');
    expect(document.querySelector('a[target="_blank"][href="https://localhost:8284/atlas/"]')).not.toBeNull();
    expect(document.querySelector('[data-phone-layout="fullscreen"]') !== null).toBe(fullScreen);
  });

  it('says "Ocharo" under the app name for a session organization with no name', async () => {
    const mounted = await mountEvents(eventsPath, {
      identitySession: {
        ...EditorSession,
        organization: { id: OrganizationId, name: null, role: 'editor', slug: null },
      },
    });
    wrapper = mounted.wrapper;
    await flushPromises();

    const switcher = wrapper.get('header button[aria-label="Ocharo apps — Events"]');
    expect(switcher.text()).toContain('Ocharo');
    expect(switcher.text()).not.toContain('Ocharo Atelier');
  });
});

describe('Events sections', () => {
  it('lists the four places in the sidebar for an admin, Reminders and Telegram delivery under Settings', async () => {
    await renderAt(eventsPath, 1440, AdminSession);

    const links = sidebar().findAll('a');
    expect(links.map((link) => link.text())).toEqual(['Feed', 'Calendar', 'Reminders', 'Telegram delivery']);
    expect(links.map((link) => link.attributes('href'))).toEqual([
      eventsPath,
      placePath('calendar'),
      placePath('reminders'),
      placePath('telegram-delivery'),
    ]);
    // Settings heads the last two; the first two sit under no heading, and there is no Deadlines place.
    expect(sidebar().text()).toContain('Settings');
    expect(sidebar().text().indexOf('Settings')).toBeGreaterThan(sidebar().text().indexOf('Calendar'));
    expect(sidebar().text().indexOf('Settings')).toBeLessThan(sidebar().text().indexOf('Reminders'));
    expect(sidebar().text()).not.toContain('Deadlines');
  });

  it('offers a member without events:admin three places, none of them Telegram delivery', async () => {
    await renderAt(eventsPath, 1440, EditorSession);

    expect(
      sidebar()
        .findAll('a')
        .map((link) => link.text()),
    ).toEqual(['Feed', 'Calendar', 'Reminders']);
    expect(
      sidebar()
        .find(`a[href="${placePath('telegram-delivery')}"]`)
        .exists(),
    ).toBe(false);
  });

  it('marks the open section, follows the route, and names the place in the trail and the document title', async () => {
    const { router } = await renderAt(eventsPath, 1440, AdminSession);
    expect(sidebar().get('[aria-current="page"]').text()).toBe('Feed');

    await sidebar()
      .get(`a[href="${placePath('calendar')}"]`)
      .trigger('click');
    await arrive(router, placePath('calendar'));
    expect(sidebar().get('[aria-current="page"]').text()).toBe('Calendar');
    expect(wrapper!.get('nav[aria-label="You are here"]').text()).toBe('Calendar');
    expect(document.title).toBe('Calendar · Ocharo Events');
    expect(wrapper!.get('main h1').text()).toBe('Calendar');

    await sidebar()
      .get(`a[href="${placePath('reminders')}"]`)
      .trigger('click');
    await arrive(router, placePath('reminders'));
    expect(sidebar().get('[aria-current="page"]').text()).toBe('Reminders');
    expect(document.title).toBe('Reminders · Ocharo Events');

    await sidebar()
      .get(`a[href="${placePath('telegram-delivery')}"]`)
      .trigger('click');
    await arrive(router, placePath('telegram-delivery'));
    expect(sidebar().get('[aria-current="page"]').text()).toBe('Telegram delivery');
    expect(wrapper!.get('nav[aria-label="You are here"]').text()).toBe('Telegram delivery');
    expect(document.title).toBe('Telegram delivery · Ocharo Events');
  });

  it('keeps Feed marked while one event of it is open', async () => {
    await renderAt(`${eventsPath}/22222222-2222-4222-8222-222222222222`, 1440, EditorSession);

    expect(sidebar().get('[aria-current="page"]').text()).toBe('Feed');
  });

  it('marks no section on an address no section owns', async () => {
    await renderAt('/no-such-place', 1440, EditorSession);
    expect(sidebar().find('[aria-current="page"]').exists()).toBe(false);
  });

  it('keeps the sidebar rail choice this browser stored, and stores a change under the same key', async () => {
    localStorage.setItem(SidebarCollapsedKey, 'true');
    await renderAt(eventsPath, 1440);

    await wrapper!.get('button[aria-label="Expand sidebar"]').trigger('click');

    expect(wrapper!.find('button[aria-label="Collapse sidebar"]').exists()).toBe(true);
    expect(localStorage.getItem(SidebarCollapsedKey)).toBe('false');
  });

  it('swaps the sidebar for a bottom bar of all four places, with no More, on a phone', async () => {
    const { router } = await renderAt(placePath('reminders'), 390, AdminSession);

    expect(wrapper!.find('aside').exists()).toBe(false);
    const bar = wrapper!.get('[aria-label="Events sections"]');
    expect(bar.findAll('a').map((link) => link.text())).toEqual(['Feed', 'Calendar', 'Reminders', 'Telegram delivery']);
    expect(bar.get('[aria-current="page"]').text()).toBe('Reminders');
    expect(bar.find('button').exists()).toBe(false);
    expect(document.querySelector('[role="dialog"]')).toBeNull();

    await bar.get(`a[href="${placePath('telegram-delivery')}"]`).trigger('click');
    await arrive(router, placePath('telegram-delivery'));
    expect(wrapper!.get('[aria-label="Events sections"]').get('[aria-current="page"]').text()).toBe(
      'Telegram delivery',
    );
  });

  it('offers a member three places on a phone', async () => {
    await renderAt(eventsPath, 390, EditorSession);

    const bar = wrapper!.get('[aria-label="Events sections"]');
    expect(bar.findAll('a').map((link) => link.text())).toEqual(['Feed', 'Calendar', 'Reminders']);
  });

  it('offers no section to a person in no organization', async () => {
    await renderAt('/', 1440, { ...EditorSession, organization: null });

    expect(sidebar().findAll('a')).toHaveLength(0);
  });
});

describe('Sources updated', () => {
  const sourcesFresh = { lastReceivedAtUtc: new Date(Date.now() - 3 * 3_600_000).toISOString(), failingSources: 0 };
  const sourcesStale = { lastReceivedAtUtc: new Date(Date.now() - 50 * 3_600_000).toISOString(), failingSources: 1 };
  const staleTitle = 'Sources updated 2 days ago · No new records in over 24 hours. · Sources failing: 1';
  const footer = () => wrapper!.get('[data-app-frame-nav-footer]');
  /** The copy beside the feed's title, which only a phone shows. */
  const beside = () => wrapper!.get('main header').find('[data-test="sources-freshness-title"]');
  const freshnessReads = () => vi.mocked(fetch).mock.calls.filter(([input]) => String(input).includes('/freshness'));

  async function renderWith(path: string, width: number, freshness: Record<string, unknown>, session = EditorSession) {
    const mounted = await mountEvents(path, { width, identitySession: session, freshness });
    wrapper = mounted.wrapper;
    await flushPromises();
    return mounted;
  }

  it('stands at the foot of the open sidebar, on every place, with its text', async () => {
    const { router } = await renderWith(eventsPath, 1440, sourcesFresh);

    expect(footer().get('[data-test="sources-freshness"]').text()).toBe('Sources updated 3 hours ago');
    expect(footer().get('[data-test="sources-freshness"]').attributes('data-state')).toBe('fresh');

    await router.push(placePath('calendar'));
    await flushPromises();
    expect(footer().get('[data-test="sources-freshness"]').text()).toBe('Sources updated 3 hours ago');
  });

  it('keeps its warning tone in the sidebar', async () => {
    await renderWith(eventsPath, 1440, sourcesStale);

    const line = footer().get('[data-test="sources-freshness"]');
    expect(line.attributes('data-state')).toBe('warning');
    expect(line.classes()).toContain('text-warning');
    expect(line.text()).toContain('Sources failing: 1');
  });

  it('is only an icon with the full text as its tooltip in the icon rail', async () => {
    localStorage.setItem(SidebarCollapsedKey, 'true');
    await renderWith(eventsPath, 1440, sourcesStale);

    const line = footer().get('[data-test="sources-freshness"]');
    expect(line.attributes('data-compact')).toBeDefined();
    expect(line.text()).toBe('');
    expect(line.get('[role="img"]').attributes('title')).toBe(staleTitle);
  });

  it('stands beside the feed title on a phone, as its icon with the full text, in its warning tone', async () => {
    await renderWith(eventsPath, 390, sourcesStale);

    // A phone has no sidebar, so no foot to stand at.
    expect(wrapper!.find('aside').exists()).toBe(false);
    expect(wrapper!.find('[data-app-frame-nav-footer]').exists()).toBe(false);
    const header = wrapper!.get('main header');
    expect(header.get('h1').text()).toBe('Feed');
    expect(header.get('h1').element.nextElementSibling).toBe(beside().element);
    expect(beside().attributes('data-compact')).toBeDefined();
    expect(beside().attributes('data-state')).toBe('warning');
    expect(beside().classes()).toContain('text-warning');
    expect(beside().attributes('role')).toBe('status');
    expect(beside().text()).toBe('');
    expect(beside().get('[role="img"]').attributes('aria-label')).toBe(staleTitle);
    expect(beside().get('[role="img"]').attributes('title')).toBe(staleTitle);
  });

  it('keeps the title-row copy for a phone only: hidden from md up, where the sidebar carries it', async () => {
    await renderWith(eventsPath, 390, sourcesFresh);

    expect(beside().classes()).toContain('md:hidden');
    expect(beside().get('[role="img"]').attributes('aria-label')).toBe('Sources updated 3 hours ago');
    expect(beside().attributes('data-state')).toBe('fresh');
  });

  it('shows no title-row copy on the other places, and none before the read answers or when it fails', async () => {
    await renderWith(placePath('calendar'), 390, sourcesFresh);
    expect(wrapper!.get('main').find('[data-test^="sources-freshness"]').exists()).toBe(false);
    wrapper!.unmount();

    const failed = await mountEvents(eventsPath, { width: 390 });
    wrapper = failed.wrapper;
    await flushPromises();
    expect(wrapper.get('main header').find('[data-test="sources-freshness-title"]').exists()).toBe(false);
    expect(wrapper.get('main header').element.children).toHaveLength(1);
  });

  it('reads once for both copies, and once more on a return to the tab, never once for each', async () => {
    await renderWith(eventsPath, 1440, sourcesFresh);

    // The sidebar's foot and the title row both show it at this width in the DOM; one read feeds both.
    expect(footer().find('[data-test="sources-freshness"]').exists()).toBe(true);
    expect(beside().exists()).toBe(true);
    expect(freshnessReads()).toHaveLength(1);

    window.dispatchEvent(new Event('focus'));
    await flushPromises();
    expect(freshnessReads()).toHaveLength(2);
    expect(beside().get('[role="img"]').attributes('aria-label')).toBe('Sources updated 3 hours ago');
  });

  it('reads nothing for a person without events:read, and shows no copy', async () => {
    const { wrapper: mounted } = await mountEvents(eventsPath, {
      identitySession: { ...EditorSession, permissions: ['events:track'] },
      freshness: sourcesFresh,
    });
    wrapper = mounted;
    await flushPromises();

    expect(wrapper.find('[data-test^="sources-freshness"]').exists()).toBe(false);
    expect(freshnessReads()).toHaveLength(0);
  });
});
