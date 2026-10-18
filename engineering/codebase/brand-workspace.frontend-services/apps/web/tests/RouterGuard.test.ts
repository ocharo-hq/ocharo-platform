import { defineComponent, h } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';

import { resolveConfirmation } from '@/application/shared/UseFeedback';
import { useAccess, type AccessControls } from '@/application/workspace/UseAccess';

import { deskRouter } from './helpers/DeskRouter';

const ORG = '11111111-1111-4111-8111-111111111111';
const OTHER = '22222222-2222-4222-8222-222222222222';
let access: AccessControls;
let wrapper: VueWrapper | undefined;
let router: Awaited<ReturnType<typeof deskRouter>>;
const assign = vi.fn();

beforeEach(async () => {
  vi.clearAllMocks();
  window.history.replaceState(null, '', `/organizations/${ORG}/events`);
  router = await deskRouter();
  wrapper = mount(
    defineComponent({
      setup() {
        access = useAccess({ assign, reload: vi.fn(), router });
        return () => h('div');
      },
    }),
    { global: { plugins: [router] } },
  );
  access.session.value = {
    authenticated: true,
    user: { id: 'user', email: 'operator@example.test', displayName: 'Operator' },
    sessionId: 'session',
    csrfToken: 'token',
    setupRequired: false,
  };
});
afterEach(() => {
  resolveConfirmation(false);
  wrapper?.unmount();
  window.history.replaceState(null, '', '/');
});

describe('in-document navigation guard', () => {
  it('moves between sections of the same organization without replacing the document', async () => {
    await access.navigate(`/organizations/${ORG}/sourcing`);
    await flushPromises();
    expect(router.currentRoute.value.path).toBe(`/organizations/${ORG}/sourcing`);
    expect(assign).not.toHaveBeenCalled();
  });

  it('asks before leaving unsaved edits by back, forward or a link, and stays when declined', async () => {
    access.setDirty(true);
    const declined = router.push(`/organizations/${ORG}/media`);
    await flushPromises();
    resolveConfirmation(false);
    await declined;
    expect(router.currentRoute.value.path).toBe(`/organizations/${ORG}/events`);
    expect(access.dirty.value).toBe(true);

    const accepted = router.push(`/organizations/${ORG}/media`);
    await flushPromises();
    resolveConfirmation(true);
    await accepted;
    expect(router.currentRoute.value.path).toBe(`/organizations/${ORG}/media`);
    expect(access.dirty.value).toBe(false);
  });

  it('holds the page while a write is pending', async () => {
    let finish!: () => void;
    const pending = access.write<void>(() => new Promise((resolve) => { finish = () => resolve(ResultExtensions.ok(undefined)); }));
    await router.push(`/organizations/${ORG}/media`);
    expect(router.currentRoute.value.path).toBe(`/organizations/${ORG}/events`);
    finish();
    await pending;
    await router.push(`/organizations/${ORG}/media`);
    expect(router.currentRoute.value.path).toBe(`/organizations/${ORG}/media`);
  });

  it('replaces the document to reach another organization, so no private state crosses it', async () => {
    await router.push(`/organizations/${OTHER}/events`);
    expect(assign).toHaveBeenCalledWith(`/organizations/${OTHER}/events`);
    expect(router.currentRoute.value.path).toBe(`/organizations/${ORG}/events`);

    assign.mockClear();
    await access.navigate(`/organizations/${OTHER}`);
    expect(assign).toHaveBeenCalledWith(`/organizations/${OTHER}`);
  });

  it('keeps the organization-free diagnostics inside the document', async () => {
    await access.navigate('/system');
    expect(router.currentRoute.value.path).toBe('/system');
    expect(assign).not.toHaveBeenCalled();
  });
});
