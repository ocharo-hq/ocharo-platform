<script setup lang="ts">
import DeskSelect from '@/presentation/shared/DeskSelect.vue';
import { computed, ref } from 'vue';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';
import { Badge } from '@wow-two-beta/ui-vue/presentation/display';
import { FileUploadPicker, type FileRejection } from '@wow-two-beta/ui-vue/presentation/forms';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { AccessControls } from '@/application/workspace/UseAccess';
import type { SourcingUploads, UploadStatus } from '@/application/workspace/UseSourcingUploads';
import {
  captureLabels, formatBytes, mediaCaptures, sourcingMaximumBytes, sourcingMediaAccept, type MediaCapture,
} from '@/domain/workspace/Sourcing';

/** Sends one request per file; the list shows each file's outcome, including duplicates. */
defineOptions({ name: 'SourcingUploader' });
const props = defineProps<{ access: AccessControls; uploads: SourcingUploads; supplierId: string | null }>();
const { t } = useUiLocale();
const capture = ref<MediaCapture>('original');
const statusLabels: Record<UploadStatus, string> = { queued: 'Waiting', uploading: 'Uploading…', added: 'Added', duplicate: 'Already in Desk', failed: 'Failed' };
const statusVariants = { queued: 'neutral', uploading: 'info', added: 'success', duplicate: 'warning', failed: 'danger' } as const;
const finished = computed(() => props.uploads.items.filter(item => item.status !== 'queued' && item.status !== 'uploading').length);
function add(accepted: readonly File[], rejected: readonly FileRejection[]): void {
  props.uploads.enqueue(accepted, rejected, { supplierId: props.supplierId, capture: capture.value });
}
</script>

<template>
  <div class="workspace-card space-y-4 p-4 sm:p-5">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div class="min-w-0">
        <h4 class="font-semibold">{{ supplierId ? t("Add files for this supplier") : t("Add files to the inbox") }}</h4>
        <p class="text-xs text-muted-foreground">{{ t("Each file uploads separately. Files already in Desk are recognized and not stored twice.") }}</p>
      </div>
      <label class="sourcing-field">{{ t("File origin") }}
        <DeskSelect v-model="capture" class="w-auto" :label="t('File origin')" :options="[...(mediaCaptures).map(item => ({ value: item, label: (t(captureLabels[item])) }))]" />
      </label>
    </div>
    <FileUploadPicker
      multiple :accept="sourcingMediaAccept" :max-size="sourcingMaximumBytes" :max-files="100"
      :disabled="access.busy.value && !uploads.running.value"
      :label="t('Drop photos, PDFs or videos here, or click to browse')"
      :hint="t('JPEG, PNG or WebP up to 20 MiB · PDF up to 25 MiB · MP4 up to 64 MiB')"
      @files-change="add"
    />
    <div v-if="uploads.items.length" class="space-y-2">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <p role="status" class="text-sm text-muted-foreground">{{ t("{done} of {total} files finished", { done: finished, total: uploads.items.length }) }}</p>
        <Button v-if="!uploads.running.value" size="sm" variant="ghost" @click="uploads.clearFinished()">{{ t("Clear list") }}</Button>
      </div>
      <ul class="max-h-80 divide-y divide-border overflow-y-auto rounded-xl border border-border" :aria-label="t('Uploads')">
        <li v-for="item in uploads.items" :key="item.key" class="space-y-1 px-3 py-2" :data-status="item.status">
          <div class="flex min-w-0 items-center justify-between gap-3">
            <span class="min-w-0 truncate text-sm" :title="item.name">{{ item.name }}</span>
            <span class="flex shrink-0 items-center gap-2">
              <span class="text-xs text-muted-foreground">{{ formatBytes(item.size) }}</span>
              <Badge size="sm" :variant="statusVariants[item.status]">{{ t(statusLabels[item.status]) }}</Badge>
            </span>
          </div>
          <progress v-if="item.status === 'uploading'" class="h-1 w-full" :aria-label="t('Uploading {name}', { name: item.name })" />
          <p v-if="item.nearDuplicates" class="text-xs text-warning-soft-foreground">{{ t("Looks similar to {count} existing images. Review before keeping both.", { count: item.nearDuplicates }) }}</p>
          <p v-if="item.message" class="text-xs" :class="item.status === 'failed' ? 'text-destructive' : 'text-muted-foreground'">{{ t(item.message) }}</p>
          <p v-if="item.detail" class="break-words text-xs text-muted-foreground">{{ item.detail }}</p>
        </li>
      </ul>
    </div>
  </div>
</template>
