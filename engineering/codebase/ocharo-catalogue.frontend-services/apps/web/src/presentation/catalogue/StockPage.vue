<script setup lang="ts">
import ContentSkeleton from '../common/ContentSkeleton.vue';
import { computed, onMounted } from 'vue';
import { RouterLink } from 'vue-router';
import { Save } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { useStockSheet } from '../../application/catalogue';
import { availabilityLabels } from '../../domain/catalogue/CatalogueRules';

/** Pieces on hand per size; drives "sotuvda bor / tugagan" in the public feed and the order sheet. */
defineOptions({ name: 'StockPage' });
const sheet = useStockSheet();
const allSizes = computed(() => {
  const order = ['XS', 'S', 'M', 'L', 'XL', 'XXL', 'XXXL'];
  const sizes = new Set(sheet.rows.value.flatMap((item) => item.row.sizes));
  return [...sizes].sort((a, b) => (order.indexOf(a) + 1 || 99) - (order.indexOf(b) + 1 || 99) || a.localeCompare(b));
});
const tone: Record<string, string> = {
  'in-stock': 'bg-success-soft text-success-soft-foreground',
  'sold-out': 'bg-destructive-soft text-destructive-soft-foreground',
  'made-to-order': 'bg-primary-soft text-primary-soft-foreground',
  unknown: 'bg-muted text-muted-foreground',
};
onMounted(() => sheet.load());
</script>

<template>
  <section>
    <div class="flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 class="font-editorial text-4xl tracking-tight">Qoldiq</h1>
        <p class="mt-2 max-w-2xl text-sm text-muted-foreground">
          O‘lcham bo‘yicha mavjud donalar: jami {{ sheet.total.value }} dona. Ommaviy lentaga faqat “sotuvda bor /
          tugagan” holati chiqadi, sonlar ichki qoladi.
        </p>
      </div>
      <button class="btn-primary" :disabled="sheet.dirtyCount.value === 0" @click="sheet.saveAll()">
        <Icon :icon="Save" :size="15" />{{ sheet.dirtyCount.value }} ta o‘zgarishni saqlash
      </button>
    </div>
    <ContentSkeleton v-if="sheet.loading.value" />
    <div class="panel mt-6 overflow-x-auto p-0">
      <table class="w-full min-w-[900px] text-sm">
        <thead class="bg-muted/60 text-left text-[11px] uppercase tracking-[0.12em] text-muted-foreground">
          <tr>
            <th class="px-4 py-3 font-semibold">Mahsulot</th>
            <th class="px-2 py-3 font-semibold">Hisob</th>
            <th v-for="size in allSizes" :key="size" class="w-20 px-1 py-3 text-center font-semibold">{{ size }}</th>
            <th class="px-4 py-3 font-semibold">Holat</th>
            <th class="px-4 py-3"></th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="item in sheet.rows.value"
            :key="item.row.id"
            :class="['border-t border-border', sheet.isDirty(item) ? 'bg-warning-soft/40' : '']"
          >
            <td class="px-4 py-2">
              <RouterLink :to="`/products/${item.row.id}`" class="flex items-center gap-3 hover:text-primary">
                <img
                  v-if="item.row.previewUrl"
                  :src="item.row.previewUrl"
                  alt=""
                  class="h-12 w-9 object-contain"
                  loading="lazy"
                />
                <span>
                  <span class="block font-semibold">{{ item.row.nameUz ?? item.row.id }}</span>
                  <span class="block text-xs text-muted-foreground">{{ item.row.id }}</span>
                </span>
              </RouterLink>
            </td>
            <td class="px-2 py-2 text-xs">
              <label class="flex items-center gap-1.5"
                ><input
                  type="checkbox"
                  :checked="item.row.stock !== null"
                  @change="sheet.track(item, ($event.target as HTMLInputElement).checked)"
                />Sanash</label
              >
              <label class="mt-1 flex items-center gap-1.5"
                ><input v-model="item.row.madeToOrder" type="checkbox" />Buyurtmaga</label
              >
            </td>
            <td v-for="size in allSizes" :key="size" class="px-1 py-2 text-center">
              <input
                v-if="item.row.sizes.includes(size)"
                :value="item.row.stock?.[size] ?? ''"
                type="number"
                min="0"
                class="field w-16 px-2 text-center"
                :disabled="item.row.stock === null"
                :aria-label="`${item.row.id} ${size}`"
                @input="sheet.setQuantity(item, size, Number(($event.target as HTMLInputElement).value))"
              />
              <span v-else class="text-muted-foreground">—</span>
            </td>
            <td class="px-4 py-2">
              <span :class="['chip', tone[item.row.availability]]">{{
                availabilityLabels[item.row.availability]
              }}</span>
            </td>
            <td class="px-4 py-2 text-right">
              <button class="btn-outline" :disabled="!sheet.isDirty(item) || item.saving" @click="sheet.save(item)">
                {{ item.saving ? '…' : 'Saqlash' }}
              </button>
              <p v-if="item.error" class="mt-1 max-w-40 text-left text-xs text-destructive" role="alert">
                {{ item.error }}
              </p>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
