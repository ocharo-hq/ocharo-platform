<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { RouterLink, useRouter } from 'vue-router';
import { CheckSquare, Copy, ImageOff, Search, Square } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { useCatalogueStore, useProductList } from '../../application/catalogue';
import { availabilityLabels, formatUzs, statusLabels } from '../../domain/catalogue/CatalogueRules';
import StatusBadge from './components/StatusBadge.vue';

/** Product grid with filters, selection and bulk status/price actions. */
defineOptions({ name: 'ProductsPage' });
const store = useCatalogueStore();
const router = useRouter();
const list = useProductList({ q: '', garmentType: '', status: '' });
const percent = ref(10);
const tier = ref<'retail' | 'wholesale' | ''>('');
const counts = computed(() => {
  const all = list.products.value;
  return {
    total: all.length,
    active: all.filter((p) => p.status === 'active').length,
    issues: all.reduce((n, p) => n + p.openIssues, 0),
  };
});
async function duplicate(id: string) {
  const created = await list.duplicate(id);
  if (created) await router.push(`/products/${created}`);
}
onMounted(() => list.load());
</script>

<template>
  <section>
    <div class="flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 class="font-editorial text-4xl tracking-tight">Mahsulotlar</h1>
        <p class="mt-2 text-sm text-muted-foreground">
          {{ counts.total }} ta mahsulot · {{ counts.active }} tasi katalogda · {{ counts.issues }} ta ochiq savol
        </p>
      </div>
    </div>

    <div class="panel mt-6 grid gap-3 p-4 md:grid-cols-[1fr_auto_auto_auto]">
      <label class="relative">
        <span class="sr-only">Qidirish</span>
        <Icon :icon="Search" :size="16" class="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
        <input v-model="list.filter.q" class="field pl-9" placeholder="Nom, id, mato yoki teg bo‘yicha qidirish" />
      </label>
      <select v-model="list.filter.garmentType" class="field md:w-44" aria-label="Kiyim turi">
        <option value="">Barcha turlar</option>
        <option v-for="g in store.garmentTypes.value" :key="g.key" :value="g.key">{{ g.uz }}</option>
      </select>
      <select v-model="list.filter.status" class="field md:w-40" aria-label="Holat">
        <option value="">Barcha holatlar</option>
        <option v-for="(label, key) in statusLabels" :key="key" :value="key">{{ label }}</option>
      </select>
      <button class="btn-outline" @click="list.selectedCount.value ? list.clearSelection() : list.selectAll()">
        <Icon :icon="list.selectedCount.value ? CheckSquare : Square" :size="15" />
        {{ list.selectedCount.value ? `${list.selectedCount.value} tanlangan` : 'Hammasini tanlash' }}
      </button>
    </div>

    <div
      v-if="list.selectedCount.value"
      class="panel mt-3 flex flex-wrap items-center gap-3 border-primary/30 bg-primary-soft/40 p-4 text-sm"
    >
      <span class="font-semibold">{{ list.selectedCount.value }} ta mahsulot:</span>
      <button class="btn-outline" @click="list.setStatus('active')">Katalogga chiqarish</button>
      <button class="btn-outline" @click="list.setStatus('draft')">Qoralamaga</button>
      <button class="btn-outline" @click="list.setStatus('archived')">Arxivlash</button>
      <span class="mx-2 h-6 w-px bg-border" />
      <label class="flex items-center gap-2"
        >Narx <input v-model.number="percent" type="number" class="field w-20" aria-label="Foiz" />%
      </label>
      <select v-model="tier" class="field w-40" aria-label="Qaysi narx">
        <option value="">Barcha oraliqlar</option>
        <option value="retail">Faqat 1 dona</option>
        <option value="wholesale">Faqat ulgurji</option>
      </select>
      <button class="btn-primary" @click="list.adjustPrices('percent', percent, 1000, tier || null)">
        Qo‘llash (1 000 ga yaxlitlab)
      </button>
    </div>

    <p
      v-if="list.loading.value && !list.products.value.length"
      class="mt-10 text-sm text-muted-foreground"
      role="status"
    >
      Yuklanmoqda…
    </p>
    <p v-else-if="!list.products.value.length" class="panel mt-6 p-10 text-center text-sm text-muted-foreground">
      Mahsulot topilmadi. Filtrlarni tozalang yoki
      <RouterLink to="/settings" class="text-primary underline">importni</RouterLink> ishga tushiring.
    </p>

    <ul class="mt-6 grid grid-cols-[repeat(auto-fill,minmax(220px,1fr))] gap-4">
      <li
        v-for="product in list.products.value"
        :key="product.id"
        :class="[
          'group relative overflow-hidden rounded-2xl border bg-card transition hover:shadow-lg',
          list.selected.value.has(product.id) ? 'border-primary ring-2 ring-primary/20' : 'border-border',
        ]"
      >
        <button
          class="absolute left-3 top-3 z-10 rounded-md bg-card/90 p-1 text-muted-foreground shadow-sm hover:text-primary"
          :aria-pressed="list.selected.value.has(product.id)"
          :aria-label="`${product.id} ni tanlash`"
          @click="list.toggle(product.id)"
        >
          <Icon :icon="list.selected.value.has(product.id) ? CheckSquare : Square" :size="16" />
        </button>
        <button
          class="absolute right-3 top-3 z-10 rounded-md bg-card/90 p-1 text-muted-foreground opacity-0 shadow-sm transition group-hover:opacity-100 hover:text-primary focus:opacity-100"
          :aria-label="`${product.id} nusxasini yaratish`"
          title="Yangi variant sifatida nusxalash"
          @click="duplicate(product.id)"
        >
          <Icon :icon="Copy" :size="16" />
        </button>
        <RouterLink :to="`/products/${product.id}`" class="block">
          <div
            class="flex aspect-[3/4] items-center justify-center bg-[radial-gradient(circle_at_50%_35%,#fffdf8,#ece7dc)]"
          >
            <img
              v-if="product.primaryPreviewUrl"
              :src="product.primaryPreviewUrl"
              :alt="product.nameUz ?? product.id"
              loading="lazy"
              class="h-full w-full object-contain p-4"
            />
            <Icon v-else :icon="ImageOff" :size="28" class="text-muted-foreground" />
          </div>
          <div class="border-t border-border p-4">
            <div class="flex items-center justify-between gap-2">
              <p class="font-editorial text-lg leading-tight">
                {{ product.nameUz ?? store.garmentName(product.garmentType) }}
              </p>
              <StatusBadge :status="product.status" />
            </div>
            <p class="mt-1 text-xs text-muted-foreground">
              {{ product.id }} · {{ store.garmentName(product.garmentType) }}
            </p>
            <div class="mt-3 flex items-baseline justify-between text-sm">
              <span class="font-semibold">{{ formatUzs(product.retailPrice) }}</span>
              <span
                v-if="product.wholesalePrice !== null && product.wholesalePrice !== product.retailPrice"
                class="text-xs text-muted-foreground"
                >ulgurji {{ formatUzs(product.wholesalePrice) }}</span
              >
            </div>
            <div class="mt-3 flex flex-wrap gap-1.5 text-[10px]">
              <span class="chip bg-secondary text-secondary-foreground"
                >{{ product.localesApproved }}/3 til tasdiq</span
              >
              <span v-if="product.openIssues" class="chip bg-warning-soft text-warning-soft-foreground"
                >{{ product.openIssues }} savol</span
              >
              <span v-if="product.pricingStatus === 'draft'" class="chip bg-muted text-muted-foreground"
                >narx qoralama</span
              >
              <span
                v-if="product.availability !== 'unknown'"
                class="chip bg-primary-soft text-primary-soft-foreground"
                >{{ availabilityLabels[product.availability] }}</span
              >
            </div>
          </div>
        </RouterLink>
      </li>
    </ul>
  </section>
</template>
