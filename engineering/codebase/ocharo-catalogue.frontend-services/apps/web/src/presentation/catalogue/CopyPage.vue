<script setup lang="ts">
import { useRefresh } from '@wow-two-beta/ui-vue/query';
import { SkeletonStateGroup, SkeletonStateSlot } from '@wow-two-beta/ui-vue/presentation/feedback';
import RefreshButton from '../common/RefreshButton.vue';
import { requestConfirmation } from '../../application/common/UseFeedback';
import TableSkeletonRows from '../common/TableSkeletonRows.vue';
import ResultEmpty from '../common/ResultEmpty.vue';
import { computed, onMounted, ref } from 'vue';
import { RouterLink } from 'vue-router';
import { Check, Save } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { useCatalogueStore, useCopyGrid } from '../../application/catalogue';
import type { LocaleCode } from '../../domain/catalogue/CatalogueModels';
import { locales } from '../../domain/catalogue/CatalogueRules';
import { useUiLocale } from '../../application/shared/UseUiLocale';
import StatusBadge from './components/StatusBadge.vue';

/** Fill and approve uz/ru/en copy for every product in one grid. */
defineOptions({ name: 'CopyPage' });
type Field = 'name' | 'fabric' | 'decoration' | 'description';
const store = useCatalogueStore();
const grid = useCopyGrid();
const { t } = useUiLocale();
const { refresh: refreshRows, refreshing } = useRefresh(() => grid.load());
const resultBusy = computed(() => grid.loading.value || refreshing.value);
async function refresh() {
  if (resultBusy.value || grid.busy.value || grid.rows.value.some((item) => item.saving)) return;
  if (
    grid.dirtyCount.value &&
    !(await requestConfirmation(t('Discard unsaved changes?'), { confirmLabel: t('Discard changes') }))
  )
    return;
  await refreshRows();
}
const languageNames = computed(() => ({ uz: t('Uzbek'), ru: t('Russian'), en: t('English') }));
const field = ref<Field>('name');
const onlyMissing = ref(false);
const fields = computed<{ key: Field; label: string }[]>(() => [
  { key: 'name', label: t('Name') },
  { key: 'fabric', label: t('Fabric') },
  { key: 'decoration', label: t('Decoration') },
  { key: 'description', label: t('Description') },
]);
const visible = computed(() =>
  grid.rows.value.filter(
    (item) =>
      !onlyMissing.value ||
      locales.some(
        (code) => !item.row.locales[code][field.value]?.trim() || item.row.locales[code].status !== 'approved',
      ),
  ),
);
const approved = computed(() =>
  grid.rows.value.reduce(
    (n, item) => n + locales.filter((code) => item.row.locales[code].status === 'approved').length,
    0,
  ),
);
function setValue(code: LocaleCode, item: (typeof grid.rows.value)[number], value: string) {
  item.row.locales[code][field.value] = value.trim() === '' ? null : value;
}
function toggleApproved(code: LocaleCode, item: (typeof grid.rows.value)[number]) {
  const locale = item.row.locales[code];
  locale.status = locale.status === 'approved' ? 'draft' : 'approved';
}
onMounted(() => grid.load());
</script>

<template>
  <section>
    <div class="flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 class="font-editorial text-4xl tracking-tight">{{ t('Product copy') }}</h1>
        <p class="mt-2 max-w-2xl text-sm text-muted-foreground">
          {{ t('Fill and approve copy in three languages.') }}
          {{ t('{approved} / {total} language versions approved.', { approved, total: grid.rows.value.length * 3 }) }}
          {{ t('A name is required before approval.') }}
        </p>
      </div>
      <div class="flex flex-wrap items-center gap-3">
        <RefreshButton
          :busy="resultBusy"
          :disabled="grid.busy.value || grid.rows.value.some((item) => item.saving)"
          @refresh="refresh"
        />
        <button
          class="btn-primary"
          :disabled="grid.dirtyCount.value === 0 || grid.busy.value || resultBusy"
          :aria-busy="grid.busy.value"
          @click="grid.saveAll()"
        >
          <Icon :icon="Save" :size="15" />{{ t('Save {count} changes', { count: grid.dirtyCount.value }) }}
        </button>
      </div>
    </div>
    <div class="panel mt-6 flex flex-wrap items-center gap-3 p-4">
      <div role="tablist" class="flex rounded-lg bg-muted p-1 text-sm">
        <button
          v-for="f in fields"
          :key="f.key"
          role="tab"
          :aria-selected="field === f.key"
          :class="[
            'rounded-md px-3 py-1.5',
            field === f.key ? 'bg-card font-semibold shadow-sm' : 'text-muted-foreground',
          ]"
          @click="field = f.key"
        >
          {{ f.label }}
        </button>
      </div>
      <label class="flex items-center gap-2 text-sm"
        ><input v-model="onlyMissing" type="checkbox" />{{ t('Only missing or unapproved') }}</label
      >
    </div>
    <div class="catalogue-result-region">
      <SkeletonStateGroup :is-loading="resultBusy" :label="t('Loading product copy')">
        <ResultEmpty
          v-if="!visible.length && (grid.loaded.value || !resultBusy)"
          :label="grid.loaded.value ? t('No products found.') : t('Content unavailable')"
        />
        <div v-else class="panel overflow-x-auto p-0">
          <table class="w-full min-w-[980px] text-sm">
            <thead class="bg-muted/60 text-left text-[11px] uppercase tracking-[0.12em] text-muted-foreground">
              <tr>
                <th class="px-4 py-3 font-semibold">{{ t('Product') }}</th>
                <th v-for="code in locales" :key="code" class="px-4 py-3 font-semibold">{{ languageNames[code] }}</th>
                <th class="px-4 py-3"></th>
              </tr>
            </thead>
            <tbody>
              <TableSkeletonRows v-if="resultBusy && !grid.loaded.value" :columns="5" />
              <tr
                v-for="item in visible"
                :key="item.row.id"
                :class="['border-t border-border align-top', grid.isDirty(item) ? 'bg-warning-soft/40' : '']"
              >
                <td class="w-56 px-4 py-3">
                  <RouterLink
                    :to="`/products/${item.row.id}`"
                    class="flex items-center gap-3 hover:text-primary-soft-foreground"
                  >
                    <img
                      v-if="item.row.previewUrl"
                      :src="item.row.previewUrl"
                      alt=""
                      class="h-14 w-10 object-contain"
                      loading="lazy"
                    />
                    <span>
                      <span class="block font-semibold">{{ item.row.locales.uz.name ?? item.row.id }}</span>
                      <span class="block text-xs text-muted-foreground"
                        >{{ item.row.id }} · {{ store.garmentName(item.row.garmentType) }}</span
                      >
                    </span>
                  </RouterLink>
                  <StatusBadge class="mt-2" :status="item.row.status" />
                </td>
                <td v-for="code in locales" :key="code" class="px-2 py-3">
                  <SkeletonStateSlot class="catalogue-value" :is-block="true">
                    <textarea
                      v-if="field === 'description'"
                      :value="item.row.locales[code][field] ?? ''"
                      rows="3"
                      class="field"
                      :aria-label="`${item.row.id} ${languageNames[code]}`"
                      :disabled="item.saving || resultBusy"
                      @input="setValue(code, item, ($event.target as HTMLTextAreaElement).value)"
                    ></textarea>
                    <input
                      v-else
                      :value="item.row.locales[code][field] ?? ''"
                      class="field"
                      :aria-label="`${item.row.id} ${languageNames[code]}`"
                      :disabled="item.saving || resultBusy"
                      @input="setValue(code, item, ($event.target as HTMLInputElement).value)"
                    /> </SkeletonStateSlot
                  ><button
                    :class="[
                      'chip mt-1.5',
                      item.row.locales[code].status === 'approved'
                        ? 'bg-success-soft text-success-soft-foreground'
                        : 'bg-muted text-muted-foreground',
                    ]"
                    :aria-pressed="item.row.locales[code].status === 'approved'"
                    :disabled="item.saving || resultBusy"
                    @click="toggleApproved(code, item)"
                  >
                    <Icon v-if="item.row.locales[code].status === 'approved'" :icon="Check" :size="11" />
                    {{ item.row.locales[code].status === 'approved' ? t('Approved') : t('Draft') }}
                  </button>
                </td>
                <td class="px-4 py-3 text-right">
                  <button
                    class="btn-outline"
                    :disabled="!grid.isDirty(item) || item.saving || resultBusy"
                    @click="grid.save(item)"
                  >
                    {{ item.saving ? t('Saving…') : t('Save') }}
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </SkeletonStateGroup>
    </div>
  </section>
</template>
