<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue';
import { ChevronLeft, ChevronRight, FileText, MapPin, X } from 'lucide-vue-next';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';

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
  busy: boolean;
  save: (media: SourcingMedia, input: { capture: MediaCapture; caption: string }) => Promise<SaveOutcome>;
  remove: (media: SourcingMedia) => Promise<boolean>;
}>();
const emit = defineEmits<{ close: []; 'update:index': [index: number] }>();
const { t, uiLocale } = useUiLocale();
const root = ref<HTMLElement | null>(null);
const titleId = `sourcing-lightbox-${Math.random().toString(36).slice(2)}`;
const item = computed(() => props.items[Math.min(Math.max(props.index, 0), props.items.length - 1)]);
const kind = computed(() => item.value ? mediaKind(item.value.contentType) : 'other');
const editing = ref(false);
const saving = ref(false);
const conflict = ref(false);
const draft = reactive({ caption: '', capture: 'original' as MediaCapture });
let opener: HTMLElement | null = null;
let previousOverflow = '';

watch(() => props.items.length, length => {
  if (!length) emit('close');
  else if (props.index >= length) emit('update:index', length - 1);
});
watch(item, (next, previous) => { if (next?.id !== previous?.id) { editing.value = false; conflict.value = false; } });
const dirty = computed(() => editing.value && !!item.value && (draft.caption !== item.value.caption || draft.capture !== item.value.capture));
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
  if (event.key === 'Escape') {
    event.preventDefault();
    if (editing.value) { editing.value = false; root.value?.focus(); } else emit('close');
    return;
  }
  const target = event.target as HTMLElement | null;
  const typing = !!target && /^(INPUT|TEXTAREA|SELECT|VIDEO)$/.test(target.tagName);
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
function formatDate(value: string | null): string {
  if (!value) return '—';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString(uiLocale.value);
}
function startEdit(): void {
  if (!item.value) return;
  draft.caption = item.value.caption;
  draft.capture = item.value.capture;
  conflict.value = false;
  editing.value = true;
}
async function submit(): Promise<void> {
  if (!item.value || saving.value || draft.caption.length > sourcingLimits.caption) return;
  saving.value = true;
  try {
    const outcome = await props.save(item.value, { caption: draft.caption.trim(), capture: draft.capture });
    conflict.value = outcome === 'conflict';
    if (outcome === 'saved') { editing.value = false; root.value?.focus(); }
  } finally { saving.value = false; }
}
async function removeCurrent(): Promise<void> {
  if (item.value && await props.remove(item.value)) root.value?.focus();
}
</script>

<template>
  <div ref="root" class="sourcing-lightbox" role="dialog" aria-modal="true" :aria-labelledby="titleId" tabindex="-1" @keydown="keydown">
    <div class="sourcing-lightbox-bar">
      <p :id="titleId" class="min-w-0 truncate font-medium">{{ item ? item.caption || item.originalName : '' }}</p>
      <span class="ml-auto shrink-0 text-xs opacity-80">{{ t("{current} of {total}", { current: index + 1, total: items.length }) }}</span>
      <button type="button" class="sourcing-lightbox-close" :aria-label="t('Close')" @click="emit('close')"><X :size="20" aria-hidden="true" /></button>
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
      <p v-if="item.hasLocation" class="sourcing-alert flex gap-2"><MapPin :size="16" class="mt-0.5 shrink-0" aria-hidden="true" />{{ t("This original contains location (GPS) data. Remove it before sharing the file outside Desk.") }}</p>
      <form v-if="editing" class="space-y-3" novalidate @submit.prevent="submit">
        <label class="sourcing-field">{{ t("Caption") }}
          <textarea v-model="draft.caption" class="sourcing-input" rows="3" :maxlength="sourcingLimits.caption" :disabled="saving" />
        </label>
        <label class="sourcing-field">{{ t("File origin") }}
          <select v-model="draft.capture" class="sourcing-input" :disabled="saving">
            <option v-for="capture in mediaCaptures" :key="capture" :value="capture">{{ t(captureLabels[capture]) }}</option>
          </select>
        </label>
        <div v-if="conflict" role="alert" class="sourcing-alert">{{ t("This record changed. Your edits are still here. Reload the latest version before trying again.") }}</div>
        <div class="flex flex-wrap gap-2">
          <Button type="submit" size="sm" :is-loading="saving" :is-disabled="busy">{{ t("Save details") }}</Button>
          <Button size="sm" variant="ghost" :is-disabled="saving" @click="editing = false">{{ t("Cancel") }}</Button>
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
