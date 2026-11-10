import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { DOMWrapper, flushPromises, type VueWrapper } from '@vue/test-utils';

import { useUiLocale, UiLocaleStorageKey } from '../src/application/shared/UseUiLocale';
import { ApiVersion, EditorSession, OrganizationId, mountEvents } from './helpers/EventsApp';

let wrapper: VueWrapper | undefined;
const eventsPath = `/organizations/${OrganizationId}/events`;

async function renderAt(path: string, width = 1440) {
  const mounted = await mountEvents(path, { width });
  wrapper = mounted.wrapper;
  await flushPromises();
  return mounted;
}

beforeEach(() => {
  localStorage.clear();
  useUiLocale().setUiLocale('en');
});
afterEach(() => {
  wrapper?.unmount();
  wrapper = undefined;
  document.body.innerHTML = '';
  vi.unstubAllGlobals();
  useUiLocale().setUiLocale('uz');
  localStorage.clear();
});

describe('Ocharo frame in Events', () => {
  it.each([390, 1440])('renders the bar with one main landmark and no sidebar at %ipx', async (width) => {
    await renderAt(eventsPath, width);

    const main = wrapper!.get('main');
    expect(wrapper!.findAll('main')).toHaveLength(1);
    expect(wrapper!.get(`a[href="#${main.attributes('id')}"]`).text()).toBe('Skip to content');
    expect(main.findAll('h1')).toHaveLength(1);
    expect(wrapper!.find('aside').exists()).toBe(false);
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
    expect(wrapper!.find('nav[aria-label="Navigation"]').exists()).toBe(false);
  });

  it('names the place in the trail', async () => {
    await renderAt(eventsPath);

    expect(wrapper!.get('nav[aria-label="You are here"]').text()).toBe('Events');
    expect(document.title).toBe('Events · Ocharo Events');
  });

  it('reads the API version only when the account menu opens, then offers sign-out, languages and the versions', async () => {
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
    expect(menu.find('[data-account-menu-languages]').exists()).toBe(true);
    expect(menu.find('[data-account-menu-sign-out]').exists()).toBe(true);
    expect(menu.find('[data-account-menu-identity]').text()).toContain('Dilnoza Karimova');
  });

  it('keeps the language choice under the shared operator key and renames the frame in it', async () => {
    await renderAt(eventsPath);

    useUiLocale().setUiLocale('ru');
    await flushPromises();

    expect(localStorage.getItem(UiLocaleStorageKey)).toBe('ru');
    expect(UiLocaleStorageKey).toBe('ocharo.operator.locale');
    expect(wrapper!.find('button[aria-label="Приложения Ocharo — Мероприятия"]').exists()).toBe(true);
    expect(wrapper!.get('main h1').text()).toBe('Мероприятия');
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
