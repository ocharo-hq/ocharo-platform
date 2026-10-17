<script setup lang="ts">
import DeskSelect from '@/presentation/shared/DeskSelect.vue';
import { computed, reactive, ref, shallowRef, watch } from 'vue';
import { FileText, Film, ImageIcon, MapPin, RefreshCw } from 'lucide-vue-next';
import { Button } from '@wow-two-beta/fe/presentation/actions';

import { notifyError, requestConfirmation, useErrorToast } from '@/application/shared/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { failureMessage, type AccessControls } from '@/application/workspace/UseAccess';
import { saveOutcome, useSourcingMedia, useSupplierList, type SaveOutcome, type SourcingControls } from '@/application/workspace/UseSourcing';
import { useSourcingUploads } from '@/application/workspace/UseSourcingUploads';
import { mediaKind, type MediaCapture, type MediaFilter, type SourcingMedia } from '@/domain/workspace/Sourcing';

import SourcingLightbox from './SourcingLightbox.vue';
import SourcingUploader from './SourcingUploader.vue';
import WorkspaceCollectionSkeleton from './WorkspaceCollectionSkeleton.vue';

/** One supplier's media or the inbox: upload, view, sort and tidy. */
defineOptions({ name: 'SourcingGallery' });
const props = defineProps<{ access: AccessControls; sourcing: SourcingControls; filter: MediaFilter }>();
const emit = defineEmits<{ openSupplier: [id: string]; openInbox: [] }>();
const { t } = useUiLocale();
const media = useSourcingMedia(props.sourcing, () => props.filter);
const uploads = useSourcingUploads(props.sourcing);
const suppliers = useSupplierList(props.sourcing, () => ({ query: '', stage: '' }));
const supplierId = computed(() => props.filter.kind === 'supplier' ? props.filter.supplierId : null);
const items = computed(() => media.items.value);
const selecting = ref(false);
const selected = reactive(new Set<string>());
const target = ref('');
const moved = ref('');
const viewing = shallowRef<SourcingMedia | null>(null);
const missingViewed = computed(() => !!viewing.value && !items.value.some(item => item.id === viewing.value!.id));
// Keep the opened file available for copying drafts when a background refresh removes it from this gallery.
const viewedItems = computed(() => missingViewed.value && viewing.value ? [...items.value, viewing.value] : items.value);
const viewedIndex = computed(() => viewing.value === null ? -1 : viewedItems.value.findIndex(item => item.id === viewing.value!.id));
watch(() => props.filter, () => { viewing.value = null; stopSelecting(); });
const supplierName = (id: string) => suppliers.suppliers.value?.find(supplier => supplier.id === id)?.name ?? t('Open supplier');
const targets = computed(() => (suppliers.suppliers.value ?? []).filter(supplier => supplier.id !== supplierId.value));
const canEdit = computed(() => props.sourcing.canEdit.value);
const refreshing = ref(false);
const hasRead = ref(false);
watch(() => props.filter, () => { hasRead.value = false; }, { flush: 'sync' });
watch([media.loading, media.error], ([loading, error]) => {
  if (props.sourcing.enabled() && !loading && !error) hasRead.value = true;
}, { immediate: true });
useErrorToast(computed(() => media.error.value ? failureMessage(media.error.value) : null));
useErrorToast(computed(() => suppliers.error.value ? failureMessage(suppliers.error.value) : null));
async function refresh(): Promise<void> {
  if (refreshing.value) return;
  refreshing.value = true;
  try { await media.refetch(); }
  finally { refreshing.value = false; }
}

// Selections only refer to loaded items; drop any that left this gallery.
watch(items, list => {
  const present = new Set(list.map(item => item.id));
  for (const id of [...selected]) if (!present.has(id)) selected.delete(id);
});
function activate(item: SourcingMedia): void {
  if (selecting.value) toggle(item.id);
  else viewing.value = { ...item };
}
function toggle(id: string): void {
  if (selected.has(id)) selected.delete(id); else selected.add(id);
}
function stopSelecting(): void {
  selecting.value = false;
  selected.clear();
  target.value = '';
}
async function assign(destination: string | null): Promise<void> {
  const ids = items.value.filter(item => selected.has(item.id)).map(item => item.id);
  if (!ids.length) return;
  const result = await props.sourcing.assignMedia(ids, destination);
  if (!result.ok) {
    if (result.failure.code !== 'cancelled') notifyError(failureMessage(result.failure));
    return;
  }
  const name = destination ? suppliers.suppliers.value?.find(supplier => supplier.id === destination)?.name ?? '' : '';
  moved.value = destination
    ? t('{count} files moved to {name}.', { count: result.value.updated, name })
    : t('{count} files returned to the inbox.', { count: result.value.updated });
  stopSelecting();
}
async function reloadMedia(): Promise<boolean> {
  await media.refetch();
  return media.error.value === null;
}
function save(item: SourcingMedia, input: { capture: MediaCapture; caption: string }): Promise<SaveOutcome> {
  return props.sourcing.updateMedia(item, { supplierId: item.supplierId, ...input }).then(saveOutcome);
}
async function remove(item: SourcingMedia): Promise<boolean> {
  if (!await requestConfirmation('Delete this file from Desk? Its original bytes are removed and this cannot be undone.', { confirmLabel: 'Delete' })) return false;
  const result = await props.sourcing.deleteMedia(item);
  if (!result.ok && result.failure.code !== 'cancelled') notifyError(failureMessage(result.failure));
  return result.ok;
}
</script>

<template>
  <div class="space-y-4">
    <SourcingUploader v-if="canEdit" :access="access" :uploads="uploads" :supplier-id="supplierId" />
    <p v-if="filter.kind === 'all' && canEdit" class="text-xs text-muted-foreground">{{ t('New files enter the inbox until you assign a supplier.') }}</p>
    <div class="flex flex-wrap items-center justify-between gap-3">
      <p class="text-sm text-muted-foreground">{{ !hasRead ? t("Media") : t("{count} files shown", { count: items.length }) }}</p>
      <div class="flex items-center gap-2">
        <Button size="sm" variant="ghost" :is-loading="refreshing" :is-disabled="access.busy.value || media.loading.value" @click="refresh">
          <RefreshCw :size="16" aria-hidden="true" />{{ t("Refresh media") }}
        </Button>
        <Button v-if="canEdit && items.length && !selecting" size="sm" variant="outline" :is-disabled="access.busy.value" @click="selecting = true; moved = ''">{{ t("Select files") }}</Button>
      </div>
    </div>
    <p v-if="moved" role="status" class="rounded-lg bg-muted p-3 text-sm">{{ moved }}</p>
    <div v-if="selecting" class="workspace-card flex flex-wrap items-center gap-3 p-3" role="toolbar" :aria-label="t('Selected files')">
      <span class="text-sm font-medium">{{ t("{count} selected", { count: selected.size }) }}</span>
      <Button size="sm" variant="ghost" @click="items.forEach(item => selected.add(item.id))">{{ t("Select all shown") }}</Button>
      <DeskSelect v-model="target" class="w-auto max-w-full" :label="t('Destination supplier')" :options="[({ value: '' as const, label: (supplierId ? t('Move to supplier…') : t('Assign to supplier…')) }), ...(targets).map(supplier => ({ value: supplier.id, label: (supplier.name) }))]" />
      <Button v-if="suppliers.hasNextPage.value" size="sm" variant="ghost" :is-loading="suppliers.isFetchingNextPage.value" :is-disabled="access.busy.value" @click="suppliers.fetchNextPage()">{{ t("Load more suppliers") }}</Button>
      <Button size="sm" :is-disabled="access.busy.value || !selected.size || !target" @click="assign(target)">{{ supplierId ? t("Move") : t("Assign") }}</Button>
      <Button v-if="supplierId" size="sm" variant="outline" :is-disabled="access.busy.value || !selected.size" @click="assign(null)">{{ t("Return to inbox") }}</Button>
      <Button size="sm" variant="ghost" @click="stopSelecting">{{ t("Done") }}</Button>
    </div>
    <div class="min-h-72" :aria-busy="media.loading.value || refreshing">
      <WorkspaceCollectionSkeleton v-if="media.loading.value && !items.length" shape="media" :label="t('Loading media…')" />
      <ul v-else-if="items.length" class="sourcing-gallery" :aria-label="t('Media')">
        <li v-for="item in items" :key="item.id" class="sourcing-tile" :data-selected="selecting && selected.has(item.id) ? 'true' : undefined">
          <button
            type="button" class="sourcing-tile-button" :aria-pressed="selecting ? selected.has(item.id) : undefined"
            :aria-label="selecting ? t('Select {name}', { name: item.caption || item.originalName }) : t('Open {name}', { name: item.caption || item.originalName })"
            @click="activate(item)"
          >
            <img v-if="mediaKind(item.contentType) === 'image' && item.hasThumbnail" :src="sourcing.thumbnailUrl(item.id)" alt="" loading="lazy" decoding="async" width="240" height="240" />
            <span v-else class="sourcing-typed">
              <FileText v-if="mediaKind(item.contentType) === 'pdf'" :size="28" aria-hidden="true" />
              <Film v-else-if="mediaKind(item.contentType) === 'video'" :size="28" aria-hidden="true" />
              <ImageIcon v-else :size="28" aria-hidden="true" />
              <span class="line-clamp-2">{{ item.originalName }}</span>
            </span>
          </button>
          <input v-if="selecting" type="checkbox" class="sourcing-tile-check" :checked="selected.has(item.id)" tabindex="-1" aria-hidden="true" @change="toggle(item.id)" />
          <span v-if="item.hasLocation" class="sourcing-tile-badge right-1.5" :title="t('Contains location data')">
            <MapPin :size="12" aria-hidden="true" /><span class="sr-only">{{ t("Contains location data") }}</span>
          </span>
          <p class="sourcing-tile-name" :title="item.caption || item.originalName">{{ item.caption || item.originalName }}</p>
          <button v-if="filter.kind === 'all' && item.supplierId" type="button" class="mt-1 block max-w-full truncate text-xs text-primary underline-offset-4 hover:underline disabled:opacity-50" :disabled="access.busy.value" @click="emit('openSupplier', item.supplierId)">{{ supplierName(item.supplierId) }}</button>
          <button v-else-if="filter.kind === 'all'" type="button" class="mt-1 block text-xs text-primary underline-offset-4 hover:underline disabled:opacity-50" :disabled="access.busy.value" @click="emit('openInbox')">{{ t('Unassigned') }} · {{ t('Inbox') }}</button>
        </li>
      </ul>
      <p v-else-if="hasRead && !media.loading.value" class="empty-state">{{ supplierId ? t("No media for this supplier yet.") : filter.kind === 'all' ? t("No shared materials or files yet.") : t("The inbox is empty.") }}</p>
    </div>
    <div v-if="media.hasNextPage.value" class="flex justify-center">
      <Button variant="outline" :is-loading="media.isFetchingNextPage.value" @click="media.fetchNextPage()">{{ t("Load more") }}</Button>
    </div>
    <SourcingLightbox
      v-if="viewedIndex >= 0" :sourcing="sourcing" :items="viewedItems" :index="viewedIndex" :unavailable="missingViewed" :can-edit="canEdit" :busy="access.busy.value"
      :save="save" :remove="remove" :reload="reloadMedia" @update:index="value => viewing = viewedItems[value] ?? null" @close="viewing = null"
    />
  </div>
</template>
