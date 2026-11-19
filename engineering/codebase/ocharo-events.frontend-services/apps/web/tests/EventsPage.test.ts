import { eventsSelect, chooseEventsValue } from './helpers/EventsSelect';
import { eventsRouter } from './helpers/EventsRouter';
import { computed, ref } from 'vue';
import { flushPromises, mount, type DOMWrapper, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiFailureFactory } from '@wow-two-beta/fe/foundation/http';
import { DefaultRetryPolicy } from '@wow-two-beta/fe/foundation/resilience';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import { createQueryClient, queryPlugin } from '@wow-two-beta/fe/query';

import { feedbackBus, resolveConfirmation } from '@/application/common/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { EventsAccess } from '@/application/events/UseEventsAccess';
import type { EventsWorkspace } from '@/application/events/UseEventsAccess';
import { trackingInput, type EventsLocation, type ShowcaseEvent } from '@/domain/events/Events';
import EventsPage from '@/presentation/events/pages/EventsPage.vue';

const api = vi.hoisted(() => ({
  listEvents: vi.fn(),
  summary: vi.fn(),
  createEvent: vi.fn(),
  getEvent: vi.fn(),
  updateTracking: vi.fn(),
}));
vi.mock('@/integration/events/EventsApi', () => ({ createEventsApi: () => api }));

const ORG = '11111111-1111-4111-8111-111111111111';
const FASHION_WEEK = '22222222-2222-4222-8222-222222222222';
const SILK_FAIR = '33333333-3333-4333-8333-333333333333';
const CRAFT_EXPO = '44444444-4444-4444-8444-444444444444';
const NEW_EVENT = '55555555-5555-4555-8555-555555555555';
const at = '2026-10-01T00:00:00Z';
const showcase = (over: Partial<ShowcaseEvent>): ShowcaseEvent => ({
  id: FASHION_WEEK,
  key: 'tfw-2026',
  title: 'Tashkent Fashion Week',
  titleLanguage: null,
  languages: [],
  organizer: 'Fashion Council',
  scope: 'local',
  format: 'runway',
  eventStatus: 'scheduled',
  attendance: 'offline',
  country: 'UZ',
  city: 'Tashkent',
  venue: 'Humo Arena',
  startsOn: '2026-10-12',
  endsOn: '2026-10-14',
  datePrecision: 'day',
  applicationDeadline: '2026-10-15',
  participation: 'Application package to the organiser',
  cost: null,
  tickets: null,
  organizerWebsite: null,
  address: null,
  startsAt: null,
  timeZone: null,
  ticketUrl: null,
  website: 'https://tfw.example.test/',
  sourceUrl: 'https://listings.example.test/tfw',
  sources: [],
  sourceUpdatedAt: null,
  status: 'candidate',
  priority: 'high',
  notes: 'Ikat coats fit the second day.\nBring the lookbook.',
  revision: 3,
  eventRevision: 1,
  createdAt: at,
  updatedAt: at,
  ...over,
});
const fashionWeek = () => showcase({});
const silkFair = () =>
  showcase({
    id: SILK_FAIR,
    key: null,
    title: 'Silk Road Trade Fair',
    organizer: null,
    scope: 'regional',
    format: 'trade-fair',
    country: 'KZ',
    city: 'Almaty',
    venue: null,
    startsOn: '2026-11-01',
    endsOn: null,
    datePrecision: 'month',
    applicationDeadline: '2026-11-30',
    cost: 'Stand from 300 USD',
    status: 'shortlisted',
    priority: 'normal',
    revision: 1,
  });
const craftExpo = () =>
  showcase({
    id: CRAFT_EXPO,
    key: null,
    title: 'Craft Expo',
    scope: 'unknown',
    format: 'exhibition',
    country: null,
    city: null,
    venue: null,
    startsOn: null,
    endsOn: null,
    datePrecision: 'unknown',
    applicationDeadline: null,
    status: 'applying',
    priority: 'low',
    revision: 2,
  });

let wrapper: VueWrapper | undefined;
let client: ReturnType<typeof createQueryClient> | undefined;
beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(2026, 9, 8, 10, 0));
  useUiLocale().setUiLocale('en');
  window.history.replaceState(null, '', '/');
  api.listEvents.mockResolvedValue(ResultExtensions.ok([fashionWeek(), silkFair(), craftExpo()]));
  api.summary.mockResolvedValue(
    ResultExtensions.ok({
      byStatus: { candidate: 4, shortlisted: 2, applying: 1 },
      upcomingDeadlines: [fashionWeek(), silkFair()],
    }),
  );
  api.getEvent.mockImplementation((_o: string, id: string) =>
    ResultExtensions.ok([fashionWeek(), silkFair(), craftExpo()].find((item) => item.id === id)),
  );
});
afterEach(() => {
  wrapper?.unmount();
  wrapper = undefined;
  client?.clear();
  resolveConfirmation(false);
  vi.useRealTimers();
});

async function render(role: 'owner' | 'editor' | 'viewer', initialLocation: EventsLocation) {
  client = createQueryClient({ retry: { ...DefaultRetryPolicy, maxRetries: 0 } });
  const access = {
    busy: ref(false),
    dirty: ref(false),
    setDirty: vi.fn((value: boolean) => {
      access.dirty.value = value;
    }),
    write: vi.fn((operation: (signal: AbortSignal) => unknown) => operation(new AbortController().signal)),
  };
  const workspace = {
    organization: ref({ id: ORG, name: 'Ocharo', slug: 'ocharo', role }),
    canEdit: computed(() => role !== 'viewer'),
  };
  wrapper = mount(EventsPage, {
    props: {
      access: access as unknown as EventsAccess,
      workspace: workspace as unknown as EventsWorkspace,
      initialLocation,
    },
    global: {
      plugins: [
        queryPlugin(client),
        await eventsRouter(
          initialLocation.view === 'event'
            ? `/organizations/${ORG}/events/${initialLocation.eventId}`
            : `/organizations/${ORG}/events`,
        ),
      ],
    },
    attachTo: document.body,
  });
  return { access };
}
const feed: EventsLocation = { view: 'feed', eventId: null };
const card = (title: string) => wrapper!.findAll('article.event-card').find((item) => item.text().includes(title))!;
const buttonIn = (scope: { findAll(selector: string): DOMWrapper<Element>[] }, label: string) =>
  scope.findAll('button').find((item) => item.text() === label);
const detail = () => wrapper!.get('article[aria-labelledby="event-detail-title"]');
const notesField = () => detail().get('[data-test="tracking-notes"]');
const unfiltered = { query: '', scope: '', format: '', status: '', from: '2026-10-08', limit: 200, offset: 0 };

describe('events feed', () => {
  it('shows the next deadlines, month groups and each card as precisely as announced', async () => {
    await render('editor', feed);
    await flushPromises();
    expect(api.summary).toHaveBeenCalledWith(ORG, '2026-10-08', expect.any(AbortSignal));
    expect(api.listEvents).toHaveBeenCalledWith(ORG, unfiltered, expect.any(AbortSignal));

    const deadlines = wrapper!.findAll('[data-test="deadline"]');
    expect(deadlines.map((item) => item.get('a').text())).toEqual(['Tashkent Fashion Week', 'Silk Road Trade Fair']);
    expect(deadlines[0]!.text()).toContain('Apply by 15 Oct 2026');
    expect(deadlines[0]!.get('.events-days').attributes('data-tone')).toBe('urgent');
    expect(deadlines[0]!.get('.events-days').text()).toBe('7 days left');
    expect(deadlines[1]!.get('.events-days').attributes('data-tone')).toBe('upcoming');
    expect(deadlines[1]!.text()).toContain('Shortlisted');

    expect(wrapper!.findAll('[data-test="event-group"] h3').map((item) => item.text())).toEqual([
      'October 2026',
      'November 2026',
      'Date not announced',
    ]);
    const week = card('Tashkent Fashion Week');
    expect(week.text()).toContain('New — review');
    expect(week.text()).toContain('High priority');
    expect(week.text()).toContain('Fashion Council');
    expect(week.text()).toContain('Tashkent · ');
    expect(week.get('[data-test="event-dates"]').text()).toBe('12–14 Oct 2026');
    for (const badge of ['Runway show', 'Local', 'Candidate', 'Cost not stated']) expect(week.text()).toContain(badge);
    expect(week.get('.events-days').attributes('data-tone')).toBe('urgent');
    const fair = card('Silk Road Trade Fair');
    expect(fair.get('[data-test="event-dates"]').text()).toBe('Nov 2026');
    expect(fair.text()).toContain('Stand from 300 USD');
    expect(fair.text()).not.toContain('New — review');
    expect(card('Craft Expo').get('[data-test="event-dates"]').text()).toBe('Dates not announced');

    expect(
      eventsSelect(wrapper!, 'Status')
        .props('options')
        .map((option: { label: string }) => option.label),
    ).toEqual([
      'All statuses (7)',
      'Candidate (4)',
      'Shortlisted (2)',
      'Applying (1)',
      'Applied (0)',
      'Confirmed (0)',
      'Attended (0)',
      'Skipped (0)',
      'Rejected (0)',
    ]);
    // Events records decisions only: nothing offers to apply, register, send or pay.
    expect(wrapper!.findAll('button').some((item) => /apply|register|send|pay/i.test(item.text()))).toBe(false);
  });

  it('tags a title written in another language than the interface and reloads the feed when the language changes', async () => {
    api.listEvents.mockResolvedValue(
      ResultExtensions.ok([showcase({ titleLanguage: 'ru-Cyrl', languages: ['ru-Cyrl'] }), silkFair()]),
    );
    await render('editor', feed);
    await flushPromises();
    const tag = card('Tashkent Fashion Week').get('[data-test="title-language"]');
    expect(tag.text()).toBe('RU');
    expect(tag.attributes('title')).toContain('Title language');
    expect(card('Silk Road Trade Fair').find('[data-test="title-language"]').exists()).toBe(false);

    const calls = api.listEvents.mock.calls.length;
    useUiLocale().setUiLocale('ru');
    await flushPromises();
    expect(api.listEvents.mock.calls.length).toBeGreaterThan(calls);
    expect(card('Tashkent Fashion Week').find('[data-test="title-language"]').exists()).toBe(false);
  });

  it("shows the source ticket price apart from Ocharo's own cost", async () => {
    api.listEvents.mockResolvedValue(
      ResultExtensions.ok([
        showcase({ tickets: { minPrice: 150000, maxPrice: null, currency: 'UZS', free: false } }),
        showcase({
          id: SILK_FAIR,
          title: 'Silk Road Trade Fair',
          tickets: { minPrice: null, maxPrice: null, currency: null, free: true },
        }),
        showcase({ id: CRAFT_EXPO, title: 'Craft Expo', cost: 'Stand from 300 USD' }),
      ]),
    );
    await render('editor', feed);
    await flushPromises();
    const week = card('Tashkent Fashion Week');
    expect(week.get('[data-test="event-tickets"] dt').text()).toBe('Tickets');
    expect(week.get('[data-test="event-tickets"] dd').text()).toBe('from 150\u00a0000\u00a0UZS');
    expect(week.text()).toContain("Ocharo's cost");
    expect(week.text()).toContain('Cost not stated');
    expect(card('Silk Road Trade Fair').get('[data-test="event-tickets"] dd').text()).toBe('Free');
    const expo = card('Craft Expo');
    expect(expo.find('[data-test="event-tickets"]').exists()).toBe(false);
    expect(expo.text()).toContain('Stand from 300 USD');
  });

  it('names the source a card came from and marks a listing removed at every source', async () => {
    api.listEvents.mockResolvedValue(
      ResultExtensions.ok([
        showcase({
          sources: [
            { name: 'Afisha Uzbekistan', url: 'https://afisha.example.test/jazz', state: 'gone' },
            { name: null, url: null, state: 'unmatched' },
          ],
        }),
        showcase({
          id: SILK_FAIR,
          title: 'Silk Road Trade Fair',
          sources: [
            { name: 'Afisha Uzbekistan', url: 'javascript:alert(1)', state: 'gone' },
            { name: 'Anons', url: 'https://anons.example.test/fair', state: 'live' },
          ],
        }),
        showcase({ id: CRAFT_EXPO, title: 'Craft Expo' }),
      ]),
    );
    await render('editor', feed);
    await flushPromises();
    const week = card('Tashkent Fashion Week');
    expect(week.get('[data-test="event-source"]').text()).toBe('via Afisha Uzbekistan');
    const link = week.get('[data-test="event-source"] a');
    expect(link.text()).toBe('Afisha Uzbekistan');
    expect(link.attributes('href')).toBe('https://afisha.example.test/jazz');
    expect(link.attributes('rel')).toBe('noopener noreferrer');
    expect(week.get('[data-test="listing-removed"]').text()).toBe('Listing removed at source');
    const fair = card('Silk Road Trade Fair');
    expect(fair.get('[data-test="event-source"]').text()).toBe('via Afisha Uzbekistan');
    expect(fair.get('[data-test="event-source"]').find('a').exists()).toBe(false);
    expect(fair.find('[data-test="listing-removed"]').exists()).toBe(false);
    expect(card('Craft Expo').find('[data-test="event-source"]').exists()).toBe(false);
    expect(card('Craft Expo').find('[data-test="listing-removed"]').exists()).toBe(false);

    useUiLocale().setUiLocale('uz');
    await flushPromises();
    expect(card('Tashkent Fashion Week').get('[data-test="event-source"]').text()).toBe('Afisha Uzbekistan orqali');
    expect(card('Tashkent Fashion Week').get('[data-test="listing-removed"]').text()).toBe(
      'Manbada e’lon olib tashlangan',
    );
  });

  it('marks a cancelled, postponed or online event as such and leaves a plain event unmarked', async () => {
    api.listEvents.mockResolvedValue(
      ResultExtensions.ok([
        showcase({ eventStatus: 'cancelled', attendance: 'online' }),
        showcase({ id: SILK_FAIR, title: 'Silk Road Trade Fair', eventStatus: 'postponed' }),
        showcase({ id: CRAFT_EXPO, title: 'Craft Expo', eventStatus: 'rescheduled', attendance: 'mixed' }),
        showcase({ id: NEW_EVENT, title: 'Plain Fair' }),
      ]),
    );
    await render('editor', feed);
    await flushPromises();
    const week = card('Tashkent Fashion Week');
    expect(week.get('[data-test="event-happening"]').text()).toBe('Cancelled');
    expect(week.get('[data-test="event-attendance"]').text()).toBe('Online');
    expect(week.get('h4 a').classes()).toContain('line-through');
    expect(card('Silk Road Trade Fair').get('[data-test="event-happening"]').text()).toBe('Postponed');
    expect(card('Silk Road Trade Fair').find('[data-test="event-attendance"]').exists()).toBe(false);
    expect(card('Silk Road Trade Fair').get('h4 a').classes()).not.toContain('line-through');
    expect(card('Craft Expo').get('[data-test="event-happening"]').text()).toBe('Rescheduled');
    expect(card('Craft Expo').get('[data-test="event-attendance"]').text()).toBe('In person and online');
    const plain = card('Plain Fair');
    expect(plain.find('[data-test="event-happening"]').exists()).toBe(false);
    expect(plain.find('[data-test="event-attendance"]').exists()).toBe(false);
  });

  it('turns every filter into query parameters and drops from=today when past events are shown', async () => {
    await render('editor', feed);
    await flushPromises();
    await wrapper!.get('input[type="search"]').setValue('silk');
    await new Promise((resolve) => setTimeout(resolve, 300));
    await flushPromises();
    expect(api.listEvents).toHaveBeenLastCalledWith(ORG, { ...unfiltered, query: 'silk' }, expect.any(AbortSignal));

    const chips = wrapper!.findAll('.events-chip');
    expect(chips.map((chip) => chip.text())).toEqual(['All', 'Local', 'Regional', 'International']);
    await chips[3]!.trigger('click');
    expect(chips[3]!.attributes('aria-pressed')).toBe('true');
    await chooseEventsValue(wrapper!, 'Format', 'trade-fair');
    await chooseEventsValue(wrapper!, 'Status', 'candidate');
    await flushPromises();
    expect(api.listEvents).toHaveBeenLastCalledWith(
      ORG,
      { ...unfiltered, query: 'silk', scope: 'international', format: 'trade-fair', status: 'candidate' },
      expect.any(AbortSignal),
    );

    await wrapper!.get('.events-toggle input').setValue(true);
    await flushPromises();
    expect(api.listEvents).toHaveBeenLastCalledWith(
      ORG,
      { ...unfiltered, query: 'silk', scope: 'international', format: 'trade-fair', status: 'candidate', from: null },
      expect.any(AbortSignal),
    );
  });

  it('shortlists or skips a candidate with one tracking PUT carrying its revision', async () => {
    await render('editor', feed);
    await flushPromises();
    api.updateTracking.mockResolvedValue(ResultExtensions.ok({ ...fashionWeek(), status: 'shortlisted', revision: 4 }));
    expect(buttonIn(card('Silk Road Trade Fair'), 'Shortlist')).toBeUndefined();
    const calls = api.listEvents.mock.calls.length;
    await buttonIn(card('Tashkent Fashion Week'), 'Shortlist')!.trigger('click');
    await flushPromises();
    expect(api.updateTracking).toHaveBeenCalledTimes(1);
    expect(api.updateTracking).toHaveBeenCalledWith(
      ORG,
      FASHION_WEEK,
      3,
      { ...trackingInput(fashionWeek()), status: 'shortlisted' },
      expect.any(AbortSignal),
    );
    expect(api.listEvents.mock.calls.length).toBeGreaterThan(calls);

    await buttonIn(card('Tashkent Fashion Week'), 'Skip')!.trigger('click');
    await flushPromises();
    expect(api.updateTracking).toHaveBeenLastCalledWith(
      ORG,
      FASHION_WEEK,
      3,
      { ...trackingInput(fashionWeek()), status: 'skipped' },
      expect.any(AbortSignal),
    );
  });

  it.each([409, 410])('reloads the feed and says so when a card review answers %i', async (status) => {
    const notice = vi.spyOn(feedbackBus, 'notify');
    await render('editor', feed);
    await flushPromises();
    api.updateTracking.mockResolvedValue(
      ResultExtensions.fail(ApiFailureFactory.create('http', { status, headers: new Headers() })),
    );
    const calls = api.listEvents.mock.calls.length;
    await buttonIn(card('Tashkent Fashion Week'), 'Shortlist')!.trigger('click');
    await flushPromises();
    expect(notice).toHaveBeenCalledWith(expect.objectContaining({ tone: 'danger' }));
    expect(api.listEvents.mock.calls.length).toBeGreaterThan(calls);
    notice.mockRestore();
  });

  it('creates a manual event as shortlisted by default and opens it', async () => {
    await render('editor', feed);
    await flushPromises();
    await buttonIn(wrapper!, 'Add event')!.trigger('click');
    const form = wrapper!.get('section[aria-label="New event"] form');
    await form.get('input').setValue('Bukhara Crafts Market');
    await form.get('input[type="date"]').setValue('2026-12-05');
    api.createEvent.mockResolvedValue(
      ResultExtensions.ok(showcase({ id: NEW_EVENT, title: 'Bukhara Crafts Market', status: 'shortlisted' })),
    );
    await form.trigger('submit');
    await flushPromises();
    expect(api.createEvent).toHaveBeenCalledWith(
      ORG,
      expect.objectContaining({
        title: 'Bukhara Crafts Market',
        status: 'shortlisted',
        priority: 'normal',
        datePrecision: 'day',
        startsOn: '2026-12-05',
        endsOn: null,
        cost: null,
      }),
      expect.any(AbortSignal),
    );
    expect(window.location.pathname).toBe(`/organizations/${ORG}/events/${NEW_EVENT}`);
  });

  it('asks no scope for a new event and shows the one its country derives', async () => {
    await render('editor', feed);
    await flushPromises();
    await buttonIn(wrapper!, 'Add event')!.trigger('click');
    const form = wrapper!.get('section[aria-label="New event"] form');
    expect(() => eventsSelect(wrapper!, 'Scope')).toThrow('Missing EventsSelect: Scope');
    expect(form.text()).not.toContain('Scope:');
    const derived = () => form.get('[data-test="derived-scope"]').text();
    expect(derived()).toBe('Scope unknown until a country is entered.');
    const country = form.get('input[maxlength="2"]');
    await country.setValue('uz');
    expect(derived()).toBe('Local: Uzbekistan');
    await country.setValue('KZ');
    expect(derived()).toBe('Regional: Kazakhstan');
    await country.setValue('FR');
    expect(derived()).toBe('International: France');
    await country.setValue('');
    await form.get('input').setValue('Online Crafts Market');
    const created = showcase({ id: NEW_EVENT, title: 'Online Crafts Market', country: null, scope: 'unknown' });
    api.createEvent.mockResolvedValue(ResultExtensions.ok(created));
    api.getEvent.mockResolvedValue(ResultExtensions.ok(created));
    await chooseEventsValue(wrapper!, 'Date precision', 'unknown');
    await form.trigger('submit');
    await flushPromises();
    expect(api.createEvent).toHaveBeenCalledTimes(1);
    const sent = api.createEvent.mock.calls[0]![1] as Record<string, unknown>;
    expect(sent.country).toBeNull();
    expect(sent).not.toHaveProperty('scope');
    expect(detail().text()).toContain('Scope unknown');
  });

  it('reads an event with no country as scope unknown, which no scope chip offers', async () => {
    await render('editor', feed);
    await flushPromises();
    expect(card('Craft Expo').text()).toContain('Scope unknown');
    expect(wrapper!.findAll('.events-chip').map((chip) => chip.text())).not.toContain('Scope unknown');
  });

  it('opens an event at its own address and returns to the feed', async () => {
    await render('editor', feed);
    await flushPromises();
    await card('Silk Road Trade Fair').get('h4 a').trigger('click');
    await flushPromises();
    expect(window.location.pathname).toBe(`/organizations/${ORG}/events/${SILK_FAIR}`);
    expect(api.getEvent).toHaveBeenCalledWith(ORG, SILK_FAIR, expect.any(AbortSignal));
    expect(detail().text()).toContain('Silk Road Trade Fair');
    await buttonIn(wrapper!, '← All events')!.trigger('click');
    await flushPromises();
    expect(window.location.pathname).toBe(`/organizations/${ORG}/events`);
    expect(wrapper!.find('article[aria-labelledby="event-detail-title"]').exists()).toBe(false);
  });
});

describe('event detail', () => {
  it('shows every field with safe links only', async () => {
    api.getEvent.mockResolvedValue(ResultExtensions.ok({ ...fashionWeek(), website: 'javascript:alert(1)' }));
    await render('editor', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    const text = detail().text();
    for (const value of [
      'Tashkent Fashion Week',
      '12–14 Oct 2026',
      'Exact dates',
      'Humo Arena',
      'Application package to the organiser',
      'Cost not stated',
      'tfw-2026',
      '15 Oct 2026',
      '7 days left',
    ])
      expect(text).toContain(value);
    expect(detail().find('dd.whitespace-pre-wrap').exists()).toBe(true);
    const links = detail().findAll('a');
    expect(links.map((link) => link.attributes('href'))).toEqual(['https://listings.example.test/tfw']);
    expect(
      links.every((link) => link.attributes('target') === '_blank' && link.attributes('rel') === 'noopener noreferrer'),
    ).toBe(true);
    expect(text).toContain('javascript:alert(1)');
  });

  it('shows the ticket price and a safe ticket link', async () => {
    api.getEvent.mockResolvedValue(
      ResultExtensions.ok({
        ...fashionWeek(),
        tickets: { minPrice: 150000, maxPrice: 150000, currency: 'UZS', free: false },
        ticketUrl: 'https://tickets.example.test/tfw',
      }),
    );
    await render('viewer', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    expect(detail().get('[data-test="event-tickets"]').text()).toContain('150\u00a0000\u00a0UZS');
    expect(detail().text()).toContain("Ocharo's cost");
    const ticket = detail()
      .findAll('a')
      .find((link) => link.text() === 'Ticket link');
    expect(ticket?.attributes('href')).toBe('https://tickets.example.test/tfw');
    expect(ticket?.attributes('target')).toBe('_blank');
    expect(ticket?.attributes('rel')).toBe('noopener noreferrer');
  });

  it('shows the start time, the address under the venue and the organizer linked to its site', async () => {
    api.getEvent.mockResolvedValue(
      ResultExtensions.ok({
        ...fashionWeek(),
        endsOn: null,
        startsAt: '19:00',
        timeZone: null,
        address: 'Pakhtakor street 5',
        organizerWebsite: 'https://council.example.test/',
      }),
    );
    await render('viewer', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    expect(detail().text()).toContain('12 Oct 2026, 19:00');
    expect(detail().get('[data-test="event-address"]').text()).toBe('Pakhtakor street 5');
    const organizer = detail().get('[data-test="event-organizer"] a');
    expect(organizer.text()).toBe('Fashion Council');
    expect(organizer.attributes('href')).toBe('https://council.example.test/');
    expect(organizer.attributes('rel')).toBe('noopener noreferrer');
  });

  it('lists every source with its listing state and says when the source updated a tracked event', async () => {
    api.getEvent.mockResolvedValue(
      ResultExtensions.ok({
        ...fashionWeek(),
        status: 'shortlisted',
        sourceUpdatedAt: '2026-10-07T09:30:00Z',
        sources: [
          { name: 'Afisha Uzbekistan', url: 'https://afisha.example.test/jazz', state: 'gone' },
          { name: null, url: 'https://desk.example.test/42', state: null },
          { name: null, url: null, state: 'unmatched' },
        ],
      }),
    );
    await render('viewer', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    const items = detail().findAll('[data-test="event-sources"] li');
    expect(items.map((item) => item.get('a, span').text())).toEqual([
      'Afisha Uzbekistan',
      'desk.example.test',
      'Unnamed source',
    ]);
    const state = (item: DOMWrapper<Element>) => {
      const badge = item.find('[data-test="source-state"]');
      return badge.exists() ? badge.text() : null;
    };
    expect(items.map(state)).toEqual(['Listing removed', null, 'No longer matches']);
    expect(items[0]!.get('a').attributes('href')).toBe('https://afisha.example.test/jazz');
    expect(items[2]!.find('a').exists()).toBe(false);
    expect(detail().get('[data-test="source-updated"]').text()).toBe('Updated by source on 7 Oct 2026');
    // The import's own source reports no state, so the listing has not left every source.
    expect(detail().find('[data-test="listing-removed"]').exists()).toBe(false);
  });

  it('keeps the recorded source link and no update line for an event without sources', async () => {
    api.getEvent.mockResolvedValue(ResultExtensions.ok(fashionWeek()));
    await render('viewer', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    expect(detail().find('[data-test="event-sources"]').exists()).toBe(false);
    expect(detail().find('[data-test="source-updated"]').exists()).toBe(false);
    expect(detail().text()).toContain('https://listings.example.test/tfw');
  });

  it('shows an unsafe organizer site as the plain organizer name', async () => {
    api.getEvent.mockResolvedValue(ResultExtensions.ok({ ...fashionWeek(), organizerWebsite: 'javascript:alert(1)' }));
    await render('viewer', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    expect(detail().get('[data-test="event-organizer"]').text()).toBe('Fashion Council');
    expect(detail().get('[data-test="event-organizer"]').find('a').exists()).toBe(false);
    expect(detail().find('[data-test="event-address"]').exists()).toBe(false);
  });

  it('shows an unsafe ticket address as plain text, never as a link', async () => {
    api.getEvent.mockResolvedValue(ResultExtensions.ok({ ...fashionWeek(), ticketUrl: 'javascript:alert(1)' }));
    await render('viewer', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    expect(detail().get('[data-test="event-tickets"]').text()).toContain('javascript:alert(1)');
    expect(detail().get('[data-test="event-tickets"]').find('a').exists()).toBe(false);
  });

  it('shows a cancelled online event with a struck-through title and its badges', async () => {
    api.getEvent.mockResolvedValue(
      ResultExtensions.ok({ ...fashionWeek(), eventStatus: 'cancelled', attendance: 'online' }),
    );
    await render('viewer', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    expect(detail().get('#event-detail-title').classes()).toContain('line-through');
    expect(detail().get('[data-test="event-happening"]').text()).toBe('Cancelled');
    expect(detail().get('[data-test="event-attendance"]').text()).toBe('Online');
  });

  it('hides every edit control from viewers', async () => {
    await render('viewer', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    expect(detail().text()).toContain('Candidate');
    expect(detail().findAll('[role=combobox]')).toHaveLength(0);
    for (const label of ['Edit details', 'Delete event', 'Add event', 'Shortlist', 'Skip'])
      expect(buttonIn(wrapper!, label)).toBeUndefined();
    await buttonIn(wrapper!, '← All events')!.trigger('click');
    await flushPromises();
    expect(card('Tashkent Fashion Week').text()).toContain('New — review');
    expect(card('Tashkent Fashion Week').findAll('button')).toHaveLength(0);
  });

  it('saves status and priority as soon as they change, through the tracking route', async () => {
    await render('editor', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    api.updateTracking.mockResolvedValue(ResultExtensions.ok({ ...fashionWeek(), status: 'applying', revision: 4 }));
    await chooseEventsValue(wrapper!.findComponent({ name: 'EventDetail' }), 'Status', 'applying');
    await flushPromises();
    expect(api.updateTracking).toHaveBeenLastCalledWith(
      ORG,
      FASHION_WEEK,
      3,
      { ...trackingInput(fashionWeek()), status: 'applying' },
      expect.any(AbortSignal),
    );
    api.getEvent.mockResolvedValue(ResultExtensions.ok({ ...fashionWeek(), status: 'applying', revision: 4 }));
    await chooseEventsValue(wrapper!.findComponent({ name: 'EventDetail' }), 'Priority', 'low');
    await flushPromises();
    expect(api.updateTracking).toHaveBeenCalledTimes(2);
    expect(api.updateTracking.mock.calls[1]![3]).toMatchObject({ priority: 'low' });
  });

  it('retains the committed status throughout a pending and rejected update', async () => {
    await render('editor', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    const eventDetail = wrapper!.findComponent({ name: 'EventDetail' });
    const picker = eventsSelect(eventDetail, 'Status');
    let finish!: (value: unknown) => void;
    api.updateTracking.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          finish = resolve;
        }),
    );
    await chooseEventsValue(eventDetail, 'Status', 'applying');
    expect(api.updateTracking).toHaveBeenCalledTimes(1);
    expect(picker.props('modelValue')).toBe('candidate');
    expect(picker.props('disabled')).toBe(true);
    finish(ResultExtensions.fail(ApiFailureFactory.create('http', { status: 409, headers: new Headers() })));
    await flushPromises();
    expect(picker.props('modelValue')).toBe('candidate');
    expect(picker.props('disabled')).toBe(false);
    expect(eventDetail.text()).toContain('This record changed. Reload the latest version before trying again.');
  });

  it('keeps the edited values and offers a reload after a revision conflict', async () => {
    const { access } = await render('editor', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    await buttonIn(detail(), 'Edit details')!.trigger('click');
    await notesField().setValue('Ikat coats fit day two.');
    expect(access.setDirty).toHaveBeenLastCalledWith(true);
    api.updateTracking.mockResolvedValue(
      ResultExtensions.fail(ApiFailureFactory.create('http', { status: 409, headers: new Headers() })),
    );
    await detail().get('form').trigger('submit');
    await flushPromises();
    expect(api.updateTracking).toHaveBeenCalledWith(
      ORG,
      FASHION_WEEK,
      3,
      expect.objectContaining({ notes: 'Ikat coats fit day two.', status: 'candidate' }),
      expect.any(AbortSignal),
    );
    expect(detail().text()).toContain('This record changed. Your edits are still here.');
    expect((notesField().element as HTMLTextAreaElement).value).toBe('Ikat coats fit day two.');
    expect(buttonIn(detail(), 'Discard my edits and reload')).toBeDefined();
    expect(access.dirty.value).toBe(true);
  });

  it('saves the tracking fields of an existing event and closes the form', async () => {
    await render('editor', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    await buttonIn(detail(), 'Edit details')!.trigger('click');
    await chooseEventsValue(wrapper!.findComponent({ name: 'EventDetail' }), 'Status', 'applying');
    await detail().get('[data-test="tracking-cost"]').setValue(' Stand from 500 USD ');
    await notesField().setValue('Book a stand.');
    api.updateTracking.mockResolvedValue(
      ResultExtensions.ok({ ...fashionWeek(), status: 'applying', cost: 'Stand from 500 USD', revision: 4 }),
    );
    await detail().get('form').trigger('submit');
    await flushPromises();
    expect(api.updateTracking).toHaveBeenCalledWith(
      ORG,
      FASHION_WEEK,
      3,
      {
        status: 'applying',
        priority: 'high',
        participation: fashionWeek().participation,
        cost: 'Stand from 500 USD',
        notes: 'Book a stand.',
      },
      expect.any(AbortSignal),
    );
    expect(detail().find('form').exists()).toBe(false);
  });

  it('rejects an end date before the start date in a new event without sending', async () => {
    await render('editor', feed);
    await flushPromises();
    await buttonIn(wrapper!, 'Add event')!.trigger('click');
    const form = wrapper!.get('section[aria-label="New event"] form');
    await form.get('input').setValue('Bukhara Crafts Market');
    const [start, end] = form.findAll('input[type="date"]');
    await start!.setValue('2026-12-05');
    await end!.setValue('2026-12-01');
    await form.trigger('submit');
    await flushPromises();
    expect(form.text()).toContain('The end date cannot be before the start date.');
    expect(api.createEvent).not.toHaveBeenCalled();
  });

  it('offers no delete action, and no title, date or place field, for an existing event', async () => {
    await render('editor', { view: 'event', eventId: SILK_FAIR });
    await flushPromises();
    expect(buttonIn(detail(), 'Delete event')).toBeUndefined();
    expect(wrapper!.text()).not.toContain('Delete event');
    await buttonIn(detail(), 'Edit details')!.trigger('click');
    const form = detail().get('form');
    expect(form.findAll('input[type="date"], input[type="month"]')).toHaveLength(0);
    for (const label of [
      'Event title',
      'Organizer',
      'Scope',
      'Format',
      'Country code',
      'City',
      'Venue',
      'Date precision',
      'Start date',
      'End date',
      'Application deadline',
      'Website',
      'Where it was found',
    ])
      expect(form.text()).not.toContain(label);
    for (const label of ['Status', 'Priority', "Ocharo's cost", 'How to take part', 'Notes'])
      expect(form.text()).toContain(label);
    expect(form.text()).toContain("Dates, place and organizer come from the event's source and cannot be edited here.");
  });
});

describe('event collection outcomes', () => {
  it('keeps controls visible while matching feed and deadline skeletons load', async () => {
    let finishEvents!: (value: ReturnType<typeof ResultExtensions.ok<ShowcaseEvent[]>>) => void;
    api.listEvents.mockImplementation(
      () =>
        new Promise((resolve) => {
          finishEvents = resolve;
        }),
    );
    api.summary.mockImplementation(() => new Promise(() => {}));
    await render('viewer', feed);
    await flushPromises();
    expect(wrapper!.findAll('.events-list .event-card')).toHaveLength(3);
    expect(wrapper!.findAll('.events-deadlines .events-deadline')).toHaveLength(3);
    expect(
      wrapper!.findAll('[aria-busy="true"] [role="status"]').every((item) => item.classes().includes('sr-only')),
    ).toBe(true);
    expect(buttonIn(wrapper!, 'Refresh events')).toBeDefined();
    expect(wrapper!.find('input[type="search"]').exists()).toBe(true);
    expect(wrapper!.text()).not.toContain('No upcoming events.');
    finishEvents(ResultExtensions.ok([]));
    await flushPromises();
    expect(wrapper!.get('.empty-state').text()).toContain('No upcoming events.');
    expect(wrapper!.find('.editor-prompt').exists()).toBe(false);
  });

  it('toasts a failed first read without an error panel or false empty result', async () => {
    const notice = vi.spyOn(feedbackBus, 'notify');
    const failure = ResultExtensions.fail(ApiFailureFactory.create('http', { status: 500, headers: new Headers() }));
    api.listEvents.mockResolvedValue(failure);
    api.summary.mockResolvedValue(failure);
    await render('viewer', feed);
    await flushPromises();
    expect(notice).toHaveBeenCalledWith(expect.objectContaining({ tone: 'danger' }));
    expect(wrapper!.text()).not.toContain('unavailable');
    expect(wrapper!.text()).not.toContain('No upcoming');
    expect(wrapper!.find('[aria-busy="true"]').exists()).toBe(false);
    expect(buttonIn(wrapper!, 'Refresh events')).toBeDefined();
    api.listEvents.mockResolvedValue(ResultExtensions.ok([fashionWeek()]));
    api.summary.mockResolvedValue(ResultExtensions.ok({ byStatus: { candidate: 1 }, upcomingDeadlines: [] }));
    await buttonIn(wrapper!, 'Refresh events')!.trigger('click');
    await flushPromises();
    expect(card('Tashkent Fashion Week')).toBeDefined();
    notice.mockRestore();
  });

  it('retains loaded cards and deadlines when a later refresh fails', async () => {
    await render('viewer', feed);
    await flushPromises();
    const failure = ResultExtensions.fail(ApiFailureFactory.create('http', { status: 503, headers: new Headers() }));
    api.listEvents.mockResolvedValue(failure);
    api.summary.mockResolvedValue(failure);
    await buttonIn(wrapper!, 'Refresh events')!.trigger('click');
    await flushPromises();
    expect(card('Tashkent Fashion Week')).toBeDefined();
    expect(wrapper!.findAll('[data-test="deadline"]')).toHaveLength(2);
    expect(wrapper!.find('.empty-state').exists()).toBe(false);
    expect(wrapper!.text()).not.toContain('unavailable');
  });
});

describe('event review safety', () => {
  it('saves a draft against the revision it opened even after another read changes the record', async () => {
    await render('editor', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    await buttonIn(detail(), 'Edit details')!.trigger('click');
    await notesField().setValue('My draft note');
    api.getEvent.mockResolvedValue(
      ResultExtensions.ok(showcase({ revision: 4, title: 'Another editor title', notes: 'New note' })),
    );
    await buttonIn(wrapper!, 'Refresh')!.trigger('click');
    await flushPromises();
    expect((notesField().element as HTMLTextAreaElement).value).toBe('My draft note');
    api.updateTracking.mockResolvedValue(
      ResultExtensions.fail(ApiFailureFactory.create('http', { status: 409, headers: new Headers() })),
    );
    await detail().get('form').trigger('submit');
    await flushPromises();
    expect(api.updateTracking).toHaveBeenLastCalledWith(
      ORG,
      FASHION_WEEK,
      3,
      expect.objectContaining({ notes: 'My draft note', participation: fashionWeek().participation }),
      expect.any(AbortSignal),
    );
    expect(detail().text()).toContain('This record changed. Your edits are still here.');
  });

  it('keeps a conflicting draft when reload fails and accepts the new revision only after a successful reload', async () => {
    const { access } = await render('editor', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    await buttonIn(detail(), 'Edit details')!.trigger('click');
    await notesField().setValue('Keep this draft');
    api.updateTracking.mockResolvedValue(
      ResultExtensions.fail(ApiFailureFactory.create('http', { status: 409, headers: new Headers() })),
    );
    await detail().get('form').trigger('submit');
    await flushPromises();
    api.getEvent.mockResolvedValue(
      ResultExtensions.fail(ApiFailureFactory.create('http', { status: 503, headers: new Headers() })),
    );
    await buttonIn(detail(), 'Discard my edits and reload')!.trigger('click');
    await flushPromises();
    expect((notesField().element as HTMLTextAreaElement).value).toBe('Keep this draft');
    expect(access.dirty.value).toBe(true);
    expect(buttonIn(detail(), 'Discard my edits and reload')).toBeDefined();
    api.getEvent.mockResolvedValue(ResultExtensions.ok(showcase({ revision: 4, notes: 'Latest confirmed note' })));
    await buttonIn(detail(), 'Discard my edits and reload')!.trigger('click');
    await flushPromises();
    expect((notesField().element as HTMLTextAreaElement).value).toBe('Latest confirmed note');
    expect(access.dirty.value).toBe(false);
    await notesField().setValue('Edited after reload');
    await detail().get('form').trigger('submit');
    await flushPromises();
    expect(api.updateTracking).toHaveBeenLastCalledWith(
      ORG,
      FASHION_WEEK,
      4,
      expect.objectContaining({ notes: 'Edited after reload' }),
      expect.any(AbortSignal),
    );
  });

  it('blocks a submitted form while another command owns the workspace', async () => {
    const { access } = await render('editor', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    await buttonIn(detail(), 'Edit details')!.trigger('click');
    await notesField().setValue('Pending edit');
    access.busy.value = true;
    await flushPromises();
    await detail().get('form').trigger('submit');
    await flushPromises();
    expect(api.updateTracking).not.toHaveBeenCalled();
    expect(access.dirty.value).toBe(true);
  });

  it('shows a neutral missing detail and a working refresh action after a failed initial read', async () => {
    api.getEvent.mockResolvedValue(
      ResultExtensions.fail(ApiFailureFactory.create('http', { status: 500, headers: new Headers() })),
    );
    await render('viewer', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    expect(wrapper!.text()).toContain('Event unavailable');
    expect(wrapper!.text()).not.toContain('Content unavailable. Try refreshing.');
    expect(buttonIn(wrapper!, 'Refresh')).toBeDefined();
    api.getEvent.mockResolvedValue(ResultExtensions.ok(fashionWeek()));
    await buttonIn(wrapper!, 'Refresh')!.trigger('click');
    await flushPromises();
    expect(detail().text()).toContain('Tashkent Fashion Week');
  });
});

describe('event feed day and empty states', () => {
  it('updates date filters and open detail deadlines when a sleeping tab regains focus', async () => {
    await render('viewer', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    expect(detail().text()).toContain('7 days left');
    vi.setSystemTime(new Date(2026, 9, 9, 10, 0));
    window.dispatchEvent(new Event('focus'));
    await flushPromises();
    expect(api.listEvents).toHaveBeenLastCalledWith(
      ORG,
      { ...unfiltered, from: '2026-10-09' },
      expect.any(AbortSignal),
    );
    expect(api.summary).toHaveBeenLastCalledWith(ORG, '2026-10-09', expect.any(AbortSignal));
    expect(detail().text()).toContain('6 days left');
  });

  it('updates the feed at local midnight without waiting for another interaction', async () => {
    vi.useFakeTimers({ toFake: ['Date', 'setTimeout', 'clearTimeout'] });
    vi.setSystemTime(new Date(2026, 9, 8, 23, 59, 59));
    await render('viewer', feed);
    await flushPromises();
    await vi.advanceTimersByTimeAsync(1000);
    await flushPromises();
    expect(api.listEvents).toHaveBeenLastCalledWith(
      ORG,
      { ...unfiltered, from: '2026-10-09' },
      expect.any(AbortSignal),
    );
  });

  it('does not label a failed filter or deadline refresh as an empty result', async () => {
    api.listEvents.mockResolvedValue(ResultExtensions.ok([]));
    api.summary.mockResolvedValue(ResultExtensions.ok({ byStatus: {}, upcomingDeadlines: [] }));
    await render('viewer', feed);
    await flushPromises();
    expect(wrapper!.text()).toContain('No upcoming application deadlines.');
    const failure = ResultExtensions.fail(ApiFailureFactory.create('http', { status: 503, headers: new Headers() }));
    api.listEvents.mockResolvedValue(failure);
    api.summary.mockResolvedValue(failure);
    await wrapper!.findAll('.events-chip')[1]!.trigger('click');
    await flushPromises();
    expect(wrapper!.text()).not.toContain('No events match these filters.');
    await buttonIn(wrapper!, 'Refresh events')!.trigger('click');
    await flushPromises();
    expect(wrapper!.text()).not.toContain('No upcoming application deadlines.');
    expect(wrapper!.get('input[type="search"]').attributes('maxlength')).toBe('120');
  });
});

describe('event date precision editing', () => {
  it('carries edited months and days across precision changes without restoring stale hidden dates', async () => {
    await render('editor', feed);
    await flushPromises();
    await buttonIn(wrapper!, 'Add event')!.trigger('click');
    const form = wrapper!.get('section[aria-label="New event"] form');
    await form.get('input').setValue('Bukhara Crafts Market');
    await form.findAll('input[type="date"]')[0]!.setValue('2026-10-12');
    await form.findAll('input[type="date"]')[1]!.setValue('2026-10-14');
    await chooseEventsValue(wrapper!, 'Date precision', 'month');
    expect((form.findAll('input[type="month"]')[0]!.element as HTMLInputElement).value).toBe('2026-10');
    await form.findAll('input[type="month"]')[0]!.setValue('2026-11');
    await form.findAll('input[type="month"]')[1]!.setValue('2026-12');
    await chooseEventsValue(wrapper!, 'Date precision', 'day');
    expect(
      form
        .findAll('input[type="date"]')
        .slice(0, 2)
        .map((input) => (input.element as HTMLInputElement).value),
    ).toEqual(['2026-11-01', '2026-12-01']);
    await form.findAll('input[type="date"]')[0]!.setValue('2026-12-17');
    await form.findAll('input[type="date"]')[1]!.setValue('2026-12-19');
    await chooseEventsValue(wrapper!, 'Date precision', 'unknown');
    await chooseEventsValue(wrapper!, 'Date precision', 'estimated');
    expect(form.findAll('input[type="month"]').map((input) => (input.element as HTMLInputElement).value)).toEqual([
      '2026-12',
      '',
    ]);
    await chooseEventsValue(wrapper!, 'Date precision', 'day');
    expect(
      form
        .findAll('input[type="date"]')
        .slice(0, 2)
        .map((input) => (input.element as HTMLInputElement).value),
    ).toEqual(['2026-12-17', '2026-12-19']);
    api.createEvent.mockResolvedValue(
      ResultExtensions.ok(showcase({ id: NEW_EVENT, title: 'Bukhara Crafts Market', status: 'shortlisted' })),
    );
    await form.trigger('submit');
    await flushPromises();
    expect(api.createEvent).toHaveBeenLastCalledWith(
      ORG,
      expect.objectContaining({ startsOn: '2026-12-17', endsOn: '2026-12-19', datePrecision: 'day' }),
      expect.any(AbortSignal),
    );
  });
});
