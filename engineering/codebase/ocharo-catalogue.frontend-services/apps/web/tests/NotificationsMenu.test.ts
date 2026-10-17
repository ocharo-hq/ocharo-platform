import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createMemoryHistory, createRouter } from 'vue-router';
import { FeedbackToastHost } from '@wow-two-beta/fe/presentation/feedback';
import NotificationsMenu from '../src/presentation/common/NotificationsMenu.vue';
import { feedbackBus, notifyCatalogueSaved, notifyError } from '../src/application/common/UseFeedback';
import { useUiLocale } from '../src/application/shared/UseUiLocale';

const wrappers: VueWrapper[] = [];
async function click(selector: string) {
  await new Promise((resolve) => setTimeout(resolve, 2));
  const element = document.querySelector(selector);
  expect(element, selector).not.toBeNull();
  element!.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
  await flushPromises();
}
async function openMenu() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/:pathMatch(.*)*', component: { template: '<p>Catalogue</p>' } }],
  });
  await router.push('/catalogues');
  await router.isReady();
  wrappers.push(mount(NotificationsMenu, { attachTo: document.body, global: { plugins: [router] } }));
  wrappers.push(
    mount(FeedbackToastHost, {
      props: { bus: feedbackBus, defaultDuration: Infinity },
      attachTo: document.body,
      global: { plugins: [router] },
    }),
  );
  await flushPromises();
  return router;
}
beforeEach(() => {
  feedbackBus.reset();
  useUiLocale().setUiLocale('en');
});
afterEach(() => {
  wrappers.splice(0).forEach((wrapper) => wrapper.unmount());
  feedbackBus.reset();
  document.body.innerHTML = '';
});
describe('local SDK notification adoption', () => {
  it('keeps retained history after toast and panel Close, and removes it only through Delete', async () => {
    await openMenu();
    notifyCatalogueSaved({ id: 'season', revision: 2, title: 'Season collection' }, 'pdf');
    await flushPromises();
    expect(document.querySelector('[aria-label="1 notifications"]')).not.toBeNull();
    await click('[aria-label="Dismiss"]');
    expect(feedbackBus.retained()).toHaveLength(1);
    await click('button[aria-label="Notifications"]');
    expect(document.body.textContent).toContain('Season collection');
    await click('[aria-label="Close notifications"]');
    expect(feedbackBus.retained()).toHaveLength(1);
    await click('button[aria-label="Notifications"]');
    await click('[aria-label="Delete notification"]');
    expect(feedbackBus.retained()).toEqual([]);
    expect(document.body.textContent).toContain('No notifications yet.');
    expect(document.querySelector('[aria-label="1 notifications"]')).toBeNull();
  });
  it('keeps transient errors out of the menu and omits Delete on those toasts', async () => {
    await openMenu();
    notifyError('Unavailable for this test', 'notification-proof-transient');
    await flushPromises();
    expect(document.body.textContent).toContain('Unavailable for this test');
    expect(document.querySelector('[aria-label="Delete notification"]')).toBeNull();
    await click('button[aria-label="Notifications"]');
    expect(document.body.textContent).toContain('No notifications yet.');
    expect(feedbackBus.retained()).toEqual([]);
  });
  it('opens the acknowledged catalogue through its type action without deleting history', async () => {
    const router = await openMenu();
    notifyCatalogueSaved({ id: 'spring', revision: 1, title: 'Spring' }, 'web', true);
    await flushPromises();
    await click('[aria-label="Dismiss"]');
    await click('button[aria-label="Notifications"]');
    await click('a[href="/catalogues/spring/web"]');
    expect(router.currentRoute.value.path).toBe('/catalogues/spring/web');
    expect(feedbackBus.retained()).toHaveLength(1);
    expect(document.querySelector('button[aria-label="Notifications"]')!.getAttribute('aria-expanded')).toBe('false');
  });
  it('updates the bell from a retained snapshot and clears it when the app scope resets', async () => {
    notifyCatalogueSaved({ id: 'old', revision: 1, title: 'Earlier save' }, 'web');
    await openMenu();
    expect(document.querySelector('[aria-label="1 notifications"]')).not.toBeNull();
    expect(document.querySelector('[aria-label="Dismiss"]')).toBeNull();
    feedbackBus.reset();
    await flushPromises();
    expect(document.querySelector('[aria-label="1 notifications"]')).toBeNull();
    await click('button[aria-label="Notifications"]');
    expect(document.body.textContent).toContain('No notifications yet.');
  });
});
