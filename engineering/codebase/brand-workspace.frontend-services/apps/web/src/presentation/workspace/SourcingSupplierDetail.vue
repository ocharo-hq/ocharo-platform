<script setup lang="ts">
import { computed, ref } from 'vue';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';
import { Badge } from '@wow-two-beta/ui-vue/presentation/display';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { AccessControls } from '@/application/workspace/UseAccess';
import { saveOutcome, useSupplier, type SaveOutcome, type SourcingControls } from '@/application/workspace/UseSourcing';
import {
  isSupplierStage, safeHttpUrl, stageLabels, supplierStages,
  type MediaFilter, type Supplier, type SupplierInput,
} from '@/domain/workspace/Sourcing';

import SourcingGallery from './SourcingGallery.vue';
import SourcingPeople from './SourcingPeople.vue';
import SourcingRoutes from './SourcingRoutes.vue';
import SourcingSupplierForm from './SourcingSupplierForm.vue';

/** One supplier: its own fields, contact routes, people and shared media. */
defineOptions({ name: 'SourcingSupplierDetail' });
const props = defineProps<{ access: AccessControls; sourcing: SourcingControls; supplierId: string }>();
const { t } = useUiLocale();
const { supplier, loading, error } = useSupplier(props.sourcing, () => props.supplierId);
const editing = ref(false);
const formVersion = ref(0);
const stageConflict = ref(false);
const stageSaving = ref(false);
const filter = computed<MediaFilter>(() => ({ kind: 'supplier', supplierId: props.supplierId }));
const place = computed(() => [supplier.value?.city, supplier.value?.country].filter(Boolean).join(', '));
const website = computed(() => safeHttpUrl(supplier.value?.website));

const fields = (value: Supplier): SupplierInput =>
  ({ name: value.name, stage: value.stage, country: value.country, city: value.city, website: value.website, notes: value.notes });
async function saveDetails(input: SupplierInput): Promise<SaveOutcome> {
  const current = supplier.value;
  if (!current) return 'failed';
  const outcome = saveOutcome(await props.sourcing.updateSupplier(current.id, current.revision, input));
  if (outcome === 'saved') editing.value = false;
  return outcome;
}
async function changeStage(event: Event): Promise<void> {
  const select = event.target as HTMLSelectElement;
  const current = supplier.value;
  if (!current || !isSupplierStage(select.value) || select.value === current.stage) return;
  stageSaving.value = true;
  try {
    const outcome = saveOutcome(await props.sourcing.updateSupplier(current.id, current.revision, { ...fields(current), stage: select.value }));
    stageConflict.value = outcome === 'conflict';
    if (outcome !== 'saved') select.value = current.stage;
  } finally { stageSaving.value = false; }
}
async function reload(): Promise<void> {
  await props.sourcing.refreshSupplier(props.supplierId);
  stageConflict.value = false;
  formVersion.value += 1;
}
</script>

<template>
  <p v-if="loading && !supplier" role="status" class="query-label">{{ t("Loading supplier…") }}</p>
  <section v-else-if="!supplier" role="status" class="query-label">
    <h2 class="text-xl font-semibold">{{ t("Supplier unavailable") }}</h2>
    <p class="mt-3 text-sm text-muted-foreground">{{ error?.status === 404 ? t("This record is unavailable in this organization.") : t("Content unavailable. Try refreshing.") }}</p>
  </section>
  <div v-else class="space-y-5">
    <article class="workspace-card space-y-4 p-5 sm:p-6" aria-labelledby="sourcing-supplier-title">
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div class="min-w-0">
          <h2 id="sourcing-supplier-title" class="break-words text-2xl font-semibold">{{ supplier.name }}</h2>
          <p v-if="place" class="mt-1 text-sm text-muted-foreground">{{ place }}</p>
        </div>
        <div class="flex flex-wrap items-center gap-3">
          <label v-if="sourcing.canEdit.value && !editing" class="flex items-center gap-2 text-sm text-muted-foreground">{{ t("Stage") }}
            <select class="sourcing-input w-auto" :value="supplier.stage" :disabled="access.busy.value || stageSaving" @change="changeStage">
              <option v-for="stage in supplierStages" :key="stage" :value="stage">{{ t(stageLabels[stage]) }}</option>
            </select>
          </label>
          <Badge v-else variant="brand">{{ t(stageLabels[supplier.stage]) }}</Badge>
          <Button v-if="sourcing.canEdit.value && !editing" size="sm" variant="outline" :is-disabled="access.busy.value" @click="editing = true">{{ t("Edit details") }}</Button>
        </div>
      </div>
      <div v-if="stageConflict" role="alert" class="sourcing-alert space-y-2">
        <p>{{ t("This record changed. Reload the latest version before trying again.") }}</p>
        <Button size="sm" variant="outline" @click="reload">{{ t("Reload latest") }}</Button>
      </div>
      <SourcingSupplierForm
        v-if="editing" :key="formVersion" :sourcing="sourcing" :dirty-key="`supplier-${supplier.id}`" :initial="supplier"
        :disabled="access.busy.value" :submit-label="t('Save supplier')" :save="saveDetails" @cancel="editing = false" @reload="reload"
      />
      <dl v-else class="grid gap-4 text-sm sm:grid-cols-2">
        <div class="min-w-0">
          <dt class="text-xs text-muted-foreground">{{ t("Website") }}</dt>
          <dd class="mt-1 break-all">
            <a v-if="website" :href="website" target="_blank" rel="noopener noreferrer" class="text-primary underline-offset-4 hover:underline">{{ supplier.website }}</a>
            <span v-else>{{ supplier.website || '—' }}</span>
          </dd>
        </div>
        <div>
          <dt class="text-xs text-muted-foreground">{{ t("Recorded") }}</dt>
          <dd class="mt-1">{{ t("People: {people} · Contact routes: {routes}", { people: supplier.people.length, routes: supplier.routes.length }) }}</dd>
        </div>
        <div class="min-w-0 sm:col-span-2">
          <dt class="text-xs text-muted-foreground">{{ t("Notes") }}</dt>
          <dd class="mt-1 whitespace-pre-wrap break-words leading-6">{{ supplier.notes || '—' }}</dd>
        </div>
      </dl>
    </article>
    <div class="grid items-start gap-5 xl:grid-cols-2">
      <SourcingRoutes :access="access" :sourcing="sourcing" :supplier="supplier" />
      <SourcingPeople :access="access" :sourcing="sourcing" :supplier="supplier" />
    </div>
    <section class="space-y-4" aria-labelledby="sourcing-media-title">
      <h3 id="sourcing-media-title" class="text-lg font-semibold">{{ t("Shared media") }}</h3>
      <SourcingGallery :access="access" :sourcing="sourcing" :filter="filter" />
    </section>
  </div>
</template>
