import { ref, shallowRef } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import OverviewPage from '../src/presentation/workspace/OverviewPage.vue';
import type { AccessControls } from '../src/application/workspace/UseAccess';
import type { WorkspaceControls } from '../src/application/workspace/UseWorkspace';
import { useUiLocale } from '../src/application/shared/UseUiLocale';

let wrapper: VueWrapper;
beforeEach(() => useUiLocale().setUiLocale('en'));
afterEach(() => wrapper?.unmount());
async function render() {
  const getDesigns = vi.fn(async (_id: string, kind: string) => ({ ok: true, value: Array.from({ length: kind === 'catalogue' ? 2 : 3 }, () => ({ archived: false })) }));
  const organization = shallowRef({ id: 'org', name: 'Confirmed brand' });
  wrapper = mount(OverviewPage, { props: {
    access: { api: { getDesigns }, session: ref({ sessionId: 'session' }), invalidated: ref(false), busy: ref(false), navigate: vi.fn() } as unknown as AccessControls,
    workspace: { organization, drafts: ref([]), media: ref([]), brands: ref([]) } as unknown as WorkspaceControls,
  } });
  await flushPromises();
  return { getDesigns, organization };
}
it('retains known totals when a refresh of the same scope fails', async () => {
  const { getDesigns } = await render();
  expect(wrapper.get('.overview-map-node--catalogue .overview-node-metric b').text()).toBe('2');
  getDesigns.mockRejectedValue(new Error('transport'));
  await wrapper.findAll('button').find(button => button.text() === 'Refresh')!.trigger('click');
  await flushPromises();
  expect(wrapper.get('.overview-map-node--catalogue .overview-node-metric b').text()).toBe('2');
  expect(wrapper.get('.overview-map-node--cards .overview-node-metric b').text()).toBe('3');
  expect(wrapper.get('.overview-map-hub p').text()).toContain('5');
  expect(wrapper.text()).not.toContain('transport');
});
it('clears totals when changing organization instead of presenting another scope’s data', async () => {
  const { getDesigns, organization } = await render();
  getDesigns.mockRejectedValue(new Error('transport'));
  organization.value = { id: 'other', name: 'Other brand' };
  await flushPromises();
  expect(wrapper.get('.overview-map-node--catalogue .overview-node-metric b').text()).toBe('—');
  expect(wrapper.get('.overview-map-node--cards .overview-node-metric b').text()).toBe('—');
});
