import { afterEach, describe, expect, it } from 'vitest';
import { mount, type VueWrapper } from '@vue/test-utils';
import { nextTick } from 'vue';
import CatalogueSelect from '../src/presentation/common/CatalogueSelect.vue';

const options = [
  { value: 'uz', label: 'O‘zbekcha' },
  { value: 'en', label: 'English' },
];

let wrapper: VueWrapper | null = null;
afterEach(() => {
  wrapper?.unmount();
  wrapper = null;
  document.body.innerHTML = '';
});

describe('CatalogueSelect', () => {
  it('labels the closed trigger from its rows before the first open', async () => {
    wrapper = mount(CatalogueSelect, {
      props: { modelValue: 'en', options, label: 'Interface language' },
      attachTo: document.body,
    });
    await nextTick();
    await nextTick();

    const trigger = wrapper.get('[aria-label="Interface language"]');
    expect(trigger.text().trim()).toBe('English');
    expect(document.body.querySelector('[role="listbox"]')).toBeNull();
  });

  it('follows a value changed from outside while closed', async () => {
    wrapper = mount(CatalogueSelect, {
      props: { modelValue: 'en', options, label: 'Interface language' },
      attachTo: document.body,
    });
    await nextTick();
    await wrapper.setProps({ modelValue: 'uz' });
    await nextTick();

    expect(wrapper.get('[aria-label="Interface language"]').text().trim()).toBe('O‘zbekcha');
  });
});
