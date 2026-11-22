import { computed, ref } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { EventsAccess, EventsWorkspace } from '@/application/events/UseEventsAccess';
import { ocharoWorkflowSpec, type EventsLocation } from '@/domain/events/Events';
import {
  CountryPicker,
  FacetBar,
  type FacetChoiceDefinition,
  type FacetDefinition,
  type FacetSelection,
} from '@wow-two-beta/fe/presentation/forms';
import { EventsFeed } from '@wow-two-beta/fe-events/kit';
import EventsPage from '@/presentation/events/pages/EventsPage.vue';
import { eventsRouter } from './helpers/EventsRouter';

const ORG = '11111111-1111-4111-8111-111111111111';
const FASHION_WEEK = '22222222-2222-4222-8222-222222222222';
const SILK_FAIR = '33333333-3333-4333-8333-333333333333';
const NEW_EVENT = '55555555-5555-4555-8555-555555555555';
const MIRROR = `/api/organizations/${ORG}/events-api/api/events/v1`;
const CATALOG = `${MIRROR}/catalogs/ocharo-fashion`;
const at = '2026-10-01T00:00:00Z';

const event = (id: string, title: Record<string, string>, place: Record<string, string>, starts: object) => ({
  id,
  catalog: 'ocharo-fashion',
  facts: {
    title,
    kind: id === FASHION_WEEK ? 'runway' : 'trade-fair',
    status: 'scheduled',
    attendance: 'offline',
    place,
    starts,
    deadlines: id === FASHION_WEEK ? [{ kind: 'application', at: { value: '2026-10-15', precision: 'day' } }] : [],
  },
  review: 'published',
  sources: [],
  revision: 1,
  createdAtUtc: at,
  updatedAtUtc: at,
});
/** The images the runway show carries, as Events sends them: the source `url` and, for SCFD's copy, a Files `fileRef`. */
let fashionWeekImages: object[] = [];
/** A runway show Ocharo shortlisted, titled by its source in Russian and by SCFD's machine English (OE06). */
const fashionWeek = () => {
  const show = event(
    FASHION_WEEK,
    { 'ru-Cyrl': 'Неделя моды в Ташкенте', en: 'Tashkent Fashion Week' },
    { city: 'Tashkent', country: 'UZ' },
    { value: '2026-10-12', precision: 'day' },
  );
  return fashionWeekImages.length ? { ...show, facts: { ...show.facts, images: fashionWeekImages } } : show;
};
const fashionWeekTracking = () => ({
  id: '99999999-9999-4999-8999-999999999999',
  eventId: FASHION_WEEK,
  stage: 'shortlisted',
  priority: 'high',
  notes: 'Bring the lookbook.',
  fields: {
    cost: 'Stand from 300 USD',
    participation: 'Application package',
    sourceUrl: 'https://listings.example.test/tfw',
  },
  references: [],
  revision: 3,
  createdAtUtc: at,
  updatedAtUtc: at,
});
/** A trade fair in Kazakhstan Ocharo has not looked at yet. */
const silkFair = () =>
  event(
    SILK_FAIR,
    { und: 'Silk Road Trade Fair' },
    { city: 'Almaty', country: 'KZ' },
    { value: '2026-11', precision: 'month' },
  );

interface Sent {
  readonly method: string;
  readonly url: URL;
  readonly body: unknown;
}
let sent: Sent[] = [];
let storedWorkflow: Record<string, unknown> = {};
let listed: unknown[] = [];
/** The nearest application deadlines the summary lists; the feed's own read ignores them. */
let deadlines: unknown[] = [];
/** What the past-events strip's query answers (OE12); none unless a test sets it. */
let pastOpen: unknown[] = [];
let wrapper: VueWrapper | undefined;
/** The router the page was mounted with: the address its filters are kept in. */
let router: Awaited<ReturnType<typeof eventsRouter>>;

/** What the mirrored facets route answers for a list of `total` events: Tashkent (UZ) and Almaty (KZ), each counted once. */
const facetsOf = (total: number) => ({
  total,
  countries: [
    { value: 'UZ', count: 1 },
    { value: 'KZ', count: 1 },
  ],
  cities: [
    { value: 'tashkent', count: 1 },
    { value: 'almaty', count: 1 },
  ],
  kinds: [
    { value: 'runway', count: 1 },
    { value: 'trade-fair', count: 1 },
  ],
  stages: [
    { value: 'candidate', count: 1 },
    { value: 'shortlisted', count: 1 },
    { value: 'skipped', count: 3 },
    { value: 'rejected', count: 0 },
  ],
  priorities: [{ value: 'high', count: 1 }],
  sources: [],
  openDeadline: { before: '2026-11-07', count: 1 },
});

const ok = (data: unknown, status = 200) => Response.json({ data }, { status });
function answer(method: string, url: URL, body: unknown): Response {
  const path = url.pathname;
  if (path === `${MIRROR}/workflow`)
    return ok(method === 'PUT' ? { ...(body as object), revision: 3 } : storedWorkflow);
  if (path === `${CATALOG}/tracking/facets`) return ok(facetsOf(listed.length));
  if (path === `${CATALOG}/tracking`)
    return ok({ items: url.searchParams.has('stages') ? pastOpen : listed, nextCursor: null });
  if (path === `${CATALOG}/tracking/summary`)
    return ok({
      byStage: { shortlisted: 1 },
      untracked: 1,
      upcomingDeadlines: deadlines,
    });
  if (path === `${CATALOG}/events/${FASHION_WEEK}` && method === 'GET') return ok(fashionWeek());
  if (path === `${CATALOG}/tracking/${FASHION_WEEK}` && method === 'GET')
    return ok({ event: fashionWeek(), tracking: fashionWeekTracking() });
  if (path.startsWith(`${CATALOG}/tracking/`) && path.endsWith('/history')) return ok([]);
  if (path.startsWith(`${CATALOG}/tracking/`) && method === 'PUT') {
    const spec = body as { stage: string; expectedRevision: number | null };
    return ok({
      ...fashionWeekTracking(),
      eventId: path.split('/').pop(),
      ...spec,
      revision: (spec.expectedRevision ?? 0) + 1,
    });
  }
  if (path === `${CATALOG}/candidates`)
    return ok({ ...silkFair(), id: NEW_EVENT, facts: (body as { facts: object }).facts }, 201);
  return Response.json({ title: 'Not found' }, { status: 404 });
}

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(2026, 9, 8, 10, 0));
  useUiLocale().setUiLocale('en');
  window.history.replaceState(null, '', '/');
  sent = [];
  pastOpen = [];
  fashionWeekImages = [];
  deadlines = [
    {
      eventId: FASHION_WEEK,
      title: fashionWeek().facts.title,
      deadline: { kind: 'application', at: { value: '2026-10-15', precision: 'day' } },
      stage: 'shortlisted',
      tracked: true,
    },
  ];
  storedWorkflow = { ...ocharoWorkflowSpec(), revision: 2 };
  listed = [
    { event: fashionWeek(), tracking: fashionWeekTracking() },
    { event: silkFair(), tracking: null },
  ];
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const request = input instanceof Request ? input : null;
      const url = new URL(request ? request.url : String(input), 'https://localhost');
      const method = (request?.method ?? init?.method ?? 'GET').toUpperCase();
      const text = request ? await request.clone().text() : typeof init?.body === 'string' ? init.body : '';
      const body: unknown = text ? JSON.parse(text) : undefined;
      sent.push({ method, url, body });
      return answer(method, url, body);
    }),
  );
});
afterEach(() => {
  wrapper?.unmount();
  wrapper = undefined;
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

async function render(
  role: 'owner' | 'editor' | 'viewer',
  initialLocation: EventsLocation = { view: 'feed', eventId: null },
  /** The address's query when the page opens, `kinds=runway&q=silk`: the filters it carries. */
  search = '',
) {
  const access = {
    busy: ref(false),
  };
  const workspace = {
    organization: ref({ id: ORG, name: 'Ocharo' }),
    canEdit: computed(() => role !== 'viewer'),
    canAdmin: computed(() => role === 'owner'),
  };
  router = await eventsRouter(
    `${
      initialLocation.view === 'event'
        ? `/organizations/${ORG}/events/${initialLocation.eventId}`
        : `/organizations/${ORG}/events`
    }${search ? `?${search}` : ''}`,
  );
  wrapper = mount(EventsPage, {
    props: {
      access: access as unknown as EventsAccess,
      workspace: workspace as unknown as EventsWorkspace,
      initialLocation,
    },
    global: { plugins: [router] },
    attachTo: document.body,
  });
  await flushPromises();
  return { access };
}

const lists = () =>
  sent.filter(
    (item) =>
      item.method === 'GET' && item.url.pathname === `${CATALOG}/tracking` && !item.url.searchParams.has('stages'),
  );
const lastList = () => lists().at(-1)!.url.searchParams;
const card = (title: string) =>
  wrapper!.findAll('[data-test="event-media-card"]').find((item) => item.text().includes(title))!;
/** Picks a quick stage from the card's overflow menu (image-first cards keep the quick stages there). */
const pickStage = async (title: string, stage: string) => {
  await card(title).get('[data-test="event-stage-menu"]').trigger('click');
  await flushPromises();
  await (document.body.querySelector(`[data-test="event-quick-stage"][data-stage="${stage}"]`) as HTMLElement).click();
  await flushPromises();
};
const button = (scope: VueWrapper | ReturnType<VueWrapper['get']>, label: string) =>
  scope.findAll('button').find((item) => item.text().trim() === label)!;
/** A scope or kind chip: its label, then the server count it carries. */
const chip = (label: string) => wrapper!.findAll('button').find((item) => item.text().trim().startsWith(label))!;
const facetReads = () =>
  sent.filter((item) => item.method === 'GET' && item.url.pathname === `${CATALOG}/tracking/facets`);
const bar = () => wrapper!.getComponent(FacetBar);
/** The one filter card above the feed, which holds the bar and the feed's own controls. */
const filterCard = () => wrapper!.get('[data-test="event-filters"]');
/** The address's query the page keeps its filters in. */
const query = () => router.currentRoute.value.query;
const facetOf = (key: string) =>
  (bar().props('facets') as readonly FacetDefinition[]).find((definition) => definition.key === key) as
    FacetChoiceDefinition | undefined;
/** Reports a choice the way the bar does, so the feed takes the whole selection. */
async function choose(selection: FacetSelection) {
  bar().vm.$emit('update:modelValue', selection);
  await flushPromises();
}

describe('feed header', () => {
  it('is the title alone: no eyebrow, no description, no Telegram delivery link', async () => {
    await render('owner');

    const header = wrapper!.get('header');
    expect(header.findAll('h1')).toHaveLength(1);
    expect(header.get('h1').text()).toBe('Feed');
    expect(header.element.children).toHaveLength(1);
    expect(wrapper!.find('.eyebrow').exists()).toBe(false);
    expect(wrapper!.text()).not.toContain('Showcase events');
    expect(wrapper!.text()).not.toContain('Events records decisions');
    expect(wrapper!.find('[data-test="telegram-delivery-link"]').exists()).toBe(false);
    expect(wrapper!.findAll('[data-test="event-media-card"]')).toHaveLength(2);
  });

  it("shows no second heading: the kit's own feed title is for screen readers only", async () => {
    await render('editor');

    expect(wrapper!.findAll('h1')).toHaveLength(1);
    expect(wrapper!.get('#events-feed-title').classes()).toContain('sr-only');
  });
});

describe('events feed through the fe-events kit', () => {
  it('is the v2 feed: a facet bar with Ocharo’s scopes, kinds and server counts, a sort and no v1 props', async () => {
    await render('editor');

    const feed = wrapper!.getComponent(EventsFeed);
    expect(Object.keys(feed.props())).not.toContain('variant');
    expect(Object.keys(feed.props())).not.toContain('shownFields');
    // The bar is the card's: the kit's own toolbar is left out, its state handed to the feed instead.
    expect(wrapper!.find('[data-test="events-toolbar"]').exists()).toBe(false);
    expect(feed.props('filters')).toBeDefined();
    expect(wrapper!.get('[data-facet-match]').text()).toBe('2 of 2');
    expect(facetReads().length).toBeGreaterThan(0);
    const scopes = facetOf('scope')!.options;
    expect(scopes.map((option) => option.label)).toEqual(['All', 'National', 'Central Asia', 'International']);
    expect(scopes.map((option) => option.count)).toEqual([undefined, 1, 1, 0]);
    expect(facetOf('kind')!.options.map((option) => [option.label, option.count])).toEqual([
      ['Runway show', 1],
      ['Trade fair', 1],
    ]);
    expect(facetOf('stage')!.options.map((option) => option.label)).toEqual([
      'Candidate',
      'Shortlisted',
      'Skipped',
      'Rejected',
    ]);
    expect(wrapper!.findAll('select[name="sort"] option').map((option) => option.text())).toEqual([
      'Start date',
      'Recently added',
      'Nearest deadline',
    ]);
  });

  it('sends the sort with the tracking read and starts over from no cursor', async () => {
    await render('editor');
    const before = lists().length;

    await wrapper!.get('select[name="sort"]').setValue('deadline');
    await flushPromises();

    expect(lists()).toHaveLength(before + 1);
    expect(lastList().get('sort')).toBe('deadline');
    expect(lastList().has('cursor')).toBe(false);
  });

  it('shows a month divider under the start-date sort and a flat grid under another', async () => {
    await render('editor');
    expect(wrapper!.findAll('[data-test="event-group"] > h3').length).toBeGreaterThan(0);

    await wrapper!.get('select[name="sort"]').setValue('added');
    await flushPromises();

    expect(wrapper!.findAll('[data-test="event-group"] > h3')).toHaveLength(0);
    expect(wrapper!.findAll('[data-test="event-media-card"]')).toHaveLength(2);
  });

  it('lists the tracking feed with one mirrored request: skipped and rejected left out, filters and paging server-side', async () => {
    await render('editor');

    expect(lists()).toHaveLength(1);
    const query = lastList();
    expect(query.getAll('excludeStages')).toEqual(['skipped', 'rejected']);
    expect(query.get('from')).toBe('2026-10-08');
    expect(query.get('limit')).toBe('50');
    expect(query.has('offset')).toBe(false);
    expect(wrapper!.findAll('[data-test="event-media-card"]')).toHaveLength(2);

    const shortlisted = card('Tashkent Fashion Week');
    expect(shortlisted.get('[data-test="event-stage"]').text().toLowerCase()).toBe('shortlisted');
    expect(shortlisted.find('[data-test="event-stage-menu"]').exists()).toBe(false);
    const untracked = card('Silk Road Trade Fair');
    expect(untracked.find('[data-test="event-stage"]').exists()).toBe(false);
    expect(untracked.find('[data-test="event-stage-menu"]').exists()).toBe(true);
  });

  it('shows a title in English, whatever other languages the event carries (the cards carry no language badge)', async () => {
    await render('editor');

    expect(card('Tashkent Fashion Week').get('[data-test="event-title"]').text()).toBe('Tashkent Fashion Week');
    expect(wrapper!.text()).not.toContain('Неделя моды в Ташкенте');
  });

  it('lists skipped and rejected events when asked, and filters by scope chips on the server', async () => {
    await render('editor');

    await choose({ hidden: true });
    expect(lastList().has('excludeStages')).toBe(false);
    await choose({});
    expect(lastList().getAll('excludeStages')).toEqual(['skipped', 'rejected']);

    await chip('Central Asia').trigger('click');
    await flushPromises();
    expect(lastList().getAll('countries')).toEqual(['KZ', 'KG', 'TJ', 'TM']);

    await chip('International').trigger('click');
    await flushPromises();
    expect(lastList().getAll('excludeCountries')).toEqual(['UZ', 'KZ', 'KG', 'TJ', 'TM']);
    expect(lastList().has('countries')).toBe(false);
  });

  it('reads the facets again with each filter, so the counts follow it, and leaves out the sort and the cursor', async () => {
    await render('editor');
    const before = facetReads().length;

    await choose({ city: ['tashkent'] });

    expect(facetReads()).toHaveLength(before + 1);
    const query = facetReads().at(-1)!.url.searchParams;
    expect(query.getAll('cities')).toEqual(['tashkent']);
    expect(query.has('sort')).toBe(false);
    expect(query.has('limit')).toBe(false);
    expect(wrapper!.get('[data-facet-chips]').text()).toContain('Tashkent');
  });

  it('shortlists an untracked event with one tracking write, then reads the feed again', async () => {
    await render('editor');
    const before = lists().length;

    await pickStage('Silk Road Trade Fair', 'shortlisted');

    const writes = sent.filter((item) => item.method === 'PUT');
    expect(writes).toHaveLength(1);
    expect(writes[0]!.url.pathname).toBe(`${CATALOG}/tracking/${SILK_FAIR}`);
    expect(writes[0]!.body).toEqual({ stage: 'shortlisted', expectedRevision: null });
    expect(lists().length).toBe(before + 1);
  });

  it('adds an event that Events publishes at once, shortlisted with where it was found, and lists it', async () => {
    await render('editor');
    const before = lists().length;

    await button(wrapper!, 'Propose an event').trigger('click');
    await wrapper!.get('input[name="title"]').setValue('Margilan Ikat Days');
    wrapper!.getComponent(CountryPicker).vm.$emit('update:modelValue', 'UZ');
    await flushPromises();
    expect(wrapper!.get('[data-test="facts-scope"]').text()).toContain('National');
    await wrapper!.get('input[name="foundAt"]').setValue('https://listings.example.test/ikat');
    await wrapper!.get('form').trigger('submit');
    await flushPromises();

    const proposal = sent.find((item) => item.url.pathname === `${CATALOG}/candidates`)!;
    expect(proposal.method).toBe('POST');
    expect((proposal.body as { facts: { title: Record<string, string> } }).facts.title).toEqual({
      und: 'Margilan Ikat Days',
    });
    const tracking = sent.find(
      (item) => item.method === 'PUT' && item.url.pathname === `${CATALOG}/tracking/${NEW_EVENT}`,
    )!;
    expect(tracking.body).toMatchObject({
      stage: 'shortlisted',
      priority: 'normal',
      fields: { sourceUrl: 'https://listings.example.test/ikat' },
      expectedRevision: null,
    });
    expect(wrapper!.get('[data-test="proposal-sent"]').text()).toContain('Margilan Ikat Days');
    expect(lists().length).toBe(before + 1);
  });

  it('offers a viewer no stage actions and no proposal', async () => {
    await render('viewer');

    expect(wrapper!.find('[data-test="event-stage-menu"]').exists()).toBe(false);
    expect(wrapper!.findAll('button').some((item) => item.text().trim() === 'Propose an event')).toBe(false);
  });
});

describe('the filter card above the feed', () => {
  it('is one card under the heading with the bar in it, and the feed has no toolbar of its own', async () => {
    await render('editor');

    expect(wrapper!.findAll('[data-test="event-filters"]')).toHaveLength(1);
    expect(wrapper!.findAll('[data-facet-match]')).toHaveLength(1);
    expect(wrapper!.findAll('[data-test="events-toolbar"]')).toHaveLength(0);
    expect(filterCard().findComponent(FacetBar).exists()).toBe(true);
    const classes = filterCard().classes();
    expect(classes).toContain('rounded-2xl');
    expect(classes).toContain('p-3');
    expect(classes).toContain('sm:p-5');
    // The card is the first thing in the feed's block, ahead of the feed's own content.
    const block = wrapper!.get('[data-test="events-feed"]').element;
    expect(block.children[0]).toBe(filterCard().element);
    expect(
      filterCard().element.compareDocumentPosition(wrapper!.get('[data-test="event-cards"]').element) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
  });

  it('lays the bar out as the kit’s feed does on a phone, with the kit’s class', async () => {
    await render('editor');

    expect(bar().classes()).toContain('max-sm:[&_[data-facet-inline]]:flex-nowrap');
    expect(bar().classes()).toContain('[&_[data-facet-clear-all]]:min-h-10');
  });

  it('puts the feed’s sort, Propose and overflow controls in the card, in its one actions row', async () => {
    await render('editor');

    const mount = filterCard().get('#feed-actions');
    const row = wrapper!.get('[data-test="events-actions"]');
    expect(mount.element.contains(row.element)).toBe(true);
    expect(filterCard().element.contains(row.element)).toBe(true);
    expect(wrapper!.findAll('[data-test="events-actions"]')).toHaveLength(1);
    expect(row.find('select[name="sort"]').exists()).toBe(true);
    expect(row.findAll('button').some((item) => item.text().trim() === 'Propose an event')).toBe(true);
    expect(row.find('[data-test="feed-overflow"]').exists()).toBe(true);
    // The bar's controls row is where they sit, after Filters.
    expect(bar().element.contains(row.element)).toBe(true);
  });

  it('offers a viewer the sort and the overflow in the card, and no Propose', async () => {
    await render('viewer');

    const row = filterCard().get('[data-test="events-actions"]');
    expect(row.find('select[name="sort"]').exists()).toBe(true);
    expect(row.findAll('button').some((item) => item.text().trim() === 'Propose an event')).toBe(false);
  });

  it('gives the region and kind pills their one-line hints, after a 5 second pause with a 200 ms fade', async () => {
    await render('editor');

    expect(bar().props('hintOpenDelay')).toBe(5000);
    expect(bar().props('hintFadeDuration')).toBe(200);
    expect(facetOf('scope')!.options.map((option) => option.hint?.description)).toEqual([
      undefined,
      'Events in Uzbekistan.',
      'Events in Kazakhstan, Kyrgyzstan, Tajikistan and Turkmenistan.',
      'Events anywhere else in the world.',
    ]);
    expect(facetOf('kind')!.options.map((option) => option.hint?.description)).toEqual([
      'A fashion show where designers present collections on a catwalk.',
      'A business fair where brands, makers and buyers meet to sell and source goods.',
    ]);
    expect(facetOf('stage')!.options.some((option) => option.hint !== undefined)).toBe(false);
    expect(facetOf('city')!.options.some((option) => option.hint !== undefined)).toBe(false);
  });

  it('greys a pill with a count of 0 that is not chosen, and keeps it pressable', async () => {
    await render('editor');

    expect(chip('International').attributes('data-empty')).toBeDefined();
    expect(chip('International').attributes('disabled')).toBeUndefined();
    expect(chip('Central Asia').attributes('data-empty')).toBeUndefined();
  });

  it('writes a chosen kind to the address as kinds and lists the feed with that kind', async () => {
    await render('editor');
    const before = lists().length;
    expect(query().kinds).toBeUndefined();

    await chip('Runway show').trigger('click');
    await flushPromises();

    expect(query().kinds).toEqual(['runway']);
    expect(router.currentRoute.value.path).toBe(`/organizations/${ORG}/events`);
    expect(lists()).toHaveLength(before + 1);
    expect(lastList().getAll('kinds')).toEqual(['runway']);
    expect(facetReads().at(-1)!.url.searchParams.getAll('kinds')).toEqual(['runway']);
    expect(chip('Runway show').attributes('aria-pressed')).toBe('true');
  });

  it('writes the region, the search, the sort and the picks behind Filters under the kit’s keys, and leaves the other keys', async () => {
    await render('editor', undefined, 'tab=x');

    await chip('Central Asia').trigger('click');
    await choose({ scope: 'central-asia', kind: ['market', 'runway'], city: ['almaty'], stage: ['candidate'] });
    await wrapper!.get('input[aria-label="Search events"]').setValue('silk');
    await wrapper!.get('select[name="sort"]').setValue('added');
    await flushPromises();

    expect(query()).toEqual({
      tab: 'x',
      regions: 'central-asia',
      kinds: ['market', 'runway'],
      cities: ['almaty'],
      stages: ['candidate'],
      q: 'silk',
      sort: 'added',
    });
  });

  it('leaves out of the address what is no choice: no kinds, no search, the default sort and the All region', async () => {
    await render('editor');
    await chip('Runway show').trigger('click');
    await wrapper!.get('select[name="sort"]').setValue('deadline');
    await flushPromises();
    expect(query().kinds).toEqual(['runway']);
    expect(query().sort).toBe('deadline');

    await chip('Runway show').trigger('click');
    await wrapper!.get('select[name="sort"]').setValue('starts');
    await flushPromises();

    expect(query()).toEqual({});
  });

  it('opens on the filters the address holds and lists the feed with them', async () => {
    await render('editor', undefined, 'regions=central-asia&kinds=trade-fair&cities=almaty&q=silk&sort=added');

    expect(chip('Central Asia').attributes('aria-pressed')).toBe('true');
    expect(chip('Trade fair').attributes('aria-pressed')).toBe('true');
    expect((wrapper!.get('input[aria-label="Search events"]').element as HTMLInputElement).value).toBe('silk');
    expect((wrapper!.get('select[name="sort"]').element as HTMLSelectElement).value).toBe('added');
    expect(wrapper!.get('[data-facet-chips]').text()).toContain('City: Almaty');
    const read = lastList();
    expect(read.getAll('countries')).toEqual(['KZ', 'KG', 'TJ', 'TM']);
    expect(read.getAll('kinds')).toEqual(['trade-fair']);
    expect(read.getAll('cities')).toEqual(['almaty']);
    expect(read.get('text')).toBe('silk');
    expect(read.get('sort')).toBe('added');
  });

  it('follows the address on back and forward, and reads a region that is none as no region', async () => {
    await render('editor');

    await router.replace({ query: { kinds: 'runway', regions: 'central-asia' } });
    await flushPromises();
    expect(chip('Runway show').attributes('aria-pressed')).toBe('true');
    expect(chip('Central Asia').attributes('aria-pressed')).toBe('true');
    expect(lastList().getAll('kinds')).toEqual(['runway']);

    await router.replace({ query: { regions: 'nowhere' } });
    await flushPromises();
    expect(chip('Runway show').attributes('aria-pressed')).toBe('false');
    expect(chip('Central Asia').attributes('aria-pressed')).toBe('false');
    expect(lastList().has('kinds')).toBe(false);
    expect(lastList().has('countries')).toBe(false);
  });

  it('keeps a search typed with a trailing space as typed while the address holds it trimmed', async () => {
    await render('editor');

    await wrapper!.get('input[aria-label="Search events"]').setValue('silk ');
    await flushPromises();

    expect(query().q).toBe('silk');
    expect((wrapper!.get('input[aria-label="Search events"]').element as HTMLInputElement).value).toBe('silk ');
  });

  it('keeps the filters in the address while an event is open and when it is closed again', async () => {
    await render('editor', undefined, 'kinds=runway');
    await pill('deadlines').trigger('click');
    await flushPromises();

    await wrapper!.get('[data-test="deadline"] a').trigger('click');
    await flushPromises();

    expect(router.currentRoute.value.path).toBe(`/organizations/${ORG}/events/${FASHION_WEEK}`);
    expect(query().kinds).toBe('runway');
    // The feed stays mounted behind the event, so the card and its choice are still there when it is closed.
    await wrapper!.get('[data-test="back-to-feed"]').trigger('click');
    await flushPromises();
    expect(router.currentRoute.value.path).toBe(`/organizations/${ORG}/events`);
    expect(query().kinds).toBe('runway');
    expect(chip('Runway show').attributes('aria-pressed')).toBe('true');
  });
});

/** A confirmed event that ended on 20 September: past, and still to close (OE12). */
const endedItem = () => ({
  event: event(
    FASHION_WEEK,
    { en: 'Tashkent Fashion Week' },
    { city: 'Tashkent', country: 'UZ' },
    { value: '2026-09-20', precision: 'day' },
  ),
  tracking: { ...fashionWeekTracking(), stage: 'confirmed' },
});
const deadlineSummaries = () =>
  sent.filter(
    (item) => item.url.pathname === `${CATALOG}/tracking/summary` && item.url.searchParams.has('deadlineKinds'),
  );
const pastReads = () =>
  sent.filter((item) => item.url.pathname === `${CATALOG}/tracking` && item.url.searchParams.has('stages'));
const pill = (name: 'deadlines' | 'past') => wrapper!.find(`[data-pill="${name}"]`);

describe('attention pills between the facet bar and the cards', () => {
  it('shows the kit’s deadlines pill and Ocharo’s past pill with their counts, and loads no list until asked', async () => {
    pastOpen = [endedItem()];
    await render('editor');

    expect(pill('deadlines').text()).toBe('1 deadline in 14 days');
    expect(pill('deadlines').attributes('aria-expanded')).toBe('false');
    expect(pill('past').text()).toBe('1 past to close');
    expect(pill('past').attributes('aria-expanded')).toBe('false');
    expect(wrapper!.find('[data-test="attention-panel"]').exists()).toBe(false);
    expect(wrapper!.find('[data-test="deadline"]').exists()).toBe(false);
    expect(wrapper!.find('[data-test="past-open"]').exists()).toBe(false);
    // The pills sit under the filter card and ahead of the cards.
    const toolbar = filterCard().element;
    const pills = wrapper!.get('[data-test="attention-pills"]').element;
    const cards = wrapper!.get('[data-test="event-cards"]').element;
    expect(toolbar.compareDocumentPosition(pills) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(pills.compareDocumentPosition(cards) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('counts the nearest application deadlines from the summary the feed reads, today as the day', async () => {
    await render('editor');

    expect(deadlineSummaries().length).toBeGreaterThan(0);
    expect(deadlineSummaries()[0]!.url.searchParams.getAll('deadlineKinds')).toEqual(['application']);
    expect(deadlineSummaries()[0]!.url.searchParams.get('today')).toBe('2026-10-08');
  });

  it('unfolds the nearest deadlines on click, each linked to its event page, and folds them again', async () => {
    await render('editor');

    await pill('deadlines').trigger('click');
    await flushPromises();
    expect(pill('deadlines').attributes('aria-expanded')).toBe('true');
    const deadline = wrapper!.get('[data-test="deadline"]');
    expect(deadline.text()).toContain('Tashkent Fashion Week');
    expect(deadline.get('a').attributes('href')).toBe(`/organizations/${ORG}/events/${FASHION_WEEK}`);

    await pill('deadlines').trigger('click');
    expect(pill('deadlines').attributes('aria-expanded')).toBe('false');
    expect(wrapper!.find('[data-test="deadline"]').exists()).toBe(false);
  });

  it('opens an event from a deadline inside the page', async () => {
    await render('editor');
    await pill('deadlines').trigger('click');
    await flushPromises();

    await wrapper!.get('[data-test="deadline"] a').trigger('click');
    await flushPromises();

    expect(wrapper!.find('[data-test="back-to-feed"]').exists()).toBe(true);
    expect(wrapper!.find('article[aria-labelledby="event-detail-title"]').exists()).toBe(true);
  });

  it('reads the past events by stage and last start day, once, and names the pill with its count', async () => {
    pastOpen = [endedItem()];
    await render('editor');

    expect(pastReads()).toHaveLength(1);
    expect(pastReads()[0]!.url.searchParams.getAll('stages')).toEqual([
      'shortlisted',
      'applying',
      'applied',
      'confirmed',
    ]);
    expect(pastReads()[0]!.url.searchParams.get('to')).toBe('2026-10-07');
    expect(pastReads()[0]!.url.searchParams.get('includeUndated')).toBe('false');
    expect(pill('past').text()).toBe('1 past to close');
  });

  it('unfolds the past events in the slot, closes one in a tracking write, drops its pill and reads the feed again', async () => {
    pastOpen = [endedItem()];
    await render('editor');

    await pill('past').trigger('click');
    expect(pill('past').attributes('aria-expanded')).toBe('true');
    const strip = wrapper!.get('[data-test="past-open"]');
    expect(strip.text()).toContain('Tashkent Fashion Week');
    const before = lists().length;
    const summaries = deadlineSummaries().length;

    await strip.get('[data-test="past-open-attended"]').trigger('click');
    await flushPromises();

    const writes = sent.filter((item) => item.method === 'PUT');
    expect(writes).toHaveLength(1);
    expect(writes[0]!.body).toMatchObject({ stage: 'attended', expectedRevision: 3 });
    expect(wrapper!.find('[data-test="past-open"]').exists()).toBe(false);
    expect(pill('past').exists()).toBe(false);
    expect(lists().length).toBe(before + 1);
    expect(deadlineSummaries().length).toBe(summaries + 1);
  });

  it('shows no pill while no deadline is near and no past event waits', async () => {
    deadlines = [];
    await render('editor');

    expect(wrapper!.find('[data-test="attention-pills"]').exists()).toBe(false);
    expect(pill('deadlines').exists()).toBe(false);
    expect(pill('past').exists()).toBe(false);
    expect(wrapper!.findAll('[data-test="event-media-card"]')).toHaveLength(2);
  });

  it('reads the past events again after a write in the feed', async () => {
    await render('editor');
    const past = pastReads().length;
    const summaries = deadlineSummaries().length;

    await pickStage('Silk Road Trade Fair', 'shortlisted');

    expect(pastReads().length).toBe(past + 1);
    expect(deadlineSummaries().length).toBe(summaries + 1);
  });

  it('offers a viewer the past events to read but no stage action', async () => {
    pastOpen = [endedItem()];
    await render('viewer');

    await pill('past').trigger('click');
    expect(wrapper!.get('[data-test="past-open"]').text()).toContain('Tashkent Fashion Week');
    expect(wrapper!.find('[data-test="past-open-attended"]').exists()).toBe(false);
  });
});

describe('one event through the fe-events kit', () => {
  it('shows the event’s facts and sources and no tracking section: no stage, priority, notes, fields or history', async () => {
    await render('editor', { view: 'event', eventId: FASHION_WEEK });

    const detail = wrapper!.get('article[aria-labelledby="event-detail-title"]');
    expect(detail.get('#event-detail-title').text()).toBe('Tashkent Fashion Week');
    expect(wrapper!.find('section[aria-labelledby="tracking-form-title"]').exists()).toBe(false);
    for (const selector of [
      '[name="stage"]',
      '[name="priority"]',
      'textarea[name="notes"]',
      '[name="field-cost"]',
      '[data-test="tracking-read-only"]',
      '[data-test="event-field-cost"]',
      '[data-test="event-field-participation"]',
    ])
      expect(wrapper!.find(selector).exists(), selector).toBe(false);
    expect(wrapper!.text()).not.toContain('Bring the lookbook.');
    expect(wrapper!.text()).not.toContain('Stand from 300 USD');
    // The page reads the catalog's event alone: no tracking read, no history, nothing written.
    expect(sent.some((item) => item.url.pathname === `${CATALOG}/events/${FASHION_WEEK}`)).toBe(true);
    expect(sent.some((item) => item.url.pathname.startsWith(`${CATALOG}/tracking/${FASHION_WEEK}`))).toBe(false);
    expect(sent.filter((item) => item.method !== 'GET')).toEqual([]);
  });
});

const workflowWrites = () => sent.filter((item) => item.method === 'PUT' && item.url.pathname === `${MIRROR}/workflow`);

describe('event images through the fe-events kit', () => {
  const SOURCE = 'https://source.example.test/runway.jpg';
  const POSTER = 'https://source.example.test/poster.jpg';
  const COPY = '0197a8a0-1111-7aaa-8bbb-000000000001';
  /** The backend route of one image's stored copy, as the page resolves it against its own origin. */
  const route = (index: number) =>
    new URL(`${CATALOG}/events/${FASHION_WEEK}/images/${index}`, window.location.origin).href;
  /** Sets the runway show's images and lists it again, as a read after Events changed them would. */
  const showImages = (images: object[]) => {
    fashionWeekImages = images;
    listed = [
      { event: fashionWeek(), tracking: fashionWeekTracking() },
      { event: silkFair(), tracking: null },
    ];
  };

  it("shows SCFD's stored copy through the backend route on a card whose image has a fileRef", async () => {
    showImages([{ url: SOURCE, fileRef: COPY }]);
    await render('editor');

    const image = card('Tashkent Fashion Week').get('[data-test="event-image"]');
    expect(image.attributes('src')).toBe(route(0));
    expect(image.attributes('src')).not.toContain(SOURCE);
    expect(image.attributes('src')).not.toContain(COPY);
    expect(image.attributes('referrerpolicy')).toBe('no-referrer');
    expect(image.attributes('loading')).toBe('lazy');
    expect(image.attributes('alt')).toBe('Tashkent Fashion Week');
    expect(card('Tashkent Fashion Week').find('[data-test="event-date-tile"]').exists()).toBe(false);
  });

  it('keeps the date tile on a card whose event has no image', async () => {
    showImages([{ url: SOURCE, fileRef: COPY }]);
    await render('editor');

    const untracked = card('Silk Road Trade Fair');
    expect(untracked.find('[data-test="event-image"]').exists()).toBe(false);
    expect(untracked.find('[data-test="event-date-tile"]').exists()).toBe(true);
  });

  it("keeps the kit's own image, the source link, on a card whose image has no stored copy", async () => {
    showImages([{ url: SOURCE }]);
    await render('editor');

    expect(card('Tashkent Fashion Week').get('[data-test="event-image"]').attributes('src')).toBe(SOURCE);
  });

  it('falls back to the source link, then the date tile, when the stored copy fails to load', async () => {
    showImages([{ url: SOURCE, fileRef: COPY }]);
    await render('editor');

    await card('Tashkent Fashion Week').get('[data-test="event-image"]').trigger('error');
    await flushPromises();

    expect(card('Tashkent Fashion Week').get('[data-test="event-image"]').attributes('src')).toBe(SOURCE);
    await card('Tashkent Fashion Week').get('[data-test="event-image"]').trigger('error');
    await flushPromises();

    expect(card('Tashkent Fashion Week').find('[data-test="event-image"]').exists()).toBe(false);
    expect(card('Tashkent Fashion Week').find('[data-test="event-date-tile"]').exists()).toBe(true);
  });

  it("falls back to the kit's date tile when a source image fails to load, as before", async () => {
    showImages([{ url: SOURCE }]);
    await render('editor');

    await card('Tashkent Fashion Week').get('[data-test="event-image"]').trigger('error');
    await flushPromises();

    expect(card('Tashkent Fashion Week').find('[data-test="event-date-tile"]').exists()).toBe(true);
  });

  it("shows each stored copy in the event's detail through the route of its own place, and a source image as it is", async () => {
    fashionWeekImages = [{ url: POSTER }, { url: SOURCE, fileRef: COPY }];
    await render('editor', { view: 'event', eventId: FASHION_WEEK });

    const sources = wrapper!.findAll('[data-test="event-images"] img').map((image) => image.attributes('src'));
    expect(sources).toEqual([POSTER, route(1)]);
  });
});

describe("Ocharo's workflow in Events", () => {
  it('writes nothing on a visit, whatever the stored workflow holds and whoever visits', async () => {
    const stored = [
      { ...ocharoWorkflowSpec(), revision: 2 },
      {
        stages: ocharoWorkflowSpec().stages.map((stage) => ({
          key: stage.key,
          class: stage.key === 'rejected' ? 'lost' : 'open',
        })),
        defaultStage: 'candidate',
        revision: 2,
      },
      { stages: [{ key: 'candidate' }], defaultStage: 'candidate', revision: 1 },
      { stages: [{ key: 'interested' }], defaultStage: 'interested', revision: 0 },
    ];
    for (const role of ['owner', 'editor', 'viewer'] as const) {
      for (const workflow of stored) {
        storedWorkflow = workflow;
        await render(role);
        wrapper!.unmount();
        await render(role, { view: 'event', eventId: FASHION_WEEK });
        wrapper!.unmount();
        wrapper = undefined;
      }
    }

    expect(workflowWrites()).toEqual([]);
    expect(sent.filter((item) => item.method !== 'GET' && item.method !== 'HEAD')).toEqual([]);
  });

  it("shows Ocharo's stage labels from the spec while Events holds only its default workflow", async () => {
    storedWorkflow = {
      stages: [{ key: 'interested', label: { en: 'Interested' }, class: 'open' }],
      defaultStage: 'interested',
      revision: 0,
    };
    await render('owner');

    const label = (key: string) => ocharoWorkflowSpec().stages.find((stage) => stage.key === key)!.label!.en!;
    expect(facetOf('stage')!.options.map((option) => option.label)).toEqual(
      ['candidate', 'shortlisted', 'skipped', 'rejected'].map(label),
    );
    expect(card('Tashkent Fashion Week').get('[data-test="event-stage"]').text().toLowerCase()).toBe(
      label('shortlisted').toLowerCase(),
    );
    expect(workflowWrites()).toEqual([]);
  });
});
