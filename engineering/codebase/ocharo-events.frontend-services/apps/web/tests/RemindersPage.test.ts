import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, type VueWrapper } from '@vue/test-utils';

import { useUiLocale } from '../src/application/shared/UseUiLocale';
import { OrganizationId, mountEvents } from './helpers/EventsApp';

let wrapper: VueWrapper | undefined;
const path = `/organizations/${OrganizationId}/reminders`;

async function renderRemindersPage() {
  const mounted = await mountEvents(path);
  wrapper = mounted.wrapper;
  await vi.waitFor(() => expect(mounted.router.currentRoute.value.matched.length).toBeGreaterThan(0));
  await flushPromises();
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
  localStorage.clear();
});

describe('reminders place', () => {
  it('says the reminder settings are on their way, under one "Reminders" title', async () => {
    await renderRemindersPage();

    expect(wrapper!.findAll('main h1')).toHaveLength(1);
    expect(wrapper!.get('main h1').text()).toBe('Reminders');
    const main = wrapper!.get('main').text();
    expect(main).toContain('Reminder settings are on their way');
    expect(main).toContain(
      'Choose which events remind whom, on which channel and when. This page fills in with the reminder system.',
    );
  });
});
