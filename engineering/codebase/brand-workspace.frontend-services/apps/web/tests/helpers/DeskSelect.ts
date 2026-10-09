import { flushPromises, type VueWrapper } from '@vue/test-utils';

/** Screen tests target semantic choice events; DeskSelect tests cover popup interaction. */
export function deskSelect(wrapper: VueWrapper, label: string) {
  const picker = wrapper.findAllComponents({ name: 'DeskSelect' }).find(item => item.props('label') === label);
  if (!picker) throw new Error(`Missing DeskSelect: ${label}`);
  return picker;
}

export async function chooseDeskValue(wrapper: VueWrapper, label: string, value: string | number): Promise<void> {
  deskSelect(wrapper, label).vm.$emit('update:modelValue', value);
  await flushPromises();
}
