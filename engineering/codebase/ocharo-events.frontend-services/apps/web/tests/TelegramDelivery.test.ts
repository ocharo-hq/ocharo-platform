import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { DigestsPanel } from '@wow-two-beta/fe-events/admin';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import { eventStatuses } from '@/domain/events/Events';
import {
  deliveryApiPrefix,
  deliveryPath,
  eventLinkTemplate,
  ocharoDigestDefaults,
  OcharoEventsCatalog,
  ocharoWorkflow,
} from '@/domain/delivery/TelegramDelivery';
import { createDigestsClient } from '@/integration/delivery/DigestsClient';
import TelegramDeliveryPage from '@/presentation/delivery/pages/TelegramDeliveryPage.vue';
import { AdminSession, EditorSession, OrganizationId, mountEvents } from './helpers/EventsApp';
import { eventsRouter } from './helpers/EventsRouter';

const digestsPath = `/api/organizations/${OrganizationId}/delivery/api/events/v1/catalogs/ocharo-fashion/digests`;
let wrapper: VueWrapper | undefined;

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

describe('Telegram delivery defaults', () => {
  it("starts a digest from Ocharo's weekly Monday 09:00 Tashkent default, in Uzbek, to every member", () => {
    expect(ocharoDigestDefaults()).toEqual({
      name: 'Ocharo tadbirlari',
      enabled: true,
      channel: 'telegram',
      schedule: { days: ['monday'], times: ['09:00'], timeZone: 'Asia/Tashkent' },
      selection: {
        newSinceLastRun: true,
        startingWithinDays: 30,
        deadlineWithinDays: 14,
        excludeStages: ['skipped', 'rejected'],
      },
      recipients: [{ role: 'member' }],
      language: 'uz-Latn',
      itemCap: 20,
    });
    expect(OcharoEventsCatalog).toBe('ocharo-fashion');
  });

  it("offers the digest editor Ocharo's eight stages in three languages, an untracked event reading as candidate", () => {
    const workflow = ocharoWorkflow();
    expect(workflow.stages.map((stage) => stage.key)).toEqual([...eventStatuses]);
    expect(workflow.defaultStage).toBe('candidate');
    expect(workflow.stages.find((stage) => stage.key === 'skipped')?.label).toEqual({
      en: 'Skipped',
      uz: 'O‘tkazib yuborilgan',
      ru: 'Пропущено',
    });
  });

  it('links each event to its page in this app, and only from an http(s) origin', () => {
    expect(eventLinkTemplate('https://events.example.test', OrganizationId.toUpperCase())).toBe(
      `https://events.example.test/organizations/${OrganizationId}/events/{id}`,
    );
    expect(eventLinkTemplate('null', OrganizationId)).toBeNull();
    expect(eventLinkTemplate('https://events.example.test', 'not-an-organization')).toBeNull();
    expect(deliveryPath(OrganizationId)).toBe(`/organizations/${OrganizationId}/telegram-delivery`);
    expect(() => deliveryPath('../admin')).toThrow();
    expect(deliveryApiPrefix(OrganizationId)).toBe(`/api/organizations/${OrganizationId}/delivery`);
  });

  it("reaches Events' digests through this app's mirror for the organization", async () => {
    const fetch = vi.fn((input: RequestInfo | URL) => {
      void input;
      return Promise.resolve(Response.json({ data: [] }));
    });
    vi.stubGlobal('fetch', fetch);
    const client = createDigestsClient(OrganizationId);

    await client.listDigests('ocharo-fashion');

    const [input] = fetch.mock.calls[0]!;
    const request = input instanceof Request ? input : new Request(new URL(String(input), 'https://localhost'));
    expect(new URL(request.url, 'https://localhost').pathname).toBe(digestsPath);
  });
});

describe('Telegram delivery page', () => {
  it("embeds Events' digest editor for the catalog with Ocharo's default, its link and the Telegram channel", async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(Response.json({ data: [] }))),
    );
    wrapper = mount(TelegramDeliveryPage, {
      props: { organizationId: OrganizationId, subject: 'owner-sub' },
      global: { plugins: [await eventsRouter(`/organizations/${OrganizationId}/telegram-delivery`)] },
      attachTo: document.body,
    });
    await flushPromises();

    const panel = wrapper.getComponent(DigestsPanel);
    expect(panel.props('catalog')).toBe('ocharo-fashion');
    expect(panel.props('locale')).toBe('en');
    expect(panel.props('channels')).toEqual(['telegram']);
    expect(panel.props('linkTemplate')).toBe(`${location.origin}/organizations/${OrganizationId}/events/{id}`);
    expect(panel.props('defaults')).toMatchObject({
      schedule: { days: ['monday'], times: ['09:00'], timeZone: 'Asia/Tashkent' },
      selection: { excludeStages: ['skipped', 'rejected'] },
      recipients: [{ role: 'member' }],
      language: 'uz-Latn',
    });
    expect(panel.props('workflow')).toEqual(ocharoWorkflow());
    expect(wrapper.get('h1').text()).toBe('Telegram delivery');
    expect(wrapper.get('[data-test="own-subject"]').text()).toBe('Your account id: owner-sub');
    expect(wrapper.text()).not.toContain('All events');
  });
});

describe('Telegram delivery: no deadline-reminder offer', () => {
  it('lists the digests only, never offering or creating a "Deadline reminders" digest', async () => {
    const requests: string[] = [];
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
        const request =
          input instanceof Request ? input : new Request(new URL(String(input), 'https://localhost'), init);
        requests.push(request.method);
        return Response.json({ data: [] });
      }),
    );
    wrapper = mount(TelegramDeliveryPage, {
      props: { organizationId: OrganizationId, subject: 'owner-sub' },
      global: { plugins: [await eventsRouter(`/organizations/${OrganizationId}/telegram-delivery`)] },
      attachTo: document.body,
    });
    await vi.waitFor(() => expect(requests.length).toBeGreaterThan(0));
    await flushPromises();

    expect(wrapper.find('[data-test="deadline-reminders"]').exists()).toBe(false);
    expect(wrapper.text()).not.toContain('Deadline reminders');
    expect(wrapper.text()).not.toContain('Add deadline reminders');
    expect(requests.every((method) => method === 'GET')).toBe(true);
    // The weekly default stays the editor's starting digest.
    expect(ocharoDigestDefaults().name).toBe('Ocharo tadbirlari');
  });
});

describe('Telegram delivery route', () => {
  const requested = () => vi.mocked(fetch).mock.calls.map(([input]) => String(input));

  const deliveryLink = () => wrapper!.find(`aside a[href="/organizations/${OrganizationId}/telegram-delivery"]`);

  it('opens the settings for an admin, links them from the sidebar and lists the digests', async () => {
    const mounted = await mountEvents(`/organizations/${OrganizationId}/events`, { identitySession: AdminSession });
    wrapper = mounted.wrapper;
    await vi.waitFor(() => expect(deliveryLink().exists()).toBe(true));
    expect(deliveryLink().text()).toBe('Telegram delivery');

    await mounted.router.push(`/organizations/${OrganizationId}/telegram-delivery`);
    await vi.waitFor(() => expect(wrapper!.find('main h1').text()).toBe('Telegram delivery'));
    await flushPromises();
    expect(document.title).toBe('Telegram delivery · Ocharo Events');
    expect(requested().some((url) => url.includes(digestsPath))).toBe(true);
  });

  it('tells a member without events:admin that admins set it, and requests no digest', async () => {
    const mounted = await mountEvents(`/organizations/${OrganizationId}/events`, { identitySession: EditorSession });
    wrapper = mounted.wrapper;
    await flushPromises();
    expect(wrapper.find('aside').exists()).toBe(true);
    expect(deliveryLink().exists()).toBe(false);

    await mounted.router.push(`/organizations/${OrganizationId}/telegram-delivery`);
    await vi.waitFor(() => expect(wrapper!.find('#delivery-refused-heading').exists()).toBe(true));
    expect(wrapper.get('#delivery-refused-heading').text()).toBe(
      "Only the organization's admins set its Telegram delivery.",
    );
    expect(requested().some((url) => url.includes('/delivery/'))).toBe(false);
  });

  it("answers not found for another organization's delivery", async () => {
    const other = '11111111-1111-4111-8111-111111111111';
    const mounted = await mountEvents(`/organizations/${other}/telegram-delivery`, { identitySession: AdminSession });
    wrapper = mounted.wrapper;
    await vi.waitFor(() => expect(wrapper!.find('main #not-found-heading').exists()).toBe(true));
    expect(requested().some((url) => url.includes(other))).toBe(false);
  });
});
