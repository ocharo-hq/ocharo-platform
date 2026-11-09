<script setup lang="ts">
import { useIdentitySession } from '../../application/common/UseIdentitySession';
const { canEdit: sessionCanEdit } = useIdentitySession();
import { useRefresh } from '@wow-two-beta/fe/query';
import { SkeletonStateGroup, SkeletonStateSlot } from '@wow-two-beta/fe/presentation/feedback';
import RefreshButton from '../common/RefreshButton.vue';
import ResultEmpty from '../common/ResultEmpty.vue';
import { computed, onMounted, ref } from 'vue';
import { RouterLink, useRouter } from 'vue-router';
import { CheckSquare, CirclePlus, Copy, ImageOff, Square } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/fe/foundation/icons';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Badge, Card } from '@wow-two-beta/fe/presentation/display';
import { NumberInput, SearchInput } from '@wow-two-beta/fe/presentation/forms';
import { useCatalogueSettings, useProductList, useCollections } from '../../application/catalogue';
import { formatUzs, statusLabels } from '../../domain/catalogue/CatalogueRules';
import ContentSkeleton from '../common/ContentSkeleton.vue';
import { useUiLocale } from '../../application/shared/UseUiLocale';
import CatalogueSelect from '../common/CatalogueSelect.vue';
import StatusBadge from './components/StatusBadge.vue';

/** Product grid with filters, selection and bulk status/price actions. */
defineOptions({ name: 'ProductsPage' });
const settings = useCatalogueSettings();
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
const { refresh, refreshing } = useRefresh(() => list.load());
const resultBusy = computed(() => list.loading.value || refreshing.value);
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
          <SkeletonStateSlot class="catalogue-value" :is-loading="!list.loaded.value && resultBusy">{{
            t('{total} products · {active} active · {issues} issues', {
              total: list.loaded.value ? counts.total : '—',
              active: list.loaded.value ? counts.active : '—',
              issues: list.loaded.value ? counts.issues : '—',
            })
          }}</SkeletonStateSlot>
        </p>
      </div>
      <div class="flex flex-wrap items-center gap-3">
        <RefreshButton :busy="resultBusy" :disabled="!!list.pending.value" @refresh="refresh" />
        <Button v-if="sessionCanEdit" as-child>
          <RouterLink to="/products/new"><Icon :icon="CirclePlus" :size="16" />{{ t('New product') }}</RouterLink>
        </Button>
      </div>
    </div>

    <Card radius="2xl" padding="lg" class="mt-6 grid gap-3 md:grid-cols-[1fr_auto_auto_auto]">
      <SearchInput
        v-model="list.filter.q"
        :aria-label="t('Search')"
        :placeholder="t('Search by name, ID, fabric or tag')"
      />
      <CatalogueSelect
        :model-value="list.filter.garmentType ?? ''"
        :label="t('Garment type')"
        class="md:w-44"
        :options="[
          { value: '', label: t('All types') },
          ...settings.garmentTypes.value.map((g) => ({ value: g.key, label: g[uiLocale] })),
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
      <Button
        variant="outline"
        tone="neutral"
        @click="list.selectedCount.value ? list.clearSelection() : list.selectAll()"
      >
        <template #leading><Icon :icon="list.selectedCount.value ? CheckSquare : Square" :size="15" /></template>
        {{ list.selectedCount.value ? `${list.selectedCount.value} · ${t('Selected')}` : t('Select all') }}
      </Button>
    </Card>

    <Card
      v-if="list.selectedCount.value"
      variant="soft"
      tone="primary"
      radius="2xl"
      padding="lg"
      class="mt-3 flex flex-wrap items-center gap-3 text-sm"
    >
      <span class="font-semibold">{{ list.selectedCount.value }} · {{ t('Products') }}:</span>
      <Button
        variant="outline"
        tone="neutral"
        :is-disabled="!sessionCanEdit || (!!list.pending.value && list.pending.value !== 'status:active')"
        :is-loading="list.pending.value === 'status:active'"
        @click="list.setStatus('active')"
      >
        {{ list.pending.value === 'status:active' ? t('Working…') : t('Activate') }}
      </Button>
      <Button
        variant="outline"
        tone="neutral"
        :is-disabled="!sessionCanEdit || (!!list.pending.value && list.pending.value !== 'status:draft')"
        :is-loading="list.pending.value === 'status:draft'"
        @click="list.setStatus('draft')"
      >
        {{ list.pending.value === 'status:draft' ? t('Working…') : t('Move to drafts') }}
      </Button>
      <Button
        variant="outline"
        tone="neutral"
        :is-disabled="!sessionCanEdit || (!!list.pending.value && list.pending.value !== 'status:archived')"
        :is-loading="list.pending.value === 'status:archived'"
        @click="list.setStatus('archived')"
      >
        {{ list.pending.value === 'status:archived' ? t('Working…') : t('Archive') }}
      </Button>
      <span class="mx-2 h-6 w-px bg-border" />
      <span class="flex items-center gap-2"
        >{{ t('Price') }}
        <NumberInput
          :model-value="percent"
          class="w-28"
          :aria-label="t('Percentage')"
          @update:model-value="percent = $event ?? 0"
        />%
      </span>
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
      <Button
        :is-disabled="!sessionCanEdit || (!!list.pending.value && list.pending.value !== 'prices')"
        :is-loading="list.pending.value === 'prices'"
        @click="list.adjustPrices('percent', percent, 1000, tier || null)"
      >
        {{ list.pending.value === 'prices' ? t('Working…') : t('Apply (round to 1,000)') }}
      </Button>
    </Card>

    <div class="catalogue-result-region">
      <ContentSkeleton
        v-if="resultBusy && !list.loaded.value"
        :label="t('Loading products')"
        :cards="6"
        variant="product"
      />
      <SkeletonStateGroup v-else :is-loading="resultBusy" :label="t('Loading products')">
        <ResultEmpty
          v-if="!list.products.value.length"
          :label="
            list.loaded.value
              ? t('No products found. Clear filters or import products in Settings.')
              : t('Content unavailable')
          "
        />
        <ul v-else class="catalogue-products-grid">
          <li
            v-for="product in list.products.value"
            :key="product.id"
            :class="[
              'group relative overflow-hidden rounded-2xl border bg-card transition hover:shadow-lg',
              list.selected.value.has(product.id) ? 'border-primary ring-2 ring-primary/20' : 'border-border',
            ]"
          >
            <Button
              variant="outline"
              tone="neutral"
              shape="square"
              class="catalogue-image-action absolute left-3 top-3 z-10 size-11 rounded-xl shadow-sm sm:size-9"
              :aria-pressed="list.selected.value.has(product.id)"
              :aria-label="`${t('Select')} ${product.id}`"
              @click="list.toggle(product.id)"
            >
              <Icon :icon="list.selected.value.has(product.id) ? CheckSquare : Square" :size="16" />
            </Button>
            <Button
              variant="outline"
              tone="neutral"
              shape="square"
              size="sm"
              class="catalogue-image-action absolute right-3 top-3 z-10 opacity-0 shadow-sm group-hover:opacity-100 focus-visible:opacity-100"
              :aria-label="`${t('Duplicate')} ${product.id}`"
              :title="t('Duplicate as a new variant')"
              :is-disabled="
                !sessionCanEdit || (!!list.pending.value && list.pending.value !== `duplicate:${product.id}`)
              "
              :is-loading="list.pending.value === `duplicate:${product.id}`"
              @click="duplicate(product.id)"
            >
              <Icon :icon="Copy" :size="16" />
            </Button>
            <RouterLink :to="`/products/${product.id}`" class="block">
              <SkeletonStateSlot class="catalogue-value" :is-block="true"
                ><div
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
              </SkeletonStateSlot>
              <div class="border-t border-border p-4">
                <div class="flex items-center justify-between gap-2">
                  <p class="font-editorial text-lg leading-tight">
                    {{
                      (uiLocale === 'ru' ? product.nameRu : uiLocale === 'en' ? product.nameEn : product.nameUz) ??
                      settings.garmentName(product.garmentType, uiLocale)
                    }}
                  </p>
                  <SkeletonStateSlot class="catalogue-value"
                    ><StatusBadge :status="product.status"
                  /></SkeletonStateSlot>
                </div>
                <p class="mt-1 text-xs text-muted-foreground">
                  {{ product.id }} · {{ settings.garmentName(product.garmentType, uiLocale) }}
                </p>
                <SkeletonStateSlot class="catalogue-value" :is-block="true"
                  ><div class="mt-3 flex items-baseline justify-between text-sm">
                    <span class="font-semibold">{{ formatUzs(product.retailPrice) }}</span>
                    <span
                      v-if="product.wholesalePrice !== null && product.wholesalePrice !== product.retailPrice"
                      class="text-xs text-muted-foreground"
                      >10+ · {{ formatUzs(product.wholesalePrice) }}</span
                    >
                  </div>
                </SkeletonStateSlot>
                <div
                  v-if="product.collectionIds?.length"
                  class="mt-2 flex flex-wrap gap-1"
                  :aria-label="t('Collections')"
                >
                  <Badge v-for="id in product.collectionIds.slice(0, 2)" :key="id" size="sm">{{
                    collectionNames.get(id) ?? id
                  }}</Badge
                  ><Badge v-if="product.collectionIds.length > 2" size="sm"
                    >+{{ product.collectionIds.length - 2 }}</Badge
                  >
                </div>
                <SkeletonStateSlot class="catalogue-value" :is-block="true"
                  ><div class="mt-3 flex flex-wrap gap-1.5 text-[10px]">
                    <Badge
                      size="sm"
                      :variant="product.localesApproved < (product.languagesTotal ?? 3) ? 'warning' : 'success'"
                      >{{ product.localesApproved }}/{{ product.languagesTotal ?? 3 }}
                      {{ t('languages confirmed') }}</Badge
                    >
                    <Badge v-if="product.openIssues" size="sm" variant="warning"
                      >{{ product.openIssues }} {{ t('open questions') }}</Badge
                    >
                    <Badge v-if="product.pricingStatus === 'draft'" size="sm">{{ t('Draft price') }}</Badge>
                    <Badge v-if="product.availability !== 'unknown'" size="sm" variant="brand">{{
                      t(availabilityNames[product.availability] ?? product.availability)
                    }}</Badge>
                  </div>
                </SkeletonStateSlot>
              </div>
            </RouterLink>
          </li>
        </ul>
      </SkeletonStateGroup>
    </div>
  </section>
</template>
