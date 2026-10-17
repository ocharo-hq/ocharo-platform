import { defineComponent, h } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';

import { resolveConfirmation } from '@/application/shared/UseFeedback';
import { useAccess, type AccessControls } from '@/application/workspace/UseAccess';

const channel = vi.hoisted(() => ({
  receive: (message: string) => {
    void message;
  },
  post: vi.fn(),
  close: vi.fn(),
}));
vi.mock('@wow-two-beta/fe/foundation/channels', () => ({
  createSyncChannel: () => ({
    post: channel.post,
    close: channel.close,
    subscribe: (listener: (message: string) => void) => {
      channel.receive = listener;
      return () => {};
    },
  }),
}));
const session = {
  authenticated: true,
  user: { id: 'user', email: 'operator@example.test', displayName: 'Operator' },
  sessionId: 'session-a',
  csrfToken: 'token',
  setupRequired: false,
};
let access: AccessControls;
let wrapper: VueWrapper;
const navigation = { assign: vi.fn(), reload: vi.fn() };
beforeEach(() => {
  vi.clearAllMocks();
  wrapper = mount(
    defineComponent({
      setup() {
        access = useAccess(navigation);
        return () => h('div');
      },
    }),
  );
  access.session.value = session;
});
afterEach(() => {
  resolveConfirmation(false);
  wrapper.unmount();
  vi.unstubAllGlobals();
});

describe('document session boundary', () => {
  it('returns successful sign-in to a validated catalogue destination while default transitions use home', () => {
    const path = '/organizations/11111111-1111-4111-8111-111111111111/catalogue?design=22222222-2222-4222-8222-222222222222&page=cover';
    access.authenticated(path);
    expect(navigation.assign).toHaveBeenLastCalledWith(path);
    access.authenticated('//outside.example');
    expect(navigation.assign).toHaveBeenLastCalledWith('/');
    access.authenticated();
    expect(navigation.assign).toHaveBeenLastCalledWith('/');
  });

  it('blocks organization switches during writes and confirms dirty navigation', async () => {
    let finish!: () => void;
    const pending = access.write<void>(
      () =>
        new Promise((resolve) => {
          finish = () => resolve(ResultExtensions.ok(undefined));
        }),
    );
    access.navigate('/organizations/other');
    expect(navigation.assign).not.toHaveBeenCalled();
    finish();
    await pending;
    access.dirty.value = true;
    const cancelled = access.navigate('/');
    resolveConfirmation(false); await cancelled;
    expect(navigation.assign).not.toHaveBeenCalled();
    const accepted = access.navigate('/');
    resolveConfirmation(true); await accepted;
    expect(navigation.assign).toHaveBeenCalledWith('/');
  });

  it('invalidates private state on a cross-tab cookie change', () => {
    channel.receive('changed');
    expect(access.invalidated.value).toBe(true);
    expect(navigation.reload).toHaveBeenCalledOnce();
  });

  it('reloads a restored document before reusing its session', () => {
    window.dispatchEvent(new PageTransitionEvent('pageshow', { persisted: true }));
    expect(access.invalidated.value).toBe(true);
    expect(navigation.reload).toHaveBeenCalledOnce();
  });

  it('keeps writes blocked on a failed session recheck and detects identity changes', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockRejectedValueOnce(new TypeError('offline'))
        .mockResolvedValueOnce(Response.json({ data: { ...session, sessionId: 'session-b' } })),
    );
    access.dirty.value = true;
    await access.recheck();
    expect(access.busy.value).toBe(true);
    expect(access.dirty.value).toBe(true);
    expect(navigation.reload).not.toHaveBeenCalled();
    await access.recheck();
    expect(navigation.reload).toHaveBeenCalledOnce();
  });

  it('adopts a fresh token only after revalidation confirms the same session', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(Response.json({ data: { ...session, csrfToken: 'another-valid-token' } })),
    );
    await access.recheck();
    await flushPromises();
    expect(access.session.value?.csrfToken).toBe('another-valid-token');
    expect(access.busy.value).toBe(false);
    expect(navigation.reload).not.toHaveBeenCalled();
  });

  it('replaces the document when a write rejects its session-bound CSRF token', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json({ code: 'csrf_validation_failed' }, { status: 403 })));
    await access.login({ email: 'operator@example.test', password: 'test-password' }, new AbortController().signal);
    expect(access.invalidated.value).toBe(true);
    expect(navigation.reload).toHaveBeenCalledOnce();
  });

  it('does not report logout success when the server did not confirm it', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('offline')));
    await access.logout();
    expect(access.sessionError.value).not.toBeNull();
    expect(channel.post).not.toHaveBeenCalled();
    expect(navigation.assign).not.toHaveBeenCalled();
  });
});
