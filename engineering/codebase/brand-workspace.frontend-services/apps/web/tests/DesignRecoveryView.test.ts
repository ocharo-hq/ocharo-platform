import { computed, ref } from 'vue';
import { mount } from '@vue/test-utils';
import { describe, expect, it, vi } from 'vitest';
import DesignRecovery from '@/presentation/workspace/DesignRecovery.vue';
import type { DesignRecoveryControls } from '@/application/workspace/UseDesignRecovery';
import type { DesignRecoveryRecord } from '@/domain/workspace/DesignRecovery';
import { createCardTemplate } from '@/domain/workspace/Templates';
function controls() {
  return { supported: computed(() => true), enabled: ref(false), available: ref<DesignRecoveryRecord | null>(null), hasStored: ref(false), status: ref('Off in this tab'), error: ref(''), enable: vi.fn(), disable: vi.fn(), discard: vi.fn(), restore: vi.fn() };
}
describe('tab recovery controls', () => {
  it('requires explicit opt-in and explains the tab lifetime', async () => {
    const recovery = controls(), wrapper = mount(DesignRecovery, { props: { recovery: recovery as unknown as DesignRecoveryControls, disabled: false } });
    expect(recovery.enable).not.toHaveBeenCalled(); expect(wrapper.text()).toContain('Save before closing the tab');
    await wrapper.get('button').trigger('click'); expect(recovery.enable).toHaveBeenCalledOnce(); wrapper.unmount();
  });
  it('shows a pending recovery as a new-copy action and leaves discarding separate', async () => {
    const recovery = controls(); recovery.hasStored.value = true;
    recovery.available.value = { schemaVersion: 1, token: 'token', scope: { userId: 'user', organizationId: 'org', kind: 'business-card' }, source: { documentId: 'source', revision: 4 }, title: 'Previous card', savedAt: '2026-09-30T00:00:00Z', snapshot: createCardTemplate() };
    const wrapper = mount(DesignRecovery, { props: { recovery: recovery as unknown as DesignRecoveryControls, disabled: false } });
    expect(wrapper.text()).toContain('source revision 4');
    await wrapper.findAll('button').find(button => button.text() === 'Restore as new copy')!.trigger('click');
    expect(recovery.restore).toHaveBeenCalledOnce(); expect(recovery.discard).not.toHaveBeenCalled(); wrapper.unmount();
  });
  it('displays quota failures and disables mutations during other editor actions', () => {
    const recovery = controls(); recovery.error.value = 'Browser storage is full.';
    const wrapper = mount(DesignRecovery, { props: { recovery: recovery as unknown as DesignRecoveryControls, disabled: true } });
    expect(wrapper.get('[role=alert]').text()).toContain('storage is full'); expect(wrapper.get('button').attributes('disabled')).toBeDefined(); wrapper.unmount();
  });
});
