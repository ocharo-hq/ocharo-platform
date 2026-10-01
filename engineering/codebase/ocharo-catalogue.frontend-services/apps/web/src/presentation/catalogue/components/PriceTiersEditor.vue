<script setup lang="ts">
import { computed } from 'vue';
import type { ProductPricing } from '../../../domain/catalogue/CatalogueModels';
import { formatUzs } from '../../../domain/catalogue/CatalogueRules';
import { useUiLocale } from '../../../application/shared/UseUiLocale';
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
function setPrice(band: 'retail' | 'wholesale', value: string) {
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
    <button type="button" class="btn-outline ml-3" @click="enable">{{ t('Add prices') }}</button>
  </div>
  <div v-else class="space-y-4">
    <div class="grid gap-4 sm:grid-cols-2">
      <label
        ><span class="label">{{ t('1–9 pieces · UZS per piece') }}</span
        ><input
          :value="retail"
          type="number"
          min="0"
          step="1"
          class="field"
          @input="setPrice('retail', ($event.target as HTMLInputElement).value)"
        /><span class="mt-1 block text-xs text-muted-foreground">{{ formatUzs(retail) }}</span></label
      >
      <label
        ><span class="label">{{ t('10+ pieces · UZS per piece') }}</span
        ><input
          :value="wholesale"
          type="number"
          min="0"
          step="1"
          class="field"
          @input="setPrice('wholesale', ($event.target as HTMLInputElement).value)"
        /><span class="mt-1 block text-xs text-muted-foreground">{{ formatUzs(wholesale) }}</span></label
      >
    </div>
    <p v-if="legacy" class="text-sm text-warning">
      {{ t('Editing or confirming prices applies the 1–9 and 10+ quantity bands.') }}
    </p>
    <div class="flex flex-wrap items-center gap-3">
      <label class="flex items-center gap-2 text-sm"
        ><input
          type="checkbox"
          :checked="pricing.status === 'confirmed'"
          @change="confirm(($event.target as HTMLInputElement).checked)"
        />{{ t('Prices confirmed by the owner') }}</label
      >
      <button type="button" class="btn-ghost ml-auto text-destructive" @click="pricing = null">
        {{ t('Remove prices') }}
      </button>
    </div>
    <p class="text-xs text-muted-foreground">
      {{ t('Changing a price resets its confirmation. Only confirmed prices appear publicly.') }}
    </p>
    <label
      ><span class="label">{{ t('Price note') }}</span
      ><input v-model="pricing.wording" class="field"
    /></label>
  </div>
</template>
