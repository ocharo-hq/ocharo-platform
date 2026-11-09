<script setup lang="ts">
import { useIdentitySession } from '../../application/common/UseIdentitySession';
const { canEdit: sessionCanEdit } = useIdentitySession();
import { useRefresh } from '@wow-two-beta/fe/query';
import { SkeletonStateGroup, SkeletonStateSlot } from '@wow-two-beta/fe/presentation/feedback';
import RefreshButton from '../common/RefreshButton.vue';
import { requestConfirmation } from '../../application/common/UseFeedback';
import TableSkeletonRows from '../common/TableSkeletonRows.vue';
import ResultEmpty from '../common/ResultEmpty.vue';
import { computed, onMounted, ref } from 'vue';
import { RouterLink } from 'vue-router';
import { Check, Save } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/fe/foundation/icons';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Card } from '@wow-two-beta/fe/presentation/display';
import {
  CheckboxField,
  SegmentedPicker,
  TextAreaInput,
  TextInput,
  ToggleInput,
} from '@wow-two-beta/fe/presentation/forms';
import { useCatalogueSettings, useCopyGrid } from '../../application/catalogue';
import type { LocaleCode } from '../../domain/catalogue/CatalogueModels';
import { locales } from '../../domain/catalogue/CatalogueRules';
import { useUiLocale } from '../../application/shared/UseUiLocale';
import StatusBadge from './components/StatusBadge.vue';

/** Fill and approve uz/ru/en copy for every product in one grid. */
defineOptions({ name: 'CopyPage' });
type Field = 'name' | 'fabric' | 'decoration' | 'description';
const settings = useCatalogueSettings();
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
/** Shows another copy field; the segment already pressed stays selected. */
function chooseField(value: string | readonly string[] | null) {
  const next = fields.value.find((f) => f.key === value);
  if (next) field.value = next.key;
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
        <Button
          :is-disabled="!sessionCanEdit || grid.dirtyCount.value === 0 || resultBusy"
          :is-loading="grid.busy.value"
          @click="grid.saveAll()"
        >
          <template #leading><Icon :icon="Save" :size="15" /></template>
          {{ t('Save {count} changes', { count: grid.dirtyCount.value }) }}
        </Button>
      </div>
    </div>
    <Card radius="2xl" padding="lg" class="mt-6 flex flex-wrap items-center gap-3">
      <SegmentedPicker :model-value="field" :aria-label="t('Copy field')" @update:model-value="chooseField">
        <ToggleInput v-for="f in fields" :key="f.key" :value="f.key">{{ f.label }}</ToggleInput>
      </SegmentedPicker>
      <CheckboxField v-model="onlyMissing" :label="t('Only missing or unapproved')" />
    </Card>
    <div class="catalogue-result-region">
      <SkeletonStateGroup :is-loading="resultBusy" :label="t('Loading product copy')">
        <ResultEmpty
          v-if="!visible.length && (grid.loaded.value || !resultBusy)"
          :label="grid.loaded.value ? t('No products found.') : t('Content unavailable')"
        />
        <Card v-else radius="2xl" class="overflow-x-auto">
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
                        >{{ item.row.id }} · {{ settings.garmentName(item.row.garmentType) }}</span
                      >
                    </span>
                  </RouterLink>
                  <StatusBadge class="mt-2" :status="item.row.status" />
                </td>
                <td v-for="code in locales" :key="code" class="px-2 py-3">
                  <SkeletonStateSlot class="catalogue-value" :is-block="true">
                    <TextAreaInput
                      v-if="field === 'description'"
                      :model-value="item.row.locales[code][field] ?? ''"
                      :rows="3"
                      :aria-label="`${item.row.id} ${languageNames[code]}`"
                      :is-disabled="!sessionCanEdit || item.saving || resultBusy"
                      @update:model-value="setValue(code, item, $event)"
                    />
                    <TextInput
                      v-else
                      :model-value="item.row.locales[code][field] ?? ''"
                      :aria-label="`${item.row.id} ${languageNames[code]}`"
                      :is-disabled="!sessionCanEdit || item.saving || resultBusy"
                      @update:model-value="setValue(code, item, $event)"
                    /> </SkeletonStateSlot
                  ><Button
                    variant="soft"
                    :tone="item.row.locales[code].status === 'approved' ? 'success' : 'neutral'"
                    size="xs"
                    class="mt-1.5 rounded-full"
                    :aria-pressed="item.row.locales[code].status === 'approved'"
                    :is-disabled="!sessionCanEdit || item.saving || resultBusy"
                    @click="toggleApproved(code, item)"
                  >
                    <template v-if="item.row.locales[code].status === 'approved'" #leading
                      ><Icon :icon="Check" :size="11"
                    /></template>
                    {{ item.row.locales[code].status === 'approved' ? t('Approved') : t('Draft') }}
                  </Button>
                </td>
                <td class="px-4 py-3 text-right">
                  <Button
                    variant="outline"
                    tone="neutral"
                    :is-disabled="!sessionCanEdit || !grid.isDirty(item) || resultBusy"
                    :is-loading="item.saving"
                    @click="grid.save(item)"
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
