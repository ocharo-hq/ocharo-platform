<script setup lang="ts">
import { computed } from 'vue';
import { Plus, Trash2 } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import type { ProductPricing } from '../../../domain/catalogue/CatalogueModels';
import { formatUzs, tierProblem } from '../../../domain/catalogue/CatalogueRules';

/** Quantity-tier price editor (UZS per piece). */
defineOptions({ name: 'PriceTiersEditor' });
const pricing = defineModel<ProductPricing | null>({ required: true });
const problem = computed(() => (pricing.value ? tierProblem(pricing.value.tiers) : null));
function enable() {
  pricing.value = {
    currency: 'UZS',
    status: 'draft',
    tiers: [
      { minimumQuantity: 1, maximumQuantity: 1, unitPrice: 0 },
      { minimumQuantity: 10, maximumQuantity: null, unitPrice: 0 },
    ],
    wording: null,
  };
}
function addTier() {
  if (!pricing.value) return;
  const tiers = pricing.value.tiers;
  const last = tiers[tiers.length - 1];
  const start = last ? (last.maximumQuantity ?? last.minimumQuantity) + 1 : 1;
  if (last && last.maximumQuantity === null) last.maximumQuantity = start - 1;
  tiers.push({ minimumQuantity: start, maximumQuantity: null, unitPrice: last?.unitPrice ?? 0 });
}
function setMax(index: number, raw: string) {
  const tier = pricing.value?.tiers[index];
  if (tier) tier.maximumQuantity = raw.trim() === '' ? null : Number(raw);
}
</script>

<template>
  <div>
    <div v-if="!pricing" class="rounded-xl border border-dashed border-border p-5 text-sm text-muted-foreground">
      Narx kiritilmagan (masalan, obraz uchun).
      <button type="button" class="btn-outline ml-3" @click="enable">Narx qo‘shish</button>
    </div>
    <div v-else class="space-y-3">
      <div class="overflow-x-auto">
        <table class="w-full min-w-[420px] text-sm">
          <thead>
            <tr class="text-left text-[11px] uppercase tracking-[0.12em] text-muted-foreground">
              <th class="pb-2 font-semibold">Dan (dona)</th>
              <th class="pb-2 font-semibold">Gacha</th>
              <th class="pb-2 font-semibold">1 dona narxi</th>
              <th class="pb-2" />
            </tr>
          </thead>
          <tbody>
            <tr v-for="(tier, index) in pricing.tiers" :key="index" class="border-t border-border">
              <td class="py-2 pr-2">
                <input
                  v-model.number="tier.minimumQuantity"
                  type="number"
                  min="1"
                  class="field"
                  :aria-label="`${index + 1}-oraliq boshlanishi`"
                />
              </td>
              <td class="py-2 pr-2">
                <input
                  :value="tier.maximumQuantity ?? ''"
                  type="number"
                  min="1"
                  placeholder="∞"
                  class="field"
                  :aria-label="`${index + 1}-oraliq oxiri`"
                  @input="setMax(index, ($event.target as HTMLInputElement).value)"
                />
              </td>
              <td class="py-2 pr-2">
                <input
                  v-model.number="tier.unitPrice"
                  type="number"
                  min="0"
                  step="1000"
                  class="field"
                  :aria-label="`${index + 1}-oraliq narxi`"
                />
                <p class="mt-1 text-[11px] text-muted-foreground">{{ formatUzs(tier.unitPrice) }}</p>
              </td>
              <td class="py-2 text-right">
                <button
                  type="button"
                  class="btn-ghost"
                  aria-label="Oraliqni o‘chirish"
                  @click="pricing.tiers.splice(index, 1)"
                >
                  <Icon :icon="Trash2" :size="15" />
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <p v-if="problem" class="text-sm text-destructive" role="alert">{{ problem }}</p>
      <div class="flex flex-wrap items-center gap-3">
        <button type="button" class="btn-outline" @click="addTier"><Icon :icon="Plus" :size="15" />Oraliq</button>
        <label class="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            :checked="pricing.status === 'confirmed'"
            @change="pricing.status = ($event.target as HTMLInputElement).checked ? 'confirmed' : 'draft'"
          />
          Narx egasi tomonidan tasdiqlangan
        </label>
        <button type="button" class="btn-ghost ml-auto text-destructive" @click="pricing = null">
          Narxni olib tashlash
        </button>
      </div>
      <div>
        <label class="label" for="price-wording">Narx izohi (katalogda)</label>
        <input
          id="price-wording"
          v-model="pricing.wording"
          class="field"
          placeholder="Narx buyurtma miqdoriga qarab belgilanadi."
        />
      </div>
    </div>
  </div>
</template>
