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
});

describe('Events routes', () => {
  it("sends / to the events of the session's organization", async () => {
    const { router } = await renderAt('/');

    await vi.waitFor(() => expect(router.currentRoute.value.path).toBe(eventsPath));
    await flushPromises();
    expect(wrapper!.get('main h1').text()).toBe('Feed');
    expect(document.title).toBe('Feed · Ocharo Events');
    const mirror = `/api/organizations/${OrganizationId}/events-api/api/events/v1/catalogs/ocharo-fashion`;
    expect(requested().some((url) => url.startsWith(`${mirror}/tracking?`))).toBe(true);
    expect(requested().some((url) => url.startsWith(`${mirror}/tracking/summary?`))).toBe(true);
  });

  it('opens one event address inside the same page', async () => {
    const eventId = '22222222-2222-4222-8222-222222222222';
    const { router } = await renderAt(`${eventsPath}/${eventId}`);

    expect(router.currentRoute.value.path).toBe(`${eventsPath}/${eventId}`);
    expect(wrapper!.get('main h1').text()).toBe('Feed');
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

  it("keeps the feed's section on the events route and on one event, and none on the other routes of the frame", async () => {
    const { router } = await renderAt(eventsPath);
    expect(router.currentRoute.value.meta.section).toBe('feed');

    await router.push(`${eventsPath}/22222222-2222-4222-8222-222222222222`);
    expect(router.currentRoute.value.meta.section).toBe('feed');
    await router.push('/nowhere');
    expect(router.currentRoute.value.meta.section).toBeNull();
  });

  it('has no Deadlines place: its old address is not found', async () => {
    await renderAt(`/organizations/${OrganizationId}/deadlines`);

    expect(wrapper!.find('main #not-found-heading').exists()).toBe(true);
  });

  it('titles an unknown address "Page not found · Ocharo Events"', async () => {
    await renderAt('/nowhere');

    expect(wrapper!.find('main #not-found-heading').exists()).toBe(true);
    expect(document.title).toBe('Page not found · Ocharo Events');
  });
});

describe.each([
  ['calendar', 'Calendar'],
  ['reminders', 'Reminders'],
] as const)('the %s place', (segment, title) => {
  const path = `/organizations/${OrganizationId}/${segment}`;

  it("opens for the session's organization under its section, with its title", async () => {
    const { router } = await renderAt(path);

    expect(router.currentRoute.value.meta).toMatchObject({ section: segment, label: title });
    expect(wrapper!.get('main h1').text()).toBe(title);
    expect(wrapper!.find('main #not-found-heading').exists()).toBe(false);
    expect(document.title).toBe(`${title} · Ocharo Events`);
  });

  it('answers not found for another organization, as the API does, and requests none of its events', async () => {
    await renderAt(`/organizations/${OtherOrganization}/${segment}`);

    expect(wrapper!.get('main #not-found-heading').text()).toBe('Page not found.');
    expect(requested().some((url) => url.includes(OtherOrganization))).toBe(false);
  });

  it('answers not found to a person without events:read, and requests none of the organization events', async () => {
    await renderAt(path, { ...EditorSession, permissions: ['events:track'] });

    expect(wrapper!.find('main #not-found-heading').exists()).toBe(true);
    expect(requested().some((url) => url.includes(`/api/organizations/${OrganizationId}/events`))).toBe(false);
  });
});

describe('the calendar place', () => {
  it('reads the tracking feed of the visible month through the mirror', async () => {
    await renderAt(`/organizations/${OrganizationId}/calendar`);

    const mirror = `/api/organizations/${OrganizationId}/events-api/api/events/v1/catalogs/ocharo-fashion`;
    const reads = requested().filter((url) => url.startsWith(`${mirror}/tracking?`));
    expect(reads).toHaveLength(1);
    expect(reads[0]).toContain('includeUndated=false');
  });
});
