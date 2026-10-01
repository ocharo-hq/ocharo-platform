import { describe, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import { nextTick } from 'vue';
import PriceTiersEditor from '../src/presentation/catalogue/components/PriceTiersEditor.vue';
import type { ProductPricing } from '../src/domain/catalogue/CatalogueModels';

describe('two UZS prices', () => {
  it('normalizes legacy quantity gaps only after an explicit price edit', async () => {
    const original: ProductPricing = {
      currency: 'UZS',
      status: 'confirmed',
      tiers: [
        { minimumQuantity: 1, maximumQuantity: 1, unitPrice: 130000 },
        { minimumQuantity: 10, maximumQuantity: null, unitPrice: 100000 },
      ],
      wording: null,
    };
    const wrapper = mount(PriceTiersEditor, { props: { modelValue: original } });
    const fields = wrapper.findAll('input[type=number]');
    expect(fields).toHaveLength(2);
    expect(wrapper.emitted('update:modelValue')).toBeUndefined();
    await fields[0]!.setValue('140000');
    const updated = wrapper.emitted('update:modelValue')![0]![0] as ProductPricing;
    expect(updated.currency).toBe('UZS');
    expect(updated.status).toBe('draft');
    expect(updated.tiers).toEqual([
      { minimumQuantity: 1, maximumQuantity: 9, unitPrice: 140000 },
      { minimumQuantity: 10, maximumQuantity: null, unitPrice: 100000 },
    ]);
    expect(original.tiers[0]?.maximumQuantity).toBe(1);
    await wrapper.setProps({ modelValue: updated });
    await nextTick();
    await wrapper.find('input[type=checkbox]').setValue(true);
    expect(updated.status).toBe('confirmed');
    wrapper.unmount();
  });
});
