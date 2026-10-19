import { defineComponent, h, type Component } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthProvider } from '@wow-two-beta/fe/auth';

import { feedbackBus } from '@/application/shared/UseFeedback';
import { useAccess, type AccessControls } from '@/application/workspace/UseAccess';
import { createSourcingApi } from '@/integration/workspace/SourcingApi';

const session = {
  authenticated: true,
  user: { id: 'user', email: 'operator@example.test', displayName: 'Operator' },
  sessionId: 'session-a',
  csrfToken: 'token',
  setupRequired: false,
};
const navigation = { assign: vi.fn(), reload: vi.fn() };
let wrapper: VueWrapper | undefined;

beforeEach(() => vi.clearAllMocks());
afterEach(() => {
  wrapper?.unmount();
  wrapper = undefined;
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

/** Signs in through the SDK provider and its bridge, then answers every other request with `respond`. */
async function signedIn(respond: () => Response): Promise<AccessControls> {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: RequestInfo | URL) => {
      const path = new URL(String(input instanceof Request ? input.url : input), 'https://localhost').pathname;
      return Promise.resolve(path === '/api/auth/session' ? Response.json({ data: session }) : respond());
    }),
  );
  let access!: AccessControls;
  wrapper = mount(
    defineComponent({
      setup() {
        access = useAccess(navigation);
        // The generic SFC's props cannot be inferred through `h`; AppRoot passes the same two props in its template.
        return () => h(AuthProvider as unknown as Component, { strategy: access.strategy, bridge: access.bridge }, () => h('div'));
      },
    }),
  );
  await flushPromises();
  return access;
}

describe('session expiry', () => {
  it('expires a signed-in session once, cancels its requests without an error toast and replaces the document', async () => {
    const notify = vi.spyOn(feedbackBus, 'notify');
    const access = await signedIn(() => Response.json({ code: 'authentication_required' }, { status: 401 }));
    const sourcing = createSourcingApi(() => 'token', access.transport);
    const signal = new AbortController().signal;

    const results = await Promise.all([
      access.api.getOrganizations(signal),
      access.api.getBrands('org', signal),
      sourcing.getSupplier('org', 'supplier', signal),
    ]);

    expect(results.map((result) => (result.ok ? 'ok' : result.failure.code))).toEqual(['cancelled', 'cancelled', 'cancelled']);
    expect(access.invalidated.value).toBe(true);
    expect(navigation.reload).toHaveBeenCalledOnce();
    expect(notify).not.toHaveBeenCalled();
  });

  it('treats a rejected password as an answer, not an expired session', async () => {
    const access = await signedIn(() => Response.json({ code: 'invalid_credentials' }, { status: 401 }));

    const result = await access.api.linkTelegram('wrong password', new AbortController().signal);

    expect(result.ok ? null : result.failure.status).toBe(401);
    expect(access.invalidated.value).toBe(false);
    expect(navigation.reload).not.toHaveBeenCalled();
  });
});
