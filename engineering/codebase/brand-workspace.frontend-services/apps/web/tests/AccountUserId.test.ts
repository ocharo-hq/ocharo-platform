import { afterEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { ref } from 'vue';
import AccountSettings from '@/presentation/access/AccountSettings.vue';
import { feedbackBus } from '@/application/common/hooks/UseFeedback';
import type { AccessControls } from '@/application/common/hooks/UseAccess';

const SUB = 'a1b2c3d4-0000-4000-8000-000000000001';
const access = {
  session: ref({ authenticated: true, user: { id: SUB, email: 'operator@example.test', displayName: 'Operator', phone: null, picture: null }, role: 'owner' }),
} as unknown as AccessControls;

afterEach(() => { vi.restoreAllMocks(); document.body.innerHTML = ''; });

describe('Account settings user ID', () => {
  it('shows the Identity sub and copies it with a toast', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    const notify = vi.spyOn(feedbackBus, 'notify');
    mount(AccountSettings, { props: { access }, attachTo: document.body });
    await flushPromises();

    expect(document.body.querySelector('[data-user-id-value]')?.textContent).toBe(SUB);
    document.body.querySelector<HTMLElement>('[data-user-id-copy]')!.click();
    await flushPromises();

    expect(writeText).toHaveBeenCalledWith(SUB);
    expect(notify).toHaveBeenCalledWith(expect.objectContaining({ tone: 'success', title: 'Copied' }));
  });
});
