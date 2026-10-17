import { deskSelect, chooseDeskValue } from './helpers/DeskSelect';
import { computed, ref } from 'vue';
import { flushPromises, mount, type DOMWrapper, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiFailureFactory } from '@wow-two-beta/fe/foundation/http';
import { DefaultRetryPolicy } from '@wow-two-beta/fe/foundation/resilience';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import { createQueryClient, queryPlugin } from '@wow-two-beta/fe/query';

import { feedbackBus, resolveConfirmation } from '@/application/shared/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
import { eventInput, type EventsLocation, type ShowcaseEvent } from '@/domain/workspace/Events';
import EventsPage from '@/presentation/workspace/EventsPage.vue';

const api = vi.hoisted(() => ({
  listEvents: vi.fn(), summary: vi.fn(), createEvent: vi.fn(), getEvent: vi.fn(), updateEvent: vi.fn(), deleteEvent: vi.fn(),
}));
vi.mock('@/integration/workspace/EventsApi', () => ({ createEventsApi: () => api }));

const ORG = '11111111-1111-4111-8111-111111111111';
const FASHION_WEEK = '22222222-2222-4222-8222-222222222222';
const SILK_FAIR = '33333333-3333-4333-8333-333333333333';
const CRAFT_EXPO = '44444444-4444-4444-8444-444444444444';
const NEW_EVENT = '55555555-5555-4555-8555-555555555555';
const at = '2026-10-01T00:00:00Z';
const showcase = (over: Partial<ShowcaseEvent>): ShowcaseEvent => ({
  id: FASHION_WEEK, key: 'tfw-2026', title: 'Tashkent Fashion Week', organizer: 'Fashion Council', scope: 'local', format: 'runway',
  country: 'UZ', city: 'Tashkent', venue: 'Humo Arena', startsOn: '2026-10-12', endsOn: '2026-10-14', datePrecision: 'day',
  applicationDeadline: '2026-10-15', participation: 'Application package to the organiser', cost: null,
  website: 'https://tfw.example.test/', sourceUrl: 'https://listings.example.test/tfw', status: 'candidate', priority: 'high',
  notes: 'Ikat coats fit the second day.\nBring the lookbook.', revision: 3, createdAt: at, updatedAt: at, ...over,
});
const fashionWeek = () => showcase({});
const silkFair = () => showcase({
  id: SILK_FAIR, key: null, title: 'Silk Road Trade Fair', organizer: null, scope: 'regional', format: 'trade-fair', country: 'KZ', city: 'Almaty',
  venue: null, startsOn: '2026-11-01', endsOn: null, datePrecision: 'month', applicationDeadline: '2026-11-30', cost: 'Stand from 300 USD',
  status: 'shortlisted', priority: 'normal', revision: 1,
});
const craftExpo = () => showcase({
  id: CRAFT_EXPO, key: null, title: 'Craft Expo', scope: 'international', format: 'exhibition', country: null, city: null, venue: null,
  startsOn: null, endsOn: null, datePrecision: 'unknown', applicationDeadline: null, status: 'applying', priority: 'low', revision: 2,
});

let wrapper: VueWrapper | undefined;
let client: ReturnType<typeof createQueryClient> | undefined;
beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(2026, 9, 8, 10, 0));
  useUiLocale().setUiLocale('en');
  window.history.replaceState(null, '', '/');
  api.listEvents.mockResolvedValue(ResultExtensions.ok([fashionWeek(), silkFair(), craftExpo()]));
  api.summary.mockResolvedValue(ResultExtensions.ok({ byStatus: { candidate: 4, shortlisted: 2, applying: 1 }, upcomingDeadlines: [fashionWeek(), silkFair()] }));
  api.getEvent.mockImplementation((_o: string, id: string) => ResultExtensions.ok([fashionWeek(), silkFair(), craftExpo()].find(item => item.id === id)));
});
afterEach(() => {
  wrapper?.unmount();
  wrapper = undefined;
  client?.clear();
  resolveConfirmation(false);
  vi.useRealTimers();
});

function render(role: 'owner' | 'editor' | 'viewer', initialLocation: EventsLocation) {
  client = createQueryClient({ retry: { ...DefaultRetryPolicy, maxRetries: 0 } });
  const access = {
    busy: ref(false), dirty: ref(false), session: ref({ csrfToken: 'token', sessionId: 'session' }),
    setDirty: vi.fn((value: boolean) => { access.dirty.value = value; }), recheck: vi.fn(),
    write: vi.fn((operation: (signal: AbortSignal) => unknown) => operation(new AbortController().signal)),
  };
  const workspace = { organization: ref({ id: ORG, name: 'Ocharo', slug: 'ocharo', role }), canEdit: computed(() => role !== 'viewer') };
  wrapper = mount(EventsPage, {
    props: { access: access as unknown as AccessControls, workspace: workspace as unknown as WorkspaceControls, initialLocation },
    global: { plugins: [queryPlugin(client)] },
    attachTo: document.body,
  });
  return { access };
}
const feed: EventsLocation = { view: 'feed', eventId: null };
const card = (title: string) => wrapper!.findAll('article.event-card').find(item => item.text().includes(title))!;
const buttonIn = (scope: { findAll(selector: string): DOMWrapper<Element>[] }, label: string) => scope.findAll('button').find(item => item.text() === label);
const detail = () => wrapper!.get('article[aria-labelledby="event-detail-title"]');
const unfiltered = { query: '', scope: '', format: '', status: '', from: '2026-10-08', limit: 200, offset: 0 };

describe('events feed', () => {
  it('shows the next deadlines, month groups and each card as precisely as announced', async () => {
    render('editor', feed);
    await flushPromises();
    expect(api.summary).toHaveBeenCalledWith(ORG, '2026-10-08', expect.any(AbortSignal));
    expect(api.listEvents).toHaveBeenCalledWith(ORG, unfiltered, expect.any(AbortSignal));

    const deadlines = wrapper!.findAll('[data-test="deadline"]');
    expect(deadlines.map(item => item.get('a').text())).toEqual(['Tashkent Fashion Week', 'Silk Road Trade Fair']);
    expect(deadlines[0]!.text()).toContain('Apply by 15 Oct 2026');
    expect(deadlines[0]!.get('.events-days').attributes('data-tone')).toBe('urgent');
    expect(deadlines[0]!.get('.events-days').text()).toBe('7 days left');
    expect(deadlines[1]!.get('.events-days').attributes('data-tone')).toBe('upcoming');
    expect(deadlines[1]!.text()).toContain('Shortlisted');

    expect(wrapper!.findAll('[data-test="event-group"] h3').map(item => item.text())).toEqual(['October 2026', 'November 2026', 'Date not announced']);
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

    expect(deskSelect(wrapper!, 'Status').props('options').map((option: { label: string }) => option.label))
      .toEqual(['All statuses (7)', 'Candidate (4)', 'Shortlisted (2)', 'Applying (1)', 'Applied (0)', 'Confirmed (0)', 'Attended (0)', 'Skipped (0)', 'Rejected (0)']);
    // Desk records decisions only: nothing offers to apply, register, send or pay.
    expect(wrapper!.findAll('button').some(item => /apply|register|send|pay/i.test(item.text()))).toBe(false);
  });

  it('turns every filter into query parameters and drops from=today when past events are shown', async () => {
    render('editor', feed);
    await flushPromises();
    await wrapper!.get('input[type="search"]').setValue('silk');
    await new Promise(resolve => setTimeout(resolve, 300));
    await flushPromises();
    expect(api.listEvents).toHaveBeenLastCalledWith(ORG, { ...unfiltered, query: 'silk' }, expect.any(AbortSignal));

    const chips = wrapper!.findAll('.events-chip');
    expect(chips.map(chip => chip.text())).toEqual(['All', 'Local', 'Regional', 'International']);
    await chips[3]!.trigger('click');
    expect(chips[3]!.attributes('aria-pressed')).toBe('true');
    await chooseDeskValue(wrapper!, 'Format', 'trade-fair');
    await chooseDeskValue(wrapper!, 'Status', 'candidate');
    await flushPromises();
    expect(api.listEvents).toHaveBeenLastCalledWith(ORG,
      { ...unfiltered, query: 'silk', scope: 'international', format: 'trade-fair', status: 'candidate' }, expect.any(AbortSignal));

    await wrapper!.get('.events-toggle input').setValue(true);
    await flushPromises();
    expect(api.listEvents).toHaveBeenLastCalledWith(ORG,
      { ...unfiltered, query: 'silk', scope: 'international', format: 'trade-fair', status: 'candidate', from: null }, expect.any(AbortSignal));
  });

  it('shortlists or skips a candidate with one PUT carrying its revision', async () => {
    render('editor', feed);
    await flushPromises();
    api.updateEvent.mockResolvedValue(ResultExtensions.ok({ ...fashionWeek(), status: 'shortlisted', revision: 4 }));
    expect(buttonIn(card('Silk Road Trade Fair'), 'Shortlist')).toBeUndefined();
    const calls = api.listEvents.mock.calls.length;
    await buttonIn(card('Tashkent Fashion Week'), 'Shortlist')!.trigger('click');
    await flushPromises();
    expect(api.updateEvent).toHaveBeenCalledTimes(1);
    expect(api.updateEvent).toHaveBeenCalledWith(ORG, FASHION_WEEK, 3, { ...eventInput(fashionWeek()), status: 'shortlisted' }, expect.any(AbortSignal));
    expect(api.listEvents.mock.calls.length).toBeGreaterThan(calls);

    await buttonIn(card('Tashkent Fashion Week'), 'Skip')!.trigger('click');
    await flushPromises();
    expect(api.updateEvent).toHaveBeenLastCalledWith(ORG, FASHION_WEEK, 3, { ...eventInput(fashionWeek()), status: 'skipped' }, expect.any(AbortSignal));
  });

  it('creates a manual event as shortlisted by default and opens it', async () => {
    render('editor', feed);
    await flushPromises();
    await buttonIn(wrapper!, 'Add event')!.trigger('click');
    const form = wrapper!.get('section[aria-label="New event"] form');
    await form.get('input').setValue('Bukhara Crafts Market');
    await form.get('input[type="date"]').setValue('2026-12-05');
    api.createEvent.mockResolvedValue(ResultExtensions.ok(showcase({ id: NEW_EVENT, title: 'Bukhara Crafts Market', status: 'shortlisted' })));
    await form.trigger('submit');
    await flushPromises();
    expect(api.createEvent).toHaveBeenCalledWith(ORG, expect.objectContaining({
      title: 'Bukhara Crafts Market', status: 'shortlisted', priority: 'normal', datePrecision: 'day', startsOn: '2026-12-05', endsOn: null, cost: null,
    }), expect.any(AbortSignal));
    expect(window.location.pathname).toBe(`/organizations/${ORG}/events/${NEW_EVENT}`);
  });

  it('opens an event at its own address and returns to the feed', async () => {
    render('editor', feed);
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
    render('editor', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    const text = detail().text();
    for (const value of ['Tashkent Fashion Week', '12–14 Oct 2026', 'Exact dates', 'Humo Arena', 'Application package to the organiser', 'Cost not stated', 'tfw-2026', '15 Oct 2026', '7 days left'])
      expect(text).toContain(value);
    expect(detail().find('dd.whitespace-pre-wrap').exists()).toBe(true);
    const links = detail().findAll('a');
    expect(links.map(link => link.attributes('href'))).toEqual(['https://listings.example.test/tfw']);
    expect(links.every(link => link.attributes('target') === '_blank' && link.attributes('rel') === 'noopener noreferrer')).toBe(true);
    expect(text).toContain('javascript:alert(1)');
  });

  it('hides every edit control from viewers', async () => {
    render('viewer', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    expect(detail().text()).toContain('Candidate');
    expect(detail().findAll('[role=combobox]')).toHaveLength(0);
    for (const label of ['Edit details', 'Delete event', 'Add event', 'Shortlist', 'Skip']) expect(buttonIn(wrapper!, label)).toBeUndefined();
    await buttonIn(wrapper!, '← All events')!.trigger('click');
    await flushPromises();
    expect(card('Tashkent Fashion Week').text()).toContain('New — review');
    expect(card('Tashkent Fashion Week').findAll('button')).toHaveLength(0);
  });

  it('saves status and priority as soon as they change', async () => {
    render('editor', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    api.updateEvent.mockResolvedValue(ResultExtensions.ok({ ...fashionWeek(), status: 'applying', revision: 4 }));
    await chooseDeskValue(wrapper!.findComponent({ name: 'EventDetail' }), 'Status', 'applying');
    await flushPromises();
    expect(api.updateEvent).toHaveBeenLastCalledWith(ORG, FASHION_WEEK, 3, { ...eventInput(fashionWeek()), status: 'applying' }, expect.any(AbortSignal));
    api.getEvent.mockResolvedValue(ResultExtensions.ok({ ...fashionWeek(), status: 'applying', revision: 4 }));
    await chooseDeskValue(wrapper!.findComponent({ name: 'EventDetail' }), 'Priority', 'low');
    await flushPromises();
    expect(api.updateEvent).toHaveBeenCalledTimes(2);
    expect(api.updateEvent.mock.calls[1]![3]).toMatchObject({ priority: 'low' });
  });

  it('retains the committed status throughout a pending and rejected update', async () => {
    render('editor', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    const eventDetail = wrapper!.findComponent({ name: 'EventDetail' });
    const picker = deskSelect(eventDetail, 'Status');
    let finish!: (value: unknown) => void;
    api.updateEvent.mockImplementationOnce(() => new Promise(resolve => { finish = resolve; }));
    await chooseDeskValue(eventDetail, 'Status', 'applying');
    expect(api.updateEvent).toHaveBeenCalledTimes(1);
    expect(picker.props('modelValue')).toBe('candidate');
    expect(picker.props('disabled')).toBe(true);
    finish(ResultExtensions.fail(ApiFailureFactory.create('http', { status: 409, headers: new Headers() })));
    await flushPromises();
    expect(picker.props('modelValue')).toBe('candidate');
    expect(picker.props('disabled')).toBe(false);
    expect(eventDetail.text()).toContain('This record changed. Reload the latest version before trying again.');
  });

  it('keeps the edited values and offers a reload after a revision conflict', async () => {
    const { access } = render('editor', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    await buttonIn(detail(), 'Edit details')!.trigger('click');
    const title = detail().get('form input');
    await title.setValue('Tashkent Fashion Week 2026');
    expect(access.setDirty).toHaveBeenLastCalledWith(true);
    api.updateEvent.mockResolvedValue(ResultExtensions.fail(ApiFailureFactory.create('http', { status: 409, headers: new Headers() })));
    await detail().get('form').trigger('submit');
    await flushPromises();
    expect(api.updateEvent).toHaveBeenCalledWith(ORG, FASHION_WEEK, 3, expect.objectContaining({ title: 'Tashkent Fashion Week 2026', status: 'candidate' }), expect.any(AbortSignal));
    expect(detail().text()).toContain('This record changed. Your edits are still here.');
    expect((detail().get('form input').element as HTMLInputElement).value).toBe('Tashkent Fashion Week 2026');
    expect(buttonIn(detail(), 'Discard my edits and reload')).toBeDefined();
    expect(access.dirty.value).toBe(true);
  });

  it('rejects an end date before the start date without sending', async () => {
    render('editor', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    await buttonIn(detail(), 'Edit details')!.trigger('click');
    const [, end] = detail().findAll('input[type="date"]');
    await end!.setValue('2026-10-10');
    await detail().get('form').trigger('submit');
    await flushPromises();
    expect(detail().text()).toContain('The end date cannot be before the start date.');
    expect(api.updateEvent).not.toHaveBeenCalled();
  });

  it('deletes only after confirmation and returns to the feed', async () => {
    render('editor', { view: 'event', eventId: SILK_FAIR });
    await flushPromises();
    api.deleteEvent.mockResolvedValue(ResultExtensions.ok({ id: SILK_FAIR }));
    await buttonIn(detail(), 'Delete event')!.trigger('click');
    await flushPromises();
    expect(api.deleteEvent).not.toHaveBeenCalled();
    resolveConfirmation(true);
    await flushPromises();
    expect(api.deleteEvent).toHaveBeenCalledWith(ORG, SILK_FAIR, 1, expect.any(AbortSignal));
    expect(window.location.pathname).toBe(`/organizations/${ORG}/events`);
  });
});


describe('event collection outcomes', () => {
  it('keeps controls visible while matching feed and deadline skeletons load', async () => {
    let finishEvents!: (value: ReturnType<typeof ResultExtensions.ok<ShowcaseEvent[]>>) => void;
    api.listEvents.mockImplementation(() => new Promise(resolve => { finishEvents = resolve; }));
    api.summary.mockImplementation(() => new Promise(() => {}));
    render('viewer', feed);
    await flushPromises();
    expect(wrapper!.findAll('.events-list .event-card')).toHaveLength(3);
    expect(wrapper!.findAll('.events-deadlines .events-deadline')).toHaveLength(3);
    expect(wrapper!.findAll('[aria-busy="true"] [role="status"]').every(item => item.classes().includes('sr-only'))).toBe(true);
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
    render('viewer', feed);
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
    render('viewer', feed);
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
    render('editor', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    await buttonIn(detail(), 'Edit details')!.trigger('click');
    await detail().get('form input').setValue('My draft title');
    api.getEvent.mockResolvedValue(ResultExtensions.ok(showcase({ revision: 4, title: 'Another editor title', notes: 'New note' })));
    await buttonIn(wrapper!, 'Refresh')!.trigger('click');
    await flushPromises();
    expect((detail().get('form input').element as HTMLInputElement).value).toBe('My draft title');
    api.updateEvent.mockResolvedValue(ResultExtensions.fail(ApiFailureFactory.create('http', { status: 409, headers: new Headers() })));
    await detail().get('form').trigger('submit');
    await flushPromises();
    expect(api.updateEvent).toHaveBeenLastCalledWith(ORG, FASHION_WEEK, 3,
      expect.objectContaining({ title: 'My draft title', notes: fashionWeek().notes }), expect.any(AbortSignal));
    expect(detail().text()).toContain('This record changed. Your edits are still here.');
  });

  it('keeps a conflicting draft when reload fails and accepts the new revision only after a successful reload', async () => {
    const { access } = render('editor', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    await buttonIn(detail(), 'Edit details')!.trigger('click');
    await detail().get('form input').setValue('Keep this draft');
    api.updateEvent.mockResolvedValue(ResultExtensions.fail(ApiFailureFactory.create('http', { status: 409, headers: new Headers() })));
    await detail().get('form').trigger('submit');
    await flushPromises();
    api.getEvent.mockResolvedValue(ResultExtensions.fail(ApiFailureFactory.create('http', { status: 503, headers: new Headers() })));
    await buttonIn(detail(), 'Discard my edits and reload')!.trigger('click');
    await flushPromises();
    expect((detail().get('form input').element as HTMLInputElement).value).toBe('Keep this draft');
    expect(access.dirty.value).toBe(true);
    expect(buttonIn(detail(), 'Discard my edits and reload')).toBeDefined();
    api.getEvent.mockResolvedValue(ResultExtensions.ok(showcase({ revision: 4, title: 'Latest confirmed title' })));
    await buttonIn(detail(), 'Discard my edits and reload')!.trigger('click');
    await flushPromises();
    expect((detail().get('form input').element as HTMLInputElement).value).toBe('Latest confirmed title');
    expect(access.dirty.value).toBe(false);
    await detail().get('form input').setValue('Edited after reload');
    await detail().get('form').trigger('submit');
    await flushPromises();
    expect(api.updateEvent).toHaveBeenLastCalledWith(ORG, FASHION_WEEK, 4,
      expect.objectContaining({ title: 'Edited after reload' }), expect.any(AbortSignal));
  });

  it('blocks a submitted form while another command owns the workspace', async () => {
    const { access } = render('editor', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    await buttonIn(detail(), 'Edit details')!.trigger('click');
    await detail().get('form input').setValue('Pending edit');
    access.busy.value = true;
    await flushPromises();
    await detail().get('form').trigger('submit');
    await flushPromises();
    expect(api.updateEvent).not.toHaveBeenCalled();
    expect(access.dirty.value).toBe(true);
  });

  it('shows a neutral missing detail and a working refresh action after a failed initial read', async () => {
    api.getEvent.mockResolvedValue(ResultExtensions.fail(ApiFailureFactory.create('http', { status: 500, headers: new Headers() })));
    render('viewer', { view: 'event', eventId: FASHION_WEEK });
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
    render('viewer', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    expect(detail().text()).toContain('7 days left');
    vi.setSystemTime(new Date(2026, 9, 9, 10, 0));
    window.dispatchEvent(new Event('focus'));
    await flushPromises();
    expect(api.listEvents).toHaveBeenLastCalledWith(ORG, { ...unfiltered, from: '2026-10-09' }, expect.any(AbortSignal));
    expect(api.summary).toHaveBeenLastCalledWith(ORG, '2026-10-09', expect.any(AbortSignal));
    expect(detail().text()).toContain('6 days left');
  });

  it('updates the feed at local midnight without waiting for another interaction', async () => {
    vi.useFakeTimers({ toFake: ['Date', 'setTimeout', 'clearTimeout'] });
    vi.setSystemTime(new Date(2026, 9, 8, 23, 59, 59));
    render('viewer', feed);
    await flushPromises();
    await vi.advanceTimersByTimeAsync(1000);
    await flushPromises();
    expect(api.listEvents).toHaveBeenLastCalledWith(ORG, { ...unfiltered, from: '2026-10-09' }, expect.any(AbortSignal));
  });

  it('does not label a failed filter or deadline refresh as an empty result', async () => {
    api.listEvents.mockResolvedValue(ResultExtensions.ok([]));
    api.summary.mockResolvedValue(ResultExtensions.ok({ byStatus: {}, upcomingDeadlines: [] }));
    render('viewer', feed);
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
    render('editor', { view: 'event', eventId: FASHION_WEEK });
    await flushPromises();
    await buttonIn(detail(), 'Edit details')!.trigger('click');
    const form = detail().get('form');
    await chooseDeskValue(wrapper!, 'Date precision', 'month');
    expect((form.findAll('input[type="month"]')[0]!.element as HTMLInputElement).value).toBe('2026-10');
    await form.findAll('input[type="month"]')[0]!.setValue('2026-11');
    await form.findAll('input[type="month"]')[1]!.setValue('2026-12');
    await chooseDeskValue(wrapper!, 'Date precision', 'day');
    expect(form.findAll('input[type="date"]').slice(0, 2).map(input => (input.element as HTMLInputElement).value))
      .toEqual(['2026-11-01', '2026-12-01']);
    await form.findAll('input[type="date"]')[0]!.setValue('2026-12-17');
    await form.findAll('input[type="date"]')[1]!.setValue('2026-12-19');
    await chooseDeskValue(wrapper!, 'Date precision', 'unknown');
    await chooseDeskValue(wrapper!, 'Date precision', 'estimated');
    expect(form.findAll('input[type="month"]').map(input => (input.element as HTMLInputElement).value))
      .toEqual(['2026-12', '']);
    await chooseDeskValue(wrapper!, 'Date precision', 'day');
    expect(form.findAll('input[type="date"]').slice(0, 2).map(input => (input.element as HTMLInputElement).value))
      .toEqual(['2026-12-17', '2026-12-19']);
    api.updateEvent.mockResolvedValue(ResultExtensions.ok({ ...fashionWeek(), revision: 4 }));
    await form.trigger('submit');
    await flushPromises();
    expect(api.updateEvent).toHaveBeenLastCalledWith(ORG, FASHION_WEEK, 3,
      expect.objectContaining({ startsOn: '2026-12-17', endsOn: '2026-12-19', datePrecision: 'day' }), expect.any(AbortSignal));
  });
});
