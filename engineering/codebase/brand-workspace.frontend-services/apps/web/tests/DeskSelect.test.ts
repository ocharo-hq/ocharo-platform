import { mount, flushPromises, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { nextTick } from 'vue';
import DeskSelect from '@/presentation/shared/DeskSelect.vue';
import UiLanguagePicker from '@/presentation/shared/UiLanguagePicker.vue';
import { useUiLocale } from '@/application/shared/UseUiLocale';

const mounted: VueWrapper[] = [];
beforeEach(() => {
  vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
  Element.prototype.scrollIntoView = vi.fn();
  useUiLocale().setUiLocale('en');
});
afterEach(() => { mounted.splice(0).forEach(wrapper => wrapper.unmount()); document.body.innerHTML = ''; vi.unstubAllGlobals(); });
const options = [{ value: 'a', label: 'Alpha' }, { value: 'b', label: 'Beta' }, { value: 'c', label: 'Gamma', disabled: true }];
function picker(props = {}) {
  const wrapper = mount(DeskSelect, { attachTo: document.body, props: { modelValue: 'a', options, label: 'Example', ...props } });
  mounted.push(wrapper); return wrapper;
}
async function settle() { await nextTick(); await flushPromises(); }

describe('Desk SDK dropdown', () => {
  it('renders an SDK listbox, emits a value, and waits for confirmed model updates', async () => {
    const wrapper = picker();
    expect(wrapper.find('select').exists()).toBe(false);
    expect(wrapper.get('button[aria-label="Example"]').text()).toContain('Alpha');
    await wrapper.get('button[aria-label="Example"]').trigger('click'); await settle();
    const choices = [...document.querySelectorAll<HTMLElement>('[role="option"]')];
    expect(choices.map(item => item.textContent?.trim())).toEqual(['Alpha', 'Beta', 'Gamma']);
    choices[1]!.click(); await settle();
    expect(wrapper.emitted('update:modelValue')).toEqual([['b']]);
    expect(wrapper.get('button[aria-label="Example"]').text()).toContain('Alpha');
    await wrapper.setProps({ modelValue: 'b' });
    expect(wrapper.get('button[aria-label="Example"]').text()).toContain('Beta');
  });
  it('keeps disabled and loading controls closed', async () => {
    const wrapper = picker({ disabled: true });
    expect(wrapper.get('button').attributes('disabled')).toBeDefined();
    await wrapper.get('button').trigger('click'); await settle();
    expect(document.querySelector('[role="listbox"]')).toBeNull();
    await wrapper.setProps({ disabled: false, loading: true });
    expect(wrapper.get('button').attributes('disabled')).toBeDefined();
  });
  it('preserves required form validity and focuses its labelled trigger', async () => {
    const wrapper = picker({ modelValue: '', required: true });
    const input = wrapper.get('input[required]').element as HTMLInputElement;
    expect(input.checkValidity()).toBe(false);
    await settle();
    expect(document.activeElement).toBe(wrapper.get('button[aria-label="Example"]').element);
    await wrapper.setProps({ modelValue: 'a' });
    expect(input.checkValidity()).toBe(true);
    await wrapper.setProps({ options: [{ value: 'b', label: 'Beta' }] });
    expect(input.checkValidity()).toBe(false);
  });
  it('opens the language menu and dismisses without changing locale', async () => {
    const wrapper = mount(UiLanguagePicker, { attachTo: document.body }); mounted.push(wrapper);
    const trigger = wrapper.get('button[aria-label="Interface language"]');
    (trigger.element as HTMLButtonElement).focus();
    await trigger.trigger('click'); await settle();
    expect(document.querySelector('[role="listbox"]')).not.toBeNull();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true })); await settle();
    expect(useUiLocale().uiLocale.value).toBe('en');
  });
});


describe('disabled ancestor fieldsets', () => {
  function guarded(template: string) {
    const wrapper = mount({
      components: { DeskSelect }, props: { locked: Boolean },
      data: () => ({ chosen: 'a', options }), template,
    }, { attachTo: document.body, props: { locked: false } });
    mounted.push(wrapper);
    return wrapper;
  }
  async function chooseAfterDisabling(wrapper: VueWrapper) {
    await wrapper.get('button').trigger('click'); await settle();
    await wrapper.setProps({ locked: true });
    document.querySelectorAll<HTMLElement>('[role="option"]')[1]!.click(); await settle();
  }
  it('rejects a teleported choice after its ancestor becomes disabled', async () => {
    const wrapper = guarded('<fieldset :disabled="locked"><DeskSelect v-model="chosen" :options="options" label="Guarded" /></fieldset>');
    await chooseAfterDisabling(wrapper);
    expect(wrapper.getComponent({ name: 'DeskSelect' }).emitted('update:modelValue')).toBeUndefined();
    expect(wrapper.get('button').text()).toContain('Alpha');
  });
  it('preserves the first legend exemption when its ancestor becomes disabled', async () => {
    const wrapper = guarded('<fieldset :disabled="locked"><legend><DeskSelect v-model="chosen" :options="options" label="Guarded" /></legend></fieldset>');
    await chooseAfterDisabling(wrapper);
    expect(wrapper.getComponent({ name: 'DeskSelect' }).emitted('update:modelValue')).toEqual([['b']]);
    expect(wrapper.get('button').text()).toContain('Beta');
  });
  it('keeps an inner legend disabled by an outer fieldset', async () => {
    const wrapper = guarded('<fieldset :disabled="locked"><fieldset :disabled="locked"><legend><DeskSelect v-model="chosen" :options="options" label="Guarded" /></legend></fieldset></fieldset>');
    await chooseAfterDisabling(wrapper);
    expect(wrapper.getComponent({ name: 'DeskSelect' }).emitted('update:modelValue')).toBeUndefined();
    expect(wrapper.get('button').text()).toContain('Alpha');
  });
});
