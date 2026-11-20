import { computed, ref } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { EventsAccess, EventsWorkspace } from '@/application/events/UseEventsAccess';
import { ocharoWorkflowSpec, type EventsLocation } from '@/domain/events/Events';
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
/** A runway show Ocharo shortlisted, titled by its source in Russian and by SCFD's machine English (OE06). */
const fashionWeek = () =>
  event(
    FASHION_WEEK,
    { 'ru-Cyrl': 'Неделя моды в Ташкенте', en: 'Tashkent Fashion Week' },
    { city: 'Tashkent', country: 'UZ' },
    { value: '2026-10-12', precision: 'day' },
  );
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

const ok = (data: unknown, status = 200) => Response.json({ data }, { status });
function answer(method: string, url: URL, body: unknown): Response {
  const path = url.pathname;
  if (path === `${MIRROR}/workflow`)
    return ok(method === 'PUT' ? { ...(body as object), revision: 3 } : storedWorkflow);
  if (path === `${CATALOG}/tracking`)
    return ok({ items: url.searchParams.has('stages') ? pastOpen : listed, nextCursor: null });
  if (path === `${CATALOG}/tracking/summary`)
    return ok({
      byStage: { shortlisted: 1 },
      untracked: 1,
      upcomingDeadlines: deadlines,
    });
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
) {
  const access = {
    busy: ref(false),
    dirty: ref(false),
    setDirty: vi.fn((value: boolean) => {
      access.dirty.value = value;
    }),
  };
  const workspace = {
    organization: ref({ id: ORG, name: 'Ocharo' }),
    canEdit: computed(() => role !== 'viewer'),
    canAdmin: computed(() => role === 'owner'),
  };
  wrapper = mount(EventsPage, {
    props: {
      access: access as unknown as EventsAccess,
      workspace: workspace as unknown as EventsWorkspace,
      initialLocation,
    },
    global: {
      plugins: [
        await eventsRouter(
          initialLocation.view === 'event'
            ? `/organizations/${ORG}/events/${initialLocation.eventId}`
            : `/organizations/${ORG}/events`,
        ),
      ],
    },
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
  wrapper!.findAll('[data-test="event-card"]').find((item) => item.text().includes(title))!;
const button = (scope: VueWrapper | ReturnType<VueWrapper['get']>, label: string) =>
  scope.findAll('button').find((item) => item.text().trim() === label)!;

describe('feed header', () => {
  it('is the title alone: no eyebrow, no description, no Telegram delivery link, no freshness line', async () => {
    await render('owner');

    const header = wrapper!.get('header');
    expect(header.findAll('h1')).toHaveLength(1);
    expect(header.get('h1').text()).toBe('Feed');
    expect(header.element.children).toHaveLength(1);
    expect(wrapper!.find('.eyebrow').exists()).toBe(false);
    expect(wrapper!.text()).not.toContain('Showcase events');
    expect(wrapper!.text()).not.toContain('Events records decisions');
    expect(wrapper!.find('[data-test="telegram-delivery-link"]').exists()).toBe(false);
    expect(wrapper!.find('[data-test="sources-freshness"]').exists()).toBe(false);
    expect(sent.some((item) => item.url.pathname === `${CATALOG}/freshness`)).toBe(false);
    expect(wrapper!.findAll('[data-test="event-card"]')).toHaveLength(2);
  });
});

describe('events feed through the fe-events kit', () => {
  it('lists the tracking feed with one mirrored request: skipped and rejected left out, filters and paging server-side', async () => {
    await render('editor');

    expect(lists()).toHaveLength(1);
    const query = lastList();
    expect(query.getAll('excludeStages')).toEqual(['skipped', 'rejected']);
    expect(query.get('from')).toBe('2026-10-08');
    expect(query.get('limit')).toBe('50');
    expect(query.has('offset')).toBe(false);
    expect(wrapper!.findAll('[data-test="event-card"]')).toHaveLength(2);

    const shortlisted = card('Tashkent Fashion Week');
    expect(shortlisted.get('[data-test="event-stage"]').text()).toBe('Shortlisted');
    expect(shortlisted.get('[data-test="event-field-cost"]').text()).toContain('Stand from 300 USD');
    expect(shortlisted.get('[data-test="event-scope"]').text()).toBe('Local');
    const untracked = card('Silk Road Trade Fair');
    expect(untracked.find('[data-test="event-untracked"]').exists()).toBe(true);
    expect(untracked.get('[data-test="event-field-cost"]').text()).toBe('Cost not stated');
    expect(untracked.get('[data-test="event-scope"]').text()).toBe('Regional');
    expect(untracked.get('[data-test="event-quick-stages"]').text()).toContain('Shortlisted');
  });

  it('shows a title in the interface language, SCFD’s English for an English reader, with the language badge otherwise', async () => {
    await render('editor');
    expect(card('Tashkent Fashion Week').find('[data-test="title-language"]').exists()).toBe(false);

    useUiLocale().setUiLocale('ru');
    await flushPromises();
    expect(card('Неделя моды в Ташкенте').find('[data-test="title-language"]').exists()).toBe(false);

    useUiLocale().setUiLocale('uz');
    await flushPromises();
    expect(card('Tashkent Fashion Week').get('[data-test="title-language"]').text()).toBe('EN');
  });

  it('lists skipped and rejected events when asked, and filters by scope chips on the server', async () => {
    await render('editor');

    await wrapper!.get('input[name="showHidden"]').setValue(true);
    await flushPromises();
    expect(lastList().has('excludeStages')).toBe(false);

    await wrapper!.get('[data-region="regional"]').trigger('click');
    await flushPromises();
    expect(lastList().getAll('countries')).toEqual(['KZ', 'KG', 'TJ', 'TM', 'AF', 'AZ', 'RU']);

    await wrapper!.get('[data-region="international"]').trigger('click');
    await flushPromises();
    expect(lastList().getAll('excludeCountries')).toEqual(['UZ', 'KZ', 'KG', 'TJ', 'TM', 'AF', 'AZ', 'RU']);
    expect(lastList().has('countries')).toBe(false);
  });

  it('shortlists an untracked event with one tracking write, then reads the feed again', async () => {
    await render('editor');
    const before = lists().length;

    await button(card('Silk Road Trade Fair'), 'Shortlisted').trigger('click');
    await flushPromises();

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
    await wrapper!.get('input[name="country"]').setValue('UZ');
    expect(wrapper!.get('[data-test="facts-scope"]').text()).toContain('Local');
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

    expect(wrapper!.find('[data-test="event-quick-stages"]').exists()).toBe(false);
    expect(wrapper!.findAll('button').some((item) => item.text().trim() === 'Propose an event')).toBe(false);
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
const pill = (name: 'deadlines' | 'past') => wrapper!.find(`[data-test="attention-${name}-pill"]`);

describe('attention pills under the title', () => {
  it('folds the deadlines and the past events behind two pills with their counts, and loads no list until asked', async () => {
    pastOpen = [endedItem()];
    await render('editor');

    expect(pill('deadlines').text()).toContain('Deadlines');
    expect(pill('deadlines').get('[data-test="attention-count"]').text()).toBe('1');
    expect(pill('deadlines').attributes('aria-expanded')).toBe('false');
    expect(pill('past').text()).toContain('Past to close');
    expect(pill('past').get('[data-test="attention-count"]').text()).toBe('1');
    expect(pill('past').attributes('aria-expanded')).toBe('false');
    // The pills sit under the title, ahead of the feed.
    const order = wrapper!.get('[data-test="events-feed"]').element.children;
    expect(order[0]!.getAttribute('data-test')).toBe('attention');
    expect(wrapper!.find('[data-test="deadline"]').exists()).toBe(false);
    expect(wrapper!.get('#attention-past').isVisible()).toBe(false);
  });

  it('counts the nearest application deadlines from the summary the list reads, today as the day', async () => {
    await render('editor');

    expect(deadlineSummaries()).toHaveLength(1);
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

  it('unfolds the past events, closes one in a tracking write, hides its pill and reads the feed again', async () => {
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

    expect(pill('deadlines').exists()).toBe(false);
    expect(pill('past').exists()).toBe(false);
    expect(wrapper!.find('[data-test="attention-count"]').exists()).toBe(false);
    expect(wrapper!.findAll('[data-test="event-card"]')).toHaveLength(2);
  });

  it('reads both counts again after a write in the feed', async () => {
    await render('editor');
    const summaries = deadlineSummaries().length;
    const past = pastReads().length;

    await button(card('Silk Road Trade Fair'), 'Shortlisted').trigger('click');
    await flushPromises();

    expect(deadlineSummaries().length).toBe(summaries + 1);
    expect(pastReads().length).toBe(past + 1);
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
  it('shows the event with Ocharo’s fields and saves tracking at the revision read, guarding the unsaved draft', async () => {
    const { access } = await render('editor', { view: 'event', eventId: FASHION_WEEK });

    const detail = wrapper!.get('article[aria-labelledby="event-detail-title"]');
    expect(detail.get('[data-test="event-field-participation"]').text()).toContain('Application package');
    expect(detail.get('[data-test="event-field-cost"]').text()).toContain('Stand from 300 USD');
    // The Desk import's source link is shown, never offered for editing (read-only in Ocharo's workflow).
    expect(wrapper!.get('[data-test="tracking-read-only"]').text()).toContain('https://listings.example.test/tfw');
    expect(wrapper!.find('[name="field-sourceUrl"]').exists()).toBe(false);
    const listsBefore = lists().length;

    await wrapper!.get('textarea[name="notes"]').setValue('Bring the lookbook and the ikat coats.');
    expect(access.setDirty).toHaveBeenLastCalledWith(true);
    await wrapper!.get('section[aria-labelledby="tracking-form-title"] form').trigger('submit');
    await flushPromises();

    const save = sent.find(
      (item) => item.method === 'PUT' && item.url.pathname === `${CATALOG}/tracking/${FASHION_WEEK}`,
    )!;
    expect(save.body).toMatchObject({
      stage: 'shortlisted',
      priority: 'high',
      notes: 'Bring the lookbook and the ikat coats.',
      fields: { cost: 'Stand from 300 USD', sourceUrl: 'https://listings.example.test/tfw' },
      expectedRevision: 3,
    });
    expect(access.setDirty).toHaveBeenLastCalledWith(false);
    // The feed behind the detail reads again, so the event's new stage shows on return.
    expect(lists().length).toBe(listsBefore + 1);
  });
});

const workflowWrites = () => sent.filter((item) => item.method === 'PUT' && item.url.pathname === `${MIRROR}/workflow`);

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

  it("shows Ocharo's stage labels and field titles from the spec while Events holds only its default workflow", async () => {
    useUiLocale().setUiLocale('uz');
    storedWorkflow = {
      stages: [{ key: 'interested', label: { en: 'Interested' }, class: 'open' }],
      defaultStage: 'interested',
      revision: 0,
    };
    await render('owner', { view: 'event', eventId: FASHION_WEEK });

    const stages = wrapper!
      .findAll('section[aria-labelledby="tracking-form-title"] select[name="stage"] option')
      .map((option) => option.text());
    expect(stages).toEqual(ocharoWorkflowSpec().stages.map((stage) => stage.label!.uz));
    const label = wrapper!.get('[name="field-cost"]').element.closest('label')?.textContent ?? '';
    expect(label).toContain('Ocharo xarajati');
    expect(workflowWrites()).toEqual([]);
  });

  it("shows the stored workflow's own labels, with Ocharo's filling the languages it lacks", async () => {
    useUiLocale().setUiLocale('uz');
    storedWorkflow = {
      ...ocharoWorkflowSpec(),
      stages: ocharoWorkflowSpec().stages.map((stage) => ({
        ...stage,
        label: stage.key === 'shortlisted' ? { en: 'Short list' } : { en: stage.label!.en },
      })),
      revision: 5,
    };
    await render('editor', { view: 'event', eventId: FASHION_WEEK });

    const stages = wrapper!
      .findAll('section[aria-labelledby="tracking-form-title"] select[name="stage"] option')
      .map((option) => option.text());
    expect(stages).toEqual(ocharoWorkflowSpec().stages.map((stage) => stage.label!.uz));
    expect(workflowWrites()).toEqual([]);
  });
});
