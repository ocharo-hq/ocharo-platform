<script setup lang="ts">
import { onMounted } from 'vue';
import { RouterLink } from 'vue-router';
import { useCatalogueStore, usePriceSheet, useProductList } from '../../application/catalogue';
import { formatUzs } from '../../domain/catalogue/CatalogueRules';
import StatusBadge from './components/StatusBadge.vue';

/** Every price on one sheet; edits save per cell with revision checks. */
defineOptions({ name: 'PricingPage' });
const store = useCatalogueStore();
const list = useProductList();
const sheet = usePriceSheet();
async function commit(id: string, tier: 'retail' | 'wholesale', event: Event, previous: number | null) {
  const raw = (event.target as HTMLInputElement).value.replace(/\s/g, '');
  if (raw === '' || Number(raw) === previous) return;
  if (await sheet.setPrice(id, tier, Number(raw))) await list.load();
}
onMounted(() => list.load());
</script>

<template>
  <section>
    <h1 class="font-editorial text-4xl tracking-tight">Narxlar</h1>
    <p class="mt-2 max-w-2xl text-sm text-muted-foreground">
      Katakni o‘zgartirib, Enter yoki boshqa joyni bosing — narx darhol saqlanadi va “qoralama” deb belgilanadi. Ko‘p
      mahsulotga foiz qo‘llash uchun Mahsulotlar sahifasida tanlang.
    </p>
    <div class="panel mt-6 overflow-x-auto p-0">
      <table class="w-full min-w-[760px] text-sm">
        <thead class="bg-muted/60 text-left text-[11px] uppercase tracking-[0.12em] text-muted-foreground">
          <tr>
            <th class="px-4 py-3 font-semibold">Mahsulot</th>
            <th class="px-4 py-3 font-semibold">Turi</th>
            <th class="px-4 py-3 font-semibold">Holat</th>
            <th class="px-4 py-3 font-semibold">1 dona</th>
            <th class="px-4 py-3 font-semibold">Ulgurji (10+)</th>
            <th class="px-4 py-3 font-semibold">Narx holati</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="p in list.products.value" :key="p.id" class="border-t border-border hover:bg-background">
            <td class="px-4 py-2">
              <RouterLink :to="`/products/${p.id}`" class="flex items-center gap-3 hover:text-primary">
                <img
                  v-if="p.primaryPreviewUrl"
                  :src="p.primaryPreviewUrl"
                  alt=""
                  class="h-12 w-9 object-contain"
                  loading="lazy"
                />
                <span
                  ><span class="font-semibold">{{ p.nameUz ?? p.id }}</span
                  ><br /><span class="text-xs text-muted-foreground">{{ p.id }}</span></span
                >
              </RouterLink>
            </td>
            <td class="px-4 py-2">{{ store.garmentName(p.garmentType) }}</td>
            <td class="px-4 py-2"><StatusBadge :status="p.status" /></td>
            <td class="px-4 py-2">
              <input
                :value="p.retailPrice ?? ''"
                type="number"
                min="0"
                step="1000"
                class="field w-36"
                :aria-label="`${p.id} 1 dona narxi`"
                :disabled="sheet.saving.value !== null"
                @change="commit(p.id, 'retail', $event, p.retailPrice)"
              />
            </td>
            <td class="px-4 py-2">
              <input
                :value="p.wholesalePrice !== p.retailPrice ? (p.wholesalePrice ?? '') : ''"
                type="number"
                min="0"
                step="1000"
                class="field w-36"
                :aria-label="`${p.id} ulgurji narxi`"
                :disabled="sheet.saving.value !== null"
                @change="commit(p.id, 'wholesale', $event, p.wholesalePrice)"
              />
            </td>
            <td class="px-4 py-2 text-xs">
              <span v-if="p.pricingStatus === 'confirmed'" class="chip bg-success-soft text-success-soft-foreground"
                >tasdiqlangan</span
              >
              <span v-else-if="p.pricingStatus === 'draft'" class="chip bg-warning-soft text-warning-soft-foreground"
                >qoralama</span
              >
              <span v-else class="text-muted-foreground">narx yo‘q</span>
              <span class="ml-2 text-muted-foreground">{{ formatUzs(p.retailPrice) }}</span>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
