<script setup lang="ts">
import { useIdentitySession } from '../../application/common/UseIdentitySession';
const { canEdit: sessionCanEdit } = useIdentitySession();
import { useRefresh } from '@wow-two-beta/fe/query';
import { SkeletonStateGroup, SkeletonStateSlot } from '@wow-two-beta/fe/presentation/feedback';
import RefreshButton from '../common/RefreshButton.vue';
import { requestConfirmation } from '../../application/common/UseFeedback';
import TableSkeletonRows from '../common/TableSkeletonRows.vue';
import ResultEmpty from '../common/ResultEmpty.vue';
import { computed, onMounted } from 'vue';
import { RouterLink } from 'vue-router';
import { Save } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/fe/foundation/icons';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Badge, Card, type BadgeVariant } from '@wow-two-beta/fe/presentation/display';
import { CheckboxInput, NumberInput } from '@wow-two-beta/fe/presentation/forms';
import { useStockSheet } from '../../application/catalogue';
import { useUiLocale } from '../../application/shared/UseUiLocale';

/** Pieces on hand per size; drives availability in the public feed and the order sheet. */
defineOptions({ name: 'StockPage' });
const sheet = useStockSheet();
const { t } = useUiLocale();
const { refresh: refreshRows, refreshing } = useRefresh(() => sheet.load());
const resultBusy = computed(() => sheet.loading.value || refreshing.value);
async function refresh() {
  if (resultBusy.value || sheet.busy.value || sheet.rows.value.some((item) => item.saving)) return;
  if (
    sheet.dirtyCount.value &&
    !(await requestConfirmation(t('Discard unsaved changes?'), { confirmLabel: t('Discard changes') }))
  )
    return;
  await refreshRows();
}
const availabilityLabels = computed<Record<string, string>>(() => ({
  'in-stock': t('In stock'),
  'sold-out': t('Sold out'),
  'made-to-order': t('Made to order'),
  unknown: t('Stock not tracked'),
}));
const allSizes = computed(() => {
  const order = ['XS', 'S', 'M', 'L', 'XL', 'XXL', 'XXXL'];
  const sizes = new Set(sheet.rows.value.flatMap((item) => item.row.sizes));
  return [...sizes].sort((a, b) => (order.indexOf(a) + 1 || 99) - (order.indexOf(b) + 1 || 99) || a.localeCompare(b));
});
const stockColumns = computed(() => (allSizes.value.length ? allSizes.value : ['S', 'M', 'L']));
const tone: Record<string, BadgeVariant> = {
  'in-stock': 'success',
  'sold-out': 'danger',
  'made-to-order': 'brand',
  unknown: 'neutral',
};
onMounted(() => sheet.load());
</script>

<template>
  <section>
    <div class="flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 class="font-editorial text-4xl tracking-tight">{{ t('Stock') }}</h1>
        <p class="mt-2 max-w-2xl text-sm text-muted-foreground">
          {{ t('Pieces by size') }}:
          <SkeletonStateSlot class="catalogue-value" :is-loading="!sheet.loaded.value && resultBusy">{{
            sheet.loaded.value ? sheet.total.value : '—'
          }}</SkeletonStateSlot>
          · {{ t('Only availability is public; exact quantities stay private.') }}
        </p>
      </div>
      <div class="flex flex-wrap items-center gap-3">
        <RefreshButton
          :busy="resultBusy"
          :disabled="sheet.busy.value || sheet.rows.value.some((item) => item.saving)"
          @refresh="refresh"
        />
        <Button
          :is-disabled="!sessionCanEdit || sheet.dirtyCount.value === 0 || resultBusy"
          :is-loading="sheet.busy.value"
          @click="sheet.saveAll()"
        >
          <template #leading><Icon :icon="Save" :size="15" /></template>
          {{ t('Save changes ({count})', { count: sheet.dirtyCount.value }) }}
        </Button>
      </div>
    </div>
    <div class="catalogue-result-region">
      <SkeletonStateGroup :is-loading="resultBusy" :label="t('Loading stock')">
        <ResultEmpty
          v-if="!sheet.rows.value.length && (sheet.loaded.value || !resultBusy)"
          :label="sheet.loaded.value ? t('No products found.') : t('Content unavailable')"
        />
        <Card v-else radius="2xl" class="overflow-x-auto">
          <table class="w-full min-w-[900px] text-sm">
            <thead class="bg-muted/60 text-left text-[11px] uppercase tracking-[0.12em] text-muted-foreground">
              <tr>
                <th class="px-4 py-3 font-semibold">{{ t('Product') }}</th>
                <th class="px-2 py-3 font-semibold">{{ t('Tracking') }}</th>
                <th v-for="size in stockColumns" :key="size" class="w-28 px-1 py-3 text-center font-semibold">
                  {{ size }}
                </th>
                <th class="px-4 py-3 font-semibold">{{ t('Status') }}</th>
                <th class="px-4 py-3"></th>
              </tr>
            </thead>
            <tbody>
              <TableSkeletonRows v-if="resultBusy && !sheet.loaded.value" :columns="stockColumns.length + 4" />
              <tr
                v-for="item in sheet.rows.value"
                :key="item.row.id"
                :class="['border-t border-border', sheet.isDirty(item) ? 'bg-warning-soft/40' : '']"
              >
                <td class="px-4 py-2">
                  <RouterLink
                    :to="`/products/${item.row.id}`"
                    class="flex items-center gap-3 hover:text-primary-soft-foreground"
                  >
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
                    ><SkeletonStateSlot class="catalogue-value"
                      ><CheckboxInput
                        size="sm"
                        :model-value="item.row.stock !== null"
                        :is-disabled="!sessionCanEdit || item.saving || resultBusy"
                        @update:model-value="sheet.track(item, $event)" /></SkeletonStateSlot
                    >{{ t('Track quantities') }}</label
                  >
                  <label class="mt-1 flex items-center gap-1.5"
                    ><SkeletonStateSlot class="catalogue-value"
                      ><CheckboxInput
                        v-model="item.row.madeToOrder"
                        size="sm"
                        :is-disabled="!sessionCanEdit || item.saving || resultBusy" /></SkeletonStateSlot
                    >{{ t('Made to order') }}</label
                  >
                </td>
                <td v-for="size in stockColumns" :key="size" class="px-1 py-2 text-center">
                  <SkeletonStateSlot class="catalogue-value" :is-block="true">
                    <NumberInput
                      v-if="item.row.sizes.includes(size)"
                      :model-value="item.row.stock?.[size] ?? null"
                      min="0"
                      size="sm"
                      class="mx-auto w-24"
                      :is-disabled="!sessionCanEdit || item.row.stock === null || item.saving || resultBusy"
                      :aria-label="t('Quantity for {product}, size {size}', { product: item.row.id, size })"
                      @update:model-value="sheet.setQuantity(item, size, $event ?? 0)"
                    />
                    <span v-else class="text-muted-foreground">—</span>
                  </SkeletonStateSlot>
                </td>
                <td class="px-4 py-2">
                  <SkeletonStateSlot class="catalogue-value" :is-block="true">
                    <Badge :variant="tone[item.row.availability]" size="sm">{{
                      availabilityLabels[item.row.availability]
                    }}</Badge>
                  </SkeletonStateSlot>
                </td>
                <td class="px-4 py-2 text-right">
                  <Button
                    variant="outline"
                    tone="neutral"
                    :is-disabled="!sessionCanEdit || !sheet.isDirty(item) || resultBusy"
                    :is-loading="item.saving"
                    @click="sheet.save(item)"
                  >
                    {{ item.saving ? t('Saving…') : t('Save') }}
                  </Button>
                </td>
              </tr>
            </tbody>
          </table>
        </Card>
      </SkeletonStateGroup>
    </div>
  </section>
</template>
