<script setup lang="ts">
import DeskSelect from '@/presentation/shared/DeskSelect.vue';
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, shallowRef, watch } from 'vue';
import { ChevronLeft, ChevronRight, FileText, MapPin, X } from 'lucide-vue-next';
import { Button } from '@wow-two-beta/fe/presentation/actions';

import { requestConfirmation } from '@/application/shared/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { SaveOutcome, SourcingControls } from '@/application/workspace/UseSourcing';
import {
  captureLabels, formatBytes, mediaCaptures, mediaKind, sourcingLimits,
  type MediaCapture, type SourcingMedia,
} from '@/domain/workspace/Sourcing';

/** An accessible viewer over one gallery in grid order: Escape closes, arrow keys move, focus returns to the opener. */
defineOptions({ name: 'SourcingLightbox' });
const props = defineProps<{
  sourcing: SourcingControls;
  items: readonly SourcingMedia[];
  index: number;
  canEdit: boolean;
  unavailable: boolean;
  busy: boolean;
  save: (media: SourcingMedia, input: { capture: MediaCapture; caption: string }) => Promise<SaveOutcome>;
  remove: (media: SourcingMedia) => Promise<boolean>;
  reload: () => Promise<boolean>;
}>();
const emit = defineEmits<{ close: []; 'update:index': [index: number] }>();
const { t, uiLocale } = useUiLocale();
const root = ref<HTMLElement | null>(null);
const titleId = `sourcing-lightbox-${Math.random().toString(36).slice(2)}`;
const item = computed(() => props.items[Math.min(Math.max(props.index, 0), props.items.length - 1)]);
const kind = computed(() => item.value ? mediaKind(item.value.contentType) : 'other');
const editing = ref(false);
const editBaseline = shallowRef<SourcingMedia | null>(null);
const saving = ref(false);
const reloading = ref(false);
const conflict = ref(false);
const draft = reactive({ caption: '', capture: 'original' as MediaCapture });
let opener: HTMLElement | null = null;
let previousOverflow = '';

watch(() => props.items.length, length => {
  if (!length) emit('close');
  else if (props.index >= length) emit('update:index', length - 1);
});
watch(item, (next, previous) => { if (next?.id !== previous?.id) { editing.value = false; conflict.value = false; } });
const dirty = computed(() => editing.value && !!editBaseline.value && (draft.caption !== editBaseline.value.caption || draft.capture !== editBaseline.value.capture));
watch(dirty, value => props.sourcing.setFormDirty('media-edit', value));

onMounted(() => {
  opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
  previousOverflow = document.documentElement.style.overflow;
  document.documentElement.style.overflow = 'hidden';
  void nextTick(() => root.value?.focus());
});
onBeforeUnmount(() => {
  props.sourcing.setFormDirty('media-edit', false);
  document.documentElement.style.overflow = previousOverflow;
  if (opener?.isConnected) opener.focus();
});

function go(step: number): void {
  if (editing.value) return;
  const next = props.index + step;
  if (next >= 0 && next < props.items.length) emit('update:index', next);
}
function focusable(): HTMLElement[] {
  return Array.from(root.value?.querySelectorAll<HTMLElement>('a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), video[controls]') ?? []);
}
function keydown(event: KeyboardEvent): void {
  if (event.defaultPrevented) return;
  if (event.key === 'Escape') {
    event.preventDefault();
    if (editing.value) void cancelEdit(); else void close();
    return;
  }
  const target = event.target as HTMLElement | null;
  const typing = !!target && (/^(INPUT|TEXTAREA|SELECT|VIDEO)$/.test(target.tagName) || target.closest('[role=combobox],[role=listbox]') !== null);
  if (event.key === 'ArrowRight' && !typing) { event.preventDefault(); go(1); return; }
  if (event.key === 'ArrowLeft' && !typing) { event.preventDefault(); go(-1); return; }
  if (event.key !== 'Tab') return;
  const elements = focusable();
  if (!elements.length) { event.preventDefault(); return; }
  const first = elements[0]!;
  const last = elements[elements.length - 1]!;
  if (event.shiftKey && (document.activeElement === first || document.activeElement === root.value)) { event.preventDefault(); last.focus(); }
  else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
}
async function discardEdit(): Promise<boolean> {
  if (saving.value || reloading.value || props.busy) return false;
  if (dirty.value && !await requestConfirmation(t('Discard unsaved changes?'), { confirmLabel: t('Discard changes') })) return false;
  if (saving.value || reloading.value || props.busy) return false;
  editing.value = false;
  conflict.value = false;
  return true;
}
async function cancelEdit(): Promise<void> {
  if (await discardEdit()) root.value?.focus();
}
async function close(): Promise<void> {
  if (await discardEdit()) emit('close');
}
async function reloadLatest(): Promise<void> {
  if (saving.value || reloading.value || props.busy) return;
  reloading.value = true;
  try {
    // Keep draft edits unless the latest server revision was actually fetched.
    if (await props.reload()) {
      await nextTick();
      if (!props.unavailable) startEdit();
    }
  } finally { reloading.value = false; }
}
function formatDate(value: string | null): string {
  if (!value) return '—';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString(uiLocale.value);
}
function startEdit(): void {
  if (!item.value) return;
  editBaseline.value = item.value;
  draft.caption = item.value.caption;
  draft.capture = item.value.capture;
  conflict.value = false;
  editing.value = true;
}
async function submit(): Promise<void> {
  if (!editBaseline.value || saving.value || reloading.value || props.busy || draft.caption.length > sourcingLimits.caption) return;
  saving.value = true;
  try {
    const outcome = await props.save(editBaseline.value, { caption: draft.caption.trim(), capture: draft.capture });
    conflict.value = outcome === 'conflict';
    if (outcome === 'saved') { editing.value = false; root.value?.focus(); }
  } finally { saving.value = false; }
}
async function removeCurrent(): Promise<void> {
  if (item.value && await props.remove(item.value)) emit('close');
}
</script>

<template>
  <div ref="root" class="sourcing-lightbox" role="dialog" aria-modal="true" :aria-labelledby="titleId" tabindex="-1" @keydown="keydown">
    <div class="sourcing-lightbox-bar">
      <p :id="titleId" class="min-w-0 truncate font-medium">{{ item ? item.caption || item.originalName : '' }}</p>
      <span class="ml-auto shrink-0 text-xs opacity-80">{{ t("{current} of {total}", { current: index + 1, total: items.length }) }}</span>
      <button type="button" class="sourcing-lightbox-close" :aria-label="t('Close')" :disabled="busy || saving || reloading" @click="close"><X :size="20" aria-hidden="true" /></button>
    </div>
    <div class="sourcing-lightbox-stage">
      <template v-if="item">
        <img v-if="kind === 'image'" :key="item.id" :src="sourcing.contentUrl(item.id)" :alt="item.caption || item.originalName" decoding="async" />
        <video v-else-if="kind === 'video'" :key="`video-${item.id}`" controls preload="metadata" :src="sourcing.contentUrl(item.id)" :aria-label="item.originalName" />
        <div v-else class="grid justify-items-center gap-3 text-center">
          <FileText :size="56" aria-hidden="true" />
          <p class="break-all text-sm">{{ item.originalName }}</p>
          <a :href="sourcing.contentUrl(item.id)" target="_blank" rel="noopener noreferrer" class="rounded-lg bg-white/15 px-4 py-2 text-sm font-medium">{{ kind === 'pdf' ? t("Open PDF") : t("Open original") }}</a>
        </div>
      </template>
      <button type="button" class="sourcing-lightbox-nav left-2" :disabled="index <= 0 || editing" :aria-label="t('Previous')" @click="go(-1)"><ChevronLeft :size="22" aria-hidden="true" /></button>
      <button type="button" class="sourcing-lightbox-nav right-2" :disabled="index >= items.length - 1 || editing" :aria-label="t('Next')" @click="go(1)"><ChevronRight :size="22" aria-hidden="true" /></button>
    </div>
    <aside v-if="item" class="sourcing-lightbox-info space-y-4" :aria-label="t('File details')">
      <div v-if="unavailable" role="alert" class="sourcing-alert space-y-2"><p>{{ t("This item is no longer in this view. Your edits are still here; copy them before closing.") }}</p><Button v-if="editing" size="sm" variant="outline" :is-loading="reloading" :is-disabled="busy || saving" @click="reloadLatest">{{ t("Reload latest") }}</Button></div>
      <p v-if="item.hasLocation" class="sourcing-alert flex gap-2"><MapPin :size="16" class="mt-0.5 shrink-0" aria-hidden="true" />{{ t("This original contains location (GPS) data. Remove it before sharing the file outside Desk.") }}</p>
      <form v-if="editing" class="space-y-3" novalidate @submit.prevent="submit">
        <label class="sourcing-field">{{ t("Caption") }}
          <textarea v-model="draft.caption" class="sourcing-input" rows="3" :maxlength="sourcingLimits.caption" :disabled="saving || reloading" />
        </label>
        <label class="sourcing-field">{{ t("File origin") }}
          <DeskSelect v-model="draft.capture" :disabled="saving || reloading" :label="t('File origin')" :options="[...(mediaCaptures).map(capture => ({ value: capture, label: (t(captureLabels[capture])) }))]" />
        </label>
        <div v-if="conflict" role="alert" class="sourcing-alert space-y-2"><p>{{ t("This record changed. Your edits are still here. Reload the latest version before trying again.") }}</p><Button size="sm" variant="outline" :is-loading="reloading" :is-disabled="busy || saving" @click="reloadLatest">{{ t("Discard my edits and reload") }}</Button></div>
        <div class="flex flex-wrap gap-2">
          <Button type="submit" size="sm" :is-loading="saving" :is-disabled="busy || reloading">{{ t("Save details") }}</Button>
          <Button size="sm" variant="ghost" :is-disabled="busy || saving || reloading" @click="cancelEdit">{{ t("Cancel") }}</Button>
        </div>
      </form>
      <dl v-else class="grid gap-3 text-sm">
        <div><dt class="text-xs text-muted-foreground">{{ t("Caption") }}</dt><dd class="whitespace-pre-wrap break-words">{{ item.caption || '—' }}</dd></div>
        <div><dt class="text-xs text-muted-foreground">{{ t("File name") }}</dt><dd class="break-all">{{ item.originalName }}</dd></div>
        <div><dt class="text-xs text-muted-foreground">{{ t("File origin") }}</dt><dd>{{ t(captureLabels[item.capture]) }}</dd></div>
        <div><dt class="text-xs text-muted-foreground">{{ t("Received") }}</dt><dd>{{ formatDate(item.receivedAt) }}</dd></div>
        <div v-if="item.takenAt"><dt class="text-xs text-muted-foreground">{{ t("Taken") }}</dt><dd>{{ formatDate(item.takenAt) }}</dd></div>
        <div v-if="item.sourceLabel"><dt class="text-xs text-muted-foreground">{{ t("Source") }}</dt><dd class="break-words">{{ item.sourceLabel }}</dd></div>
        <div><dt class="text-xs text-muted-foreground">{{ t("File") }}</dt><dd>{{ [formatBytes(item.byteSize), item.width && item.height ? `${item.width} × ${item.height}` : '', item.contentType].filter(Boolean).join(' · ') }}</dd></div>
      </dl>
      <div class="flex flex-wrap items-center gap-3">
        <a :href="sourcing.contentUrl(item.id)" target="_blank" rel="noopener noreferrer" class="text-sm font-medium text-primary underline underline-offset-4">{{ t("Open original") }}</a>
        <template v-if="canEdit && !editing">
          <Button size="sm" variant="outline" :is-disabled="busy" @click="startEdit">{{ t("Edit details") }}</Button>
          <Button size="sm" variant="ghost" tone="danger" :is-disabled="busy" @click="removeCurrent">{{ t("Delete") }}</Button>
        </template>
      </div>
    </aside>
  </div>
</template>
