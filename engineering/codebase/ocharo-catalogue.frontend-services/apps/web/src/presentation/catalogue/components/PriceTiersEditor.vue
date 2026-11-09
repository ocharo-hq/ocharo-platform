<script setup lang="ts">
import { computed } from 'vue';
import type { ProductPricing } from '../../../domain/catalogue/CatalogueModels';
import { formatUzs } from '../../../domain/catalogue/CatalogueRules';
import { useUiLocale } from '../../../application/shared/UseUiLocale';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { CheckboxField, Field, NumberInput, TextInput } from '@wow-two-beta/fe/presentation/forms';
defineOptions({ name: 'PriceTiersEditor' });
const pricing = defineModel<ProductPricing | null>({ required: true });
const { t } = useUiLocale();
const retail = computed(() => pricing.value?.tiers.find((tier) => tier.minimumQuantity === 1)?.unitPrice ?? 0);
const wholesale = computed(() => pricing.value?.tiers.find((tier) => tier.minimumQuantity === 10)?.unitPrice ?? 0);
const legacy = computed(
  () =>
    pricing.value &&
    (pricing.value.tiers.length !== 2 ||
      pricing.value.tiers[0]?.maximumQuantity !== 9 ||
      pricing.value.tiers[1]?.minimumQuantity !== 10),
);
function setPrice(band: 'retail' | 'wholesale', value: number | string | null) {
  if (!pricing.value) return;
  pricing.value = {
    ...pricing.value,
    currency: 'UZS',
    status: 'draft',
    tiers: [
      { minimumQuantity: 1, maximumQuantity: 9, unitPrice: band === 'retail' ? Number(value) : retail.value },
      { minimumQuantity: 10, maximumQuantity: null, unitPrice: band === 'wholesale' ? Number(value) : wholesale.value },
    ],
  };
}
function enable() {
  pricing.value = {
    currency: 'UZS',
    status: 'draft',
    tiers: [
      { minimumQuantity: 1, maximumQuantity: 9, unitPrice: 0 },
      { minimumQuantity: 10, maximumQuantity: null, unitPrice: 0 },
    ],
    wording: null,
  };
}
function confirm(value: boolean) {
  if (!pricing.value) return;
  if (legacy.value) setPrice('retail', String(retail.value));
  pricing.value.status = value ? 'confirmed' : 'draft';
}
</script>
<template>
  <div v-if="!pricing" class="rounded-xl border border-dashed border-border p-5 text-sm text-muted-foreground">
    {{ t('No price entered.') }}
    <Button variant="outline" tone="neutral" class="ml-3" @click="enable">{{ t('Add prices') }}</Button>
  </div>
  <div v-else class="space-y-4">
    <div class="grid gap-4 sm:grid-cols-2">
      <Field :label="t('1–9 pieces · UZS per piece')" :helper="formatUzs(retail)">
        <NumberInput :model-value="retail" min="0" @update:model-value="setPrice('retail', $event ?? 0)" />
      </Field>
      <Field :label="t('10+ pieces · UZS per piece')" :helper="formatUzs(wholesale)">
        <NumberInput :model-value="wholesale" min="0" @update:model-value="setPrice('wholesale', $event ?? 0)" />
      </Field>
    </div>
    <p v-if="legacy" class="text-sm text-warning">
      {{ t('Editing or confirming prices applies the 1–9 and 10+ quantity bands.') }}
    </p>
    <div class="flex flex-wrap items-center gap-3">
      <CheckboxField
        :model-value="pricing.status === 'confirmed'"
        :label="t('Prices confirmed by the owner')"
        @update:model-value="confirm"
      />
      <Button variant="ghost" tone="danger" class="ml-auto" @click="pricing = null">
        {{ t('Remove prices') }}
      </Button>
    </div>
    <p class="text-xs text-muted-foreground">
      {{ t('Changing a price resets its confirmation. Only confirmed prices appear publicly.') }}
    </p>
    <Field :label="t('Price note')">
      <TextInput :model-value="pricing.wording ?? ''" @update:model-value="pricing.wording = $event" />
    </Field>
  </div>
</template>
