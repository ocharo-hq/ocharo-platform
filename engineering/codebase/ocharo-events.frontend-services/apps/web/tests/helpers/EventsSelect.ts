import { flushPromises, type VueWrapper } from '@vue/test-utils';

/** Screen tests target semantic choice events; EventsSelect tests cover popup interaction. */
export function eventsSelect(wrapper: VueWrapper, label: string) {
  const picker = wrapper.findAllComponents({ name: 'EventsSelect' }).find((item) => item.props('label') === label);
  if (!picker) throw new Error(`Missing EventsSelect: ${label}`);
  return picker;
}

export async function chooseEventsValue(wrapper: VueWrapper, label: string, value: string | number): Promise<void> {
  eventsSelect(wrapper, label).vm.$emit('update:modelValue', value);
  await flushPromises();
}
