import { ref } from 'vue';
import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiFailureFactory } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import { FileUploadPicker } from '@wow-two-beta/fe/presentation/forms';
import { feedbackBus } from '@/application/shared/UseFeedback';
import type { AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
import OrganizationPage from '@/presentation/workspace/OrganizationPage.vue';
import BrandsPage from '@/presentation/workspace/BrandsPage.vue';
import MediaPage from '@/presentation/workspace/MediaPage.vue';
import LoginPage from '@/presentation/workspace/LoginPage.vue';

afterEach(() => vi.restoreAllMocks());
const failure = () => ResultExtensions.fail(ApiFailureFactory.create('http', new Response(null, { status: 500 })));
function controls() {
  const command = vi.fn().mockResolvedValue(failure());
  return {
    command,
    access: { busy: ref(false), session: ref(null), api: { getTelegram: vi.fn().mockResolvedValue(ResultExtensions.ok({ enabled: false, linked: false, phone: null, botUsername: null })) }, setDirty: vi.fn(), navigate: vi.fn(), login: command, authenticated: vi.fn() } as unknown as AccessControls,
    workspace: { organizations: ref([]), brands: ref([]), media: ref([]), canEdit: ref(true), canApprove: ref(true),
      createOrganization: command, createBrand: command, uploadMedia: command } as unknown as WorkspaceControls,
  };
}

describe('operator command failures', () => {
  it.each([OrganizationPage, BrandsPage, LoginPage])('toasts one safe request failure and preserves editable fields in %s', async (component) => {
    const notices = vi.spyOn(feedbackBus, 'notify');
    const { access, workspace, command } = controls();
    const wrapper = mount(component, { props: { access, workspace }, global: { stubs: { BuildIdentity: true } } });
    const inputs = wrapper.findAll('input');
    for (const input of inputs) await input.setValue(input.attributes('type') === 'email' ? 'owner@example.test' : 'example');
    await wrapper.get('form').trigger('submit'); await flushPromises();
    expect(command).toHaveBeenCalledTimes(1);
    expect(notices).toHaveBeenCalledTimes(1);
    expect(notices).toHaveBeenCalledWith(expect.objectContaining({ tone: 'danger', description: expect.stringContaining('could not complete') }));
    expect(wrapper.find('[role=alert]').exists()).toBe(false);
    expect(wrapper.find('form').exists()).toBe(true);
    wrapper.unmount();
  });

  it('keeps selected image validation inline while an upload request failure uses a toast', async () => {
    const notices = vi.spyOn(feedbackBus, 'notify');
    const { access, workspace, command } = controls();
    const wrapper = mount(MediaPage, { props: { access, workspace } });
    wrapper.findComponent(FileUploadPicker).vm.$emit('files-change', [], [new File(['bad'], 'bad.txt')]);
    await flushPromises();
    expect(wrapper.get('[role=alert]').text()).toContain('Use one PNG or JPEG');
    expect(notices).not.toHaveBeenCalled();
    wrapper.findComponent(FileUploadPicker).vm.$emit('files-change', [new File(['image'], 'photo.png', { type: 'image/png' })], []);
    await wrapper.get('textarea').setValue('Photograph by Ocharo');
    await wrapper.get('form').trigger('submit'); await flushPromises();
    expect(command).toHaveBeenCalledTimes(1); expect(notices).toHaveBeenCalledTimes(1);
    expect(wrapper.text()).toContain('photo.png');
    expect(wrapper.find('[role=alert]').exists()).toBe(false);
    wrapper.unmount();
  });
});
