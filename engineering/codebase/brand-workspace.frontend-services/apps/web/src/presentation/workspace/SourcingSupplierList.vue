<script setup lang="ts">
import { computed, onScopeDispose, ref, watch } from 'vue';
import { Inbox, Plus, RefreshCw } from 'lucide-vue-next';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';
import { Badge } from '@wow-two-beta/ui-vue/presentation/display';

import { notifyError, useErrorToast } from '@/application/shared/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { failureMessage, type AccessControls } from '@/application/workspace/UseAccess';
import { useSupplierList, type SaveOutcome, type SourcingControls } from '@/application/workspace/UseSourcing';
import {
  channelLabels, stageLabels, supplierInitial, supplierStages,
  type SourcingLocation, type SupplierInput, type SupplierStage,
} from '@/domain/workspace/Sourcing';

import SourcingSupplierForm from './SourcingSupplierForm.vue';
import WorkspaceCollectionSkeleton from './WorkspaceCollectionSkeleton.vue';

/** Lists suppliers with search and stage filters; the inbox sits above them. */
defineOptions({ name: 'SourcingSupplierList' });
const props = defineProps<{ access: AccessControls; sourcing: SourcingControls; current: SourcingLocation; unassigned: number | null }>();
const emit = defineEmits<{ open: [location: SourcingLocation] }>();
const { t } = useUiLocale();
const search = ref('');
const query = ref('');
const stage = ref<SupplierStage | ''>('');
const adding = ref(false);
let timer: ReturnType<typeof setTimeout> | undefined;
watch(search, value => {
  clearTimeout(timer);
  timer = setTimeout(() => { query.value = value.trim(); }, 250);
});
onScopeDispose(() => clearTimeout(timer));
const list = useSupplierList(props.sourcing, () => ({ query: query.value, stage: stage.value }));

const refreshing = ref(false);
useErrorToast(computed(() => list.error.value ? failureMessage(list.error.value) : null));
async function refresh(): Promise<void> {
  if (refreshing.value) return;
  refreshing.value = true;
  try { await list.refetch(); }
  finally { refreshing.value = false; }
}

function place(country: string | null, city: string | null): string {
  return [city, country].filter(Boolean).join(', ');
}
async function create(input: SupplierInput): Promise<SaveOutcome> {
  const result = await props.sourcing.createSupplier(input);
  if (!result.ok) {
    if (result.failure.code !== 'cancelled') notifyError(failureMessage(result.failure));
    return 'failed';
  }
  props.sourcing.setFormDirty('supplier-create', false);
  adding.value = false;
  emit('open', { view: 'supplier', supplierId: result.value.id });
  return 'saved';
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex items-center justify-between gap-3">
      <h2 class="text-lg font-semibold">{{ t("Suppliers") }}</h2>
      <div class="flex flex-wrap items-center justify-end gap-2">
        <Button size="sm" variant="ghost" :is-loading="refreshing" :is-disabled="access.busy.value || list.loading.value" @click="refresh">
          <RefreshCw :size="16" aria-hidden="true" />{{ t("Refresh suppliers") }}
        </Button>
        <Button v-if="sourcing.canEdit.value && !adding" size="sm" variant="outline" :is-disabled="access.busy.value" @click="adding = true">
          <Plus :size="16" aria-hidden="true" />{{ t("Add supplier") }}
        </Button>
      </div>
    </div>
    <section v-if="adding" class="workspace-card p-4" :aria-label="t('New supplier')">
      <h3 class="mb-3 font-semibold">{{ t("New supplier") }}</h3>
      <SourcingSupplierForm :sourcing="sourcing" dirty-key="supplier-create" :disabled="access.busy.value" :submit-label="t('Create supplier')" :save="create" @cancel="adding = false" />
    </section>
    <div class="grid gap-3">
      <label class="sourcing-field">{{ t("Search suppliers") }}
        <input v-model="search" type="search" class="sourcing-input" :placeholder="t('Type to search')" autocomplete="off" />
      </label>
      <label class="sourcing-field">{{ t("Stage") }}
        <select v-model="stage" class="sourcing-input">
          <option value="">{{ t("All stages") }}</option>
          <option v-for="item in supplierStages" :key="item" :value="item">{{ t(stageLabels[item]) }}</option>
        </select>
      </label>
    </div>
    <nav class="space-y-1" :aria-label="t('Suppliers')">
      <button
        type="button" class="sourcing-row" :aria-current="current.view === 'inbox' ? 'page' : undefined" :disabled="access.busy.value"
        @click="emit('open', { view: 'inbox', supplierId: null })"
      >
        <span class="sourcing-avatar" aria-hidden="true"><Inbox :size="20" /></span>
        <span class="min-w-0">
          <b class="block text-sm">{{ t("Inbox") }}</b>
          <span class="block text-xs text-muted-foreground">{{ unassigned === null ? t("Unsorted media") : t("{count} unsorted files", { count: unassigned }) }}</span>
        </span>
      </button>
      <div class="min-h-72" :aria-busy="list.loading.value || refreshing">
        <WorkspaceCollectionSkeleton v-if="list.loading.value && !list.suppliers.value" shape="suppliers" :label="t('Loading suppliers…')" />
        <template v-else>
          <button
            v-for="supplier in list.suppliers.value" :key="supplier.id" type="button" class="sourcing-row"
            :aria-current="current.view === 'supplier' && current.supplierId === supplier.id ? 'page' : undefined" :disabled="access.busy.value"
            @click="emit('open', { view: 'supplier', supplierId: supplier.id })"
          >
            <span class="sourcing-avatar" aria-hidden="true">
              <img v-if="supplier.coverMediaId" :src="sourcing.thumbnailUrl(supplier.coverMediaId)" alt="" loading="lazy" />
              <template v-else>{{ supplierInitial(supplier.name) }}</template>
            </span>
            <span class="min-w-0 space-y-1">
              <span class="flex min-w-0 items-center justify-between gap-2">
                <b class="truncate text-sm">{{ supplier.name }}</b>
                <Badge size="sm" :variant="supplier.stage === 'qualified' ? 'success' : supplier.stage === 'dropped' ? 'neutral' : 'brand'">{{ t(stageLabels[supplier.stage]) }}</Badge>
              </span>
              <span class="block truncate text-xs text-muted-foreground">
                {{ [place(supplier.country, supplier.city), supplier.primaryRoute ? t(channelLabels[supplier.primaryRoute.channel]) : '', t("{count} media", { count: supplier.mediaCount })].filter(Boolean).join(' · ') }}
              </span>
            </span>
          </button>
          <p v-if="list.suppliers.value && !list.suppliers.value.length && !list.loading.value" class="empty-state">
            {{ query || stage ? t("No suppliers match this search.") : t("No suppliers yet.") }}
          </p>
          <p v-if="list.truncated.value" class="px-3 text-xs text-muted-foreground">{{ t("Showing the first 200 suppliers. Search to narrow the list.") }}</p>
        </template>
      </div>
    </nav>
  </div>
</template>
