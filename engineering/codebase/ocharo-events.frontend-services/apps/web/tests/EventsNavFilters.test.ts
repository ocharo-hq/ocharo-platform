import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, type VueWrapper } from '@vue/test-utils';

import { useUiLocale } from '../src/application/shared/UseUiLocale';
import { AdminSession, OrganizationId, mountEvents } from './helpers/EventsApp';

let wrapper: VueWrapper | undefined;
const placePath = (place: string) => `/organizations/${OrganizationId}/${place}`;
/** The filter keys both pages share, as one address writes them (the keys' order, a list as a repeated key). */
const shared = 'q=silk+road&stages=candidate&kinds=runway&kinds=market&cities=tashkent&sources=afisha&regions=national';
/** An address holding the shared keys and others the links must leave behind: the feed's own `when` and `sort`, and a stray `tab`. */
const held = `${shared}&when=past&sort=added&tab=x`;

async function renderAt(path: string, width = 1440) {
  const mounted = await mountEvents(path, { width, identitySession: AdminSession });
  wrapper = mounted.wrapper;
  await flushPromises();
  return mounted;
}
/** The sidebar's links by their text, with the address each points at. */
const sidebarLinks = () =>
  Object.fromEntries(
    wrapper!
      .get('aside')
      .get('[aria-label="Events sections"]')
      .findAll('a')
      .map((link) => [link.text(), link.attributes('href')]),
  );
/** The phone bar's links by their text. */
const phoneLinks = () =>
  Object.fromEntries(
    wrapper!
      .get('[aria-label="Events sections"]')
      .findAll('a')
      .map((link) => [link.text(), link.attributes('href')]),
  );

beforeEach(() => {
  useUiLocale().setUiLocale('en');
});
afterEach(() => {
  wrapper?.unmount();
  wrapper = undefined;
  document.body.innerHTML = '';
  vi.unstubAllGlobals();
});

describe('the links between the Feed and the Calendar', () => {
  it('carry the shared filter keys of the open address in the sidebar, and no other key', async () => {
    await renderAt(`${placePath('calendar')}?${held}`);

    expect(sidebarLinks()).toEqual({
      Feed: `${placePath('events')}?${shared}`,
      Calendar: `${placePath('calendar')}?${shared}`,
      Reminders: placePath('reminders'),
      'Telegram delivery': placePath('telegram-delivery'),
    });
  });

  it('carry them on the phone bar too', async () => {
    await renderAt(`${placePath('events')}?${held}`, 390);

    expect(phoneLinks()).toEqual({
      Feed: `${placePath('events')}?${shared}`,
      Calendar: `${placePath('calendar')}?${shared}`,
      Reminders: placePath('reminders'),
      Telegram: placePath('telegram-delivery'),
    });
  });

  it('are clean while the address holds no filter, and leave Settings clean when it does', async () => {
    await renderAt(`${placePath('calendar')}?when=past&tab=x`);

    expect(sidebarLinks()).toEqual({
      Feed: placePath('events'),
      Calendar: placePath('calendar'),
      Reminders: placePath('reminders'),
      'Telegram delivery': placePath('telegram-delivery'),
    });
  });

  it('bring a filter set on the Calendar to the Feed and back, the feed’s own keys left behind', async () => {
    const { router } = await renderAt(`${placePath('calendar')}?${held}`);

    await wrapper!
      .get('aside')
      .get(`a[href="${placePath('events')}?${shared}"]`)
      .trigger('click');
    await vi.waitFor(() => expect(router.currentRoute.value.path).toBe(placePath('events')));
    await flushPromises();
    const { query } = router.currentRoute.value;
    expect(Object.keys(query).sort()).toEqual(['cities', 'kinds', 'q', 'regions', 'sources', 'stages']);
    expect(query.kinds).toEqual(['runway', 'market']);
    expect(query.q).toBe('silk road');
    // The feed shows the choice it carried: the kind pill is pressed.
    const pressed = wrapper!
      .findAll('button[aria-pressed="true"]')
      .map((button) => button.text())
      .join(' ');
    expect(pressed).toContain('Runway show');
    expect(pressed).toContain('Market');

    await wrapper!
      .get('aside')
      .get(`a[href="${placePath('calendar')}?${shared}"]`)
      .trigger('click');
    await vi.waitFor(() => expect(router.currentRoute.value.path).toBe(placePath('calendar')));
    await flushPromises();
    expect(router.currentRoute.value.query.kinds).toEqual(['runway', 'market']);
    expect(router.currentRoute.value.query.regions).toBe('national');
  });
});
