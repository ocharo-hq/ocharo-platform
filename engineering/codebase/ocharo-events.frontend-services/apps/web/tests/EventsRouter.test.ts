import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, type VueWrapper } from '@vue/test-utils';

import { useUiLocale } from '../src/application/shared/UseUiLocale';
import { EditorSession, OrganizationId, mountEvents } from './helpers/EventsApp';

let wrapper: VueWrapper | undefined;
const eventsPath = `/organizations/${OrganizationId}/events`;
const OtherOrganization = '11111111-1111-4111-8111-111111111111';

async function renderAt(path: string, identitySession?: Record<string, unknown>) {
  const mounted = await mountEvents(path, identitySession ? { identitySession } : {});
  wrapper = mounted.wrapper;
  await vi.waitFor(() => expect(mounted.router.currentRoute.value.matched.length).toBeGreaterThan(0));
  await flushPromises();
  return mounted;
}
const requested = () => vi.mocked(fetch).mock.calls.map(([input]) => String(input));

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
});

describe('Events routes', () => {
  it("sends / to the events of the session's organization", async () => {
    const { router } = await renderAt('/');

    await vi.waitFor(() => expect(router.currentRoute.value.path).toBe(eventsPath));
    await flushPromises();
    expect(wrapper!.get('main h1').text()).toBe('Events');
    expect(document.title).toBe('Events · Ocharo Events');
    expect(requested().some((url) => url.startsWith(`/api/organizations/${OrganizationId}/events?`))).toBe(true);
    expect(requested().some((url) => url.startsWith(`/api/organizations/${OrganizationId}/events/summary?`))).toBe(
      true,
    );
  });

  it('opens one event address inside the same page', async () => {
    const eventId = '22222222-2222-4222-8222-222222222222';
    const { router } = await renderAt(`${eventsPath}/${eventId}`);

    expect(router.currentRoute.value.path).toBe(`${eventsPath}/${eventId}`);
    expect(wrapper!.get('main').text()).toContain('Events');
    expect(wrapper!.find('main #not-found-heading').exists()).toBe(false);
  });

  it('answers not found for another organization, as the API does, and requests none of its events', async () => {
    await renderAt(`/organizations/${OtherOrganization}/events`);

    expect(wrapper!.get('main #not-found-heading').text()).toBe('Page not found.');
    expect(requested().some((url) => url.includes(OtherOrganization))).toBe(false);
  });

  it('answers not found to a person without events:read', async () => {
    await renderAt(eventsPath, { ...EditorSession, permissions: ['events:track'] });

    expect(wrapper!.find('main #not-found-heading').exists()).toBe(true);
    expect(requested().some((url) => url.includes(`/api/organizations/${OrganizationId}/events`))).toBe(false);
  });

  it('keeps a person in no organization on a not-found page at /', async () => {
    const { router } = await renderAt('/', { ...EditorSession, organization: null });

    expect(router.currentRoute.value.path).toBe('/');
    expect(wrapper!.find('main #not-found-heading').exists()).toBe(true);
  });

  it('titles an unknown address "Page not found · Ocharo Events"', async () => {
    await renderAt('/nowhere');

    expect(wrapper!.find('main #not-found-heading').exists()).toBe(true);
    expect(document.title).toBe('Page not found · Ocharo Events');
  });
});
