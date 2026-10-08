<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { FileText, Film, ImageIcon, MapPin } from 'lucide-vue-next';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';

import { notifyError, requestConfirmation } from '@/application/shared/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { failureMessage, type AccessControls } from '@/application/workspace/UseAccess';
import { saveOutcome, useSourcingMedia, useSupplierList, type SaveOutcome, type SourcingControls } from '@/application/workspace/UseSourcing';
import { useSourcingUploads } from '@/application/workspace/UseSourcingUploads';
import { mediaKind, type MediaCapture, type MediaFilter, type SourcingMedia } from '@/domain/workspace/Sourcing';

import SourcingLightbox from './SourcingLightbox.vue';
import SourcingUploader from './SourcingUploader.vue';

/** One supplier's media or the inbox: upload, view, sort and tidy. */
defineOptions({ name: 'SourcingGallery' });
const props = defineProps<{ access: AccessControls; sourcing: SourcingControls; filter: MediaFilter }>();
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
const viewing = ref<number | null>(null);
const targets = computed(() => (suppliers.suppliers.value ?? []).filter(supplier => supplier.id !== supplierId.value));
const canEdit = computed(() => props.sourcing.canEdit.value);

// Selections only refer to loaded items; drop any that left this gallery.
watch(items, list => {
  const present = new Set(list.map(item => item.id));
  for (const id of [...selected]) if (!present.has(id)) selected.delete(id);
});
function activate(index: number, item: SourcingMedia): void {
  if (selecting.value) toggle(item.id);
  else viewing.value = index;
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
    <div class="flex flex-wrap items-center justify-between gap-3">
      <p class="text-sm text-muted-foreground">{{ t("{count} files shown", { count: items.length }) }}</p>
      <Button v-if="canEdit && items.length && !selecting" size="sm" variant="outline" :is-disabled="access.busy.value" @click="selecting = true; moved = ''">{{ t("Select files") }}</Button>
    </div>
    <p v-if="moved" role="status" class="rounded-lg bg-muted p-3 text-sm">{{ moved }}</p>
    <div v-if="selecting" class="workspace-card flex flex-wrap items-center gap-3 p-3" role="toolbar" :aria-label="t('Selected files')">
      <span class="text-sm font-medium">{{ t("{count} selected", { count: selected.size }) }}</span>
      <Button size="sm" variant="ghost" @click="items.forEach(item => selected.add(item.id))">{{ t("Select all shown") }}</Button>
      <select v-model="target" class="sourcing-input w-auto max-w-full" :aria-label="t('Destination supplier')">
        <option value="">{{ supplierId ? t("Move to supplier…") : t("Assign to supplier…") }}</option>
        <option v-for="supplier in targets" :key="supplier.id" :value="supplier.id">{{ supplier.name }}</option>
      </select>
      <Button size="sm" :is-disabled="access.busy.value || !selected.size || !target" @click="assign(target)">{{ supplierId ? t("Move") : t("Assign") }}</Button>
      <Button v-if="supplierId" size="sm" variant="outline" :is-disabled="access.busy.value || !selected.size" @click="assign(null)">{{ t("Return to inbox") }}</Button>
      <Button size="sm" variant="ghost" @click="stopSelecting">{{ t("Done") }}</Button>
    </div>
    <p v-if="media.loading.value" role="status" class="py-10 text-center text-sm text-muted-foreground">{{ t("Loading media…") }}</p>
    <p v-else-if="media.error.value && !items.length" role="status" class="py-10 text-center text-sm text-muted-foreground">{{ t("Media unavailable. Try refreshing.") }}</p>
    <ul v-else-if="items.length" class="sourcing-gallery" :aria-label="t('Media')">
      <li v-for="(item, index) in items" :key="item.id" class="sourcing-tile" :data-selected="selecting && selected.has(item.id) ? 'true' : undefined">
        <button
          type="button" class="sourcing-tile-button" :aria-pressed="selecting ? selected.has(item.id) : undefined"
          :aria-label="selecting ? t('Select {name}', { name: item.caption || item.originalName }) : t('Open {name}', { name: item.caption || item.originalName })"
          @click="activate(index, item)"
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
      </li>
    </ul>
    <p v-else class="empty-state">{{ supplierId ? t("No media for this supplier yet.") : t("The inbox is empty.") }}</p>
    <div v-if="media.hasNextPage.value" class="flex justify-center">
      <Button variant="outline" :is-loading="media.isFetchingNextPage.value" @click="media.fetchNextPage()">{{ t("Load more") }}</Button>
    </div>
    <SourcingLightbox
      v-if="viewing !== null && items.length" :sourcing="sourcing" :items="items" :index="viewing" :can-edit="canEdit" :busy="access.busy.value"
      :save="save" :remove="remove" @update:index="value => viewing = value" @close="viewing = null"
    />
  </div>
</template>
