import { defineComponent, h, type Component } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthProvider } from '@wow-two-beta/fe/auth';

import { feedbackBus } from '@/application/common/hooks/UseFeedback';
import { useAccess, type AccessControls } from '@/application/common/hooks/UseAccess';
import { createSourcingApi } from '@/integration/sourcing/SourcingApi';

const session = {
  authenticated: true,
  user: { id: 'user', email: 'operator@example.test', displayName: 'Operator' },
  sessionId: 'session-a',
  csrfToken: 'token',
};
/** The BFF's session payload for a Desk session fixture. */
const bff = (value: { sessionId: string; csrfToken: string }) => ({
  isAuthenticated: true,
  person: { sub: value.sessionId, name: 'Operator', email: 'operator@example.test', phoneNumber: null },
  organization: null,
  permissions: [],
  csrfToken: value.csrfToken,
  expiresAt: '2026-11-07T00:00:00Z',
});
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
      return Promise.resolve(path === '/api/identity/session' ? Response.json({ data: bff(session) }) : respond());
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
});
