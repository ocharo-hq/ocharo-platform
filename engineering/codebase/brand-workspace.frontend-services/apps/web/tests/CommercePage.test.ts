import { computed, ref } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ResultExtensions } from '@wow-two-beta/ui-vue/foundation/results';
import { ApiFailureFactory } from '@wow-two-beta/ui-vue/foundation/http';
import type { AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
import { initialCommerce } from '@/domain/workspace/Commerce';
import { feedbackBus } from '@/application/shared/UseFeedback';
import CommercePage from '@/presentation/workspace/CommercePage.vue';

const api = vi.hoisted(() => ({ get: vi.fn(), releases: vi.fn(), save: vi.fn(), image: vi.fn(), publish: vi.fn() }));
vi.mock('@/integration/workspace/CommerceApi', () => ({ createCommerceApi: () => api }));
let wrapper: VueWrapper;
afterEach(() => wrapper?.unmount());
beforeEach(() => {
  api.get.mockResolvedValue(ResultExtensions.ok({ brandId:'brand',revision:3,snapshot:initialCommerce('Ocharo','ocharo'),updatedAt:'2026-09-29' }));
  api.releases.mockResolvedValue(ResultExtensions.ok([]));
});
function page() {
  const access = { busy:ref(false),session:ref({csrfToken:'token'}),setDirty:vi.fn(),recheck:vi.fn(),write:vi.fn((operation:(signal:AbortSignal)=>unknown)=>operation(new AbortController().signal)) };
  const workspace = { organization:ref({id:'org'}),brands:ref([{id:'brand',name:'Ocharo'}]),media:ref([]),canEdit:computed(()=>true),canApprove:computed(()=>true) };
  wrapper = mount(CommercePage,{props:{access:access as unknown as AccessControls,workspace:workspace as unknown as WorkspaceControls}});
  return {access,workspace};
}
function button(label:string) { return wrapper.findAll('button').find(b=>b.text()===label)!; }
describe('commerce draft safety', () => {
  it('loads the initial brand when its list is already available', async () => {
    page(); await flushPromises();
    expect(api.get).toHaveBeenCalledWith('org','brand',expect.any(AbortSignal));
    expect(wrapper.text()).toContain('Draft 3');
  });
  it('keeps the dirty buffer on conflicts and blocks brand changes', async () => {
    const notification = vi.spyOn(feedbackBus, 'notify');
    const {access} = page(); await flushPromises();
    await button('Brand identity').trigger('click');
    await wrapper.findAll('input')[0]!.setValue('Edited brand');
    expect(access.setDirty).toHaveBeenLastCalledWith(true);
    expect(wrapper.find('select').attributes('disabled')).toBeDefined();
    api.save.mockResolvedValue(ResultExtensions.fail(ApiFailureFactory.create('http',{status:409,headers:new Headers()})));
    await button('Save draft').trigger('click');await flushPromises();
    expect((wrapper.findAll('input')[0]!.element as HTMLInputElement).value).toBe('Edited brand');
    expect(wrapper.text()).toContain('Unsaved changes');
    expect(notification).toHaveBeenCalledWith(expect.objectContaining({tone:'danger',description:expect.stringContaining('This record changed')}));
    notification.mockRestore();
  });
  it('publishes only a saved revision with an owner note', async () => {
    page();await flushPromises();
    await button('Publish & history').trigger('click');
    expect(button('Publish catalogue').attributes('disabled')).toBeDefined();
    await wrapper.get('textarea').setValue('Reviewed catalogue');
    api.publish.mockResolvedValue(ResultExtensions.ok({id:'release',revision:1,sourceRevision:3,publishedAt:'2026-09-29',note:'Reviewed catalogue',withdrawnAt:null}));
    await button('Publish catalogue').trigger('click');await flushPromises();
    expect(api.publish).toHaveBeenCalledWith('org','brand',3,'Reviewed catalogue',expect.any(AbortSignal));
    expect(wrapper.text()).toContain('Release 1 published');
  });
});
