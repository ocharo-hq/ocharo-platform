<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { RouterLink, useRouter } from 'vue-router';
import { CheckSquare, CirclePlus, Copy, ImageOff, Search, Square } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { useCatalogueStore, useProductList, useCollections } from '../../application/catalogue';
import { formatUzs, statusLabels } from '../../domain/catalogue/CatalogueRules';
import ContentSkeleton from '../common/ContentSkeleton.vue';
import { useUiLocale } from '../../application/shared/UseUiLocale';
import CatalogueSelect from '../common/CatalogueSelect.vue';
import StatusBadge from './components/StatusBadge.vue';

/** Product grid with filters, selection and bulk status/price actions. */
defineOptions({ name: 'ProductsPage' });
const store = useCatalogueStore();
const { t, uiLocale } = useUiLocale();
const router = useRouter();
const collections = useCollections();
const collectionNames = computed(
  () =>
    new Map(
      collections.collections.value.map((item) => [
        item.document.id,
        item.document.names[uiLocale.value] || item.document.names.uz,
      ]),
    ),
);
const availabilityNames: Record<string, string> = {
  'in-stock': 'In stock',
  'sold-out': 'Sold out',
  'made-to-order': 'Made to order',
  unknown: 'Stock not tracked',
};
const list = useProductList({ q: '', garmentType: '', status: '' });
const percent = ref(10);
const tier = ref<'retail' | 'wholesale' | ''>('');
const counts = computed(() => {
  const all = list.products.value;
  return {
    total: all.length,
    active: all.filter((p) => p.status === 'active').length,
    issues: all.reduce(
      (n, p) => n + p.openIssues + (p.unconfirmedTranslations ?? Math.max(0, 3 - p.localesApproved)),
      0,
    ),
  };
});
async function duplicate(id: string) {
  const created = await list.duplicate(id);
  if (created) await router.push(`/products/${created}`);
}
onMounted(() => Promise.all([list.load(), collections.load()]));
</script>

<template>
  <section>
    <div class="flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 class="font-editorial text-4xl tracking-tight">{{ t('Products') }}</h1>
        <p class="mt-2 text-sm text-muted-foreground">
          {{
            t('{total} products · {active} active · {issues} issues', {
              total: counts.total,
              active: counts.active,
              issues: counts.issues,
            })
          }}
        </p>
      </div>
      <RouterLink to="/products/new" class="btn-primary">
        <Icon :icon="CirclePlus" :size="16" />{{ t('New product') }}</RouterLink
      >
    </div>

    <div class="panel mt-6 grid gap-3 p-4 md:grid-cols-[1fr_auto_auto_auto]">
      <label class="relative">
        <span class="sr-only">{{ t('Search') }}</span>
        <Icon :icon="Search" :size="16" class="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
        <input v-model="list.filter.q" class="field pl-9" :placeholder="t('Search by name, ID, fabric or tag')" />
      </label>
      <CatalogueSelect
        :model-value="list.filter.garmentType ?? ''"
        :label="t('Garment type')"
        class="md:w-44"
        :options="[
          { value: '', label: t('All types') },
          ...store.garmentTypes.value.map((g) => ({ value: g.key, label: g[uiLocale] })),
        ]"
        @update:model-value="list.filter.garmentType = $event"
      />
      <CatalogueSelect
        :model-value="list.filter.status ?? ''"
        :label="t('Status')"
        class="md:w-40"
        :options="[
          { value: '', label: t('All statuses') },
          ...Object.keys(statusLabels).map((key) => ({
            value: key,
            label: t(key === 'active' ? 'Active' : key === 'draft' ? 'Draft' : 'Archived'),
          })),
        ]"
        @update:model-value="list.filter.status = $event"
      />
      <button class="btn-outline" @click="list.selectedCount.value ? list.clearSelection() : list.selectAll()">
        <Icon :icon="list.selectedCount.value ? CheckSquare : Square" :size="15" />
        {{ list.selectedCount.value ? `${list.selectedCount.value} · ${t('Selected')}` : t('Select all') }}
      </button>
    </div>

    <div
      v-if="list.selectedCount.value"
      class="panel mt-3 flex flex-wrap items-center gap-3 border-primary/30 bg-primary-soft/40 p-4 text-sm"
    >
      <span class="font-semibold">{{ list.selectedCount.value }} · {{ t('Products') }}:</span>
      <button class="btn-outline" @click="list.setStatus('active')">{{ t('Activate') }}</button>
      <button class="btn-outline" @click="list.setStatus('draft')">{{ t('Move to drafts') }}</button>
      <button class="btn-outline" @click="list.setStatus('archived')">{{ t('Archive') }}</button>
      <span class="mx-2 h-6 w-px bg-border" />
      <label class="flex items-center gap-2"
        >{{ t('Price')
        }}<input v-model.number="percent" type="number" class="field w-20" :aria-label="t('Percentage')" />%
      </label>
      <CatalogueSelect
        v-model="tier"
        :label="t('Price')"
        class="w-40"
        :options="[
          { value: '', label: t('All bands') },
          { value: 'retail', label: t('1–9 pieces') },
          { value: 'wholesale', label: t('10+ pieces') },
        ]"
      />
      <button class="btn-primary" @click="list.adjustPrices('percent', percent, 1000, tier || null)">
        {{ t('Apply (round to 1,000)') }}
      </button>
    </div>

    <ContentSkeleton
      v-if="list.loading.value && !list.products.value.length"
      :label="t('Loading products')"
      :cards="6"
    />
    <p v-else-if="!list.products.value.length" class="panel mt-6 p-10 text-center text-sm text-muted-foreground">
      {{ t('No products found. Clear filters or import products in Settings.') }}
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
          :aria-label="`${t('Select')} ${product.id}`"
          @click="list.toggle(product.id)"
        >
          <Icon :icon="list.selected.value.has(product.id) ? CheckSquare : Square" :size="16" />
        </button>
        <button
          class="absolute right-3 top-3 z-10 rounded-md bg-card/90 p-1 text-muted-foreground opacity-0 shadow-sm transition group-hover:opacity-100 hover:text-primary focus:opacity-100"
          :aria-label="`${t('Duplicate')} ${product.id}`"
          :title="t('Duplicate as a new variant')"
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
                {{
                  (uiLocale === 'ru' ? product.nameRu : uiLocale === 'en' ? product.nameEn : product.nameUz) ??
                  store.garmentName(product.garmentType, uiLocale)
                }}
              </p>
              <StatusBadge :status="product.status" />
            </div>
            <p class="mt-1 text-xs text-muted-foreground">
              {{ product.id }} · {{ store.garmentName(product.garmentType, uiLocale) }}
            </p>
            <div class="mt-3 flex items-baseline justify-between text-sm">
              <span class="font-semibold">{{ formatUzs(product.retailPrice) }}</span>
              <span
                v-if="product.wholesalePrice !== null && product.wholesalePrice !== product.retailPrice"
                class="text-xs text-muted-foreground"
                >10+ · {{ formatUzs(product.wholesalePrice) }}</span
              >
            </div>
            <div v-if="product.collectionIds?.length" class="mt-2 flex flex-wrap gap-1" :aria-label="t('Collections')">
              <span
                v-for="id in product.collectionIds.slice(0, 2)"
                :key="id"
                class="chip bg-secondary text-secondary-foreground"
                >{{ collectionNames.get(id) ?? id }}</span
              ><span v-if="product.collectionIds.length > 2" class="chip bg-secondary text-secondary-foreground"
                >+{{ product.collectionIds.length - 2 }}</span
              >
            </div>
            <div class="mt-3 flex flex-wrap gap-1.5 text-[10px]">
              <span class="chip bg-secondary text-secondary-foreground"
                >{{ product.localesApproved }}/{{ product.languagesTotal ?? 3 }} {{ t('languages confirmed') }}</span
              >
              <span
                v-if="(product.unconfirmedTranslations ?? Math.max(0, 3 - product.localesApproved)) > 0"
                class="chip bg-warning-soft text-warning-soft-foreground"
                >{{ product.unconfirmedTranslations ?? Math.max(0, 3 - product.localesApproved) }}
                {{ t('unconfirmed translations') }}</span
              >
              <span v-if="product.openIssues" class="chip bg-warning-soft text-warning-soft-foreground"
                >{{ product.openIssues }} {{ t('open questions') }}</span
              >
              <span v-if="product.pricingStatus === 'draft'" class="chip bg-muted text-muted-foreground">{{
                t('Draft price')
              }}</span>
              <span
                v-if="product.availability !== 'unknown'"
                class="chip bg-primary-soft text-primary-soft-foreground"
                >{{ t(availabilityNames[product.availability] ?? product.availability) }}</span
              >
            </div>
          </div>
        </RouterLink>
      </li>
    </ul>
  </section>
</template>
