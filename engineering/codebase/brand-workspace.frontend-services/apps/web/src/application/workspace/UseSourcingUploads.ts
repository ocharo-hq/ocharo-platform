import { computed, markRaw, onScopeDispose, reactive } from 'vue';

import type { ApiFailure } from '@wow-two-beta/ui-vue/foundation/http';

import { sourcingLimits, uploadRejection, type MediaCapture } from '@/domain/workspace/Sourcing';

import { failureMessage } from './UseAccess';
import type { SourcingControls } from './UseSourcing';

export type UploadStatus = 'queued' | 'uploading' | 'added' | 'duplicate' | 'failed';
export interface UploadItem {
  key: number;
  name: string;
  size: number;
  status: UploadStatus;
  /** An English interface phrase, translated by the view. */
  message: string | null;
  /** A bounded server explanation for a rejected file, shown verbatim. */
  detail: string | null;
  nearDuplicates: number;
  mediaId: string | null;
}
export interface UploadTarget { supplierId: string | null; capture: MediaCapture }
export interface PickerRejection { file: File; reason: string }

/** @internal Server titles of 4xx problems are bounded application messages; others stay hidden. */
function problemDetail(failure: ApiFailure): string | null {
  const title = failure.problem?.title;
  return failure.status >= 400 && failure.status < 500 && failure.status !== 401 && failure.status !== 403 &&
    typeof title === 'string' && title.trim() ? title.trim().slice(0, 300) : null;
}
function uploadFailure(failure: ApiFailure): string {
  if (failure.status === 413) return 'This file is larger than the limit for its type.';
  if (failure.status === 415) return 'This file type is not accepted.';
  if (failure.status === 400) return 'The file was rejected. Check its type and contents.';
  return failureMessage(failure);
}
function pickerReason(reason: string): string {
  if (reason === 'type') return 'Only JPEG, PNG, WebP, PDF and MP4 files are accepted.';
  if (reason === 'size') return 'This file is larger than the limit for its type.';
  return 'Too many files in one batch. Add the rest separately.';
}

/** Uploads one request per file, at most three at a time, then refreshes the affected views once. */
export function useSourcingUploads(sourcing: SourcingControls) {
  const items = reactive<UploadItem[]>([]);
  const files = new Map<number, { file: File; target: UploadTarget }>();
  const controller = new AbortController();
  let sequence = 0;
  let active = 0;
  let completedInBatch = 0;
  const running = computed(() => items.some(item => item.status === 'queued' || item.status === 'uploading'));

  function add(name: string, size: number, status: UploadStatus, message: string | null): UploadItem {
    items.unshift({ key: ++sequence, name, size, status, message, detail: null, nearDuplicates: 0, mediaId: null });
    return items[0]!;
  }
  function enqueue(accepted: readonly File[], rejected: readonly PickerRejection[], target: UploadTarget): void {
    for (const { file, reason } of rejected) add(file.name, file.size, 'failed', pickerReason(reason));
    for (const file of accepted) {
      const problem = uploadRejection(file);
      const item = add(file.name, file.size, problem ? 'failed' : 'queued', problem);
      if (!problem) files.set(item.key, { file: markRaw(file), target: { ...target } });
    }
    pump();
  }
  function pump(): void {
    while (active < sourcingLimits.concurrentUploads && !controller.signal.aborted) {
      const next = [...items].reverse().find(item => item.status === 'queued');
      if (!next) break;
      active += 1;
      void upload(next).finally(() => {
        active -= 1;
        pump();
        if (active === 0 && !items.some(item => item.status === 'queued')) void finishBatch();
      });
    }
  }
  async function upload(item: UploadItem): Promise<void> {
    const entry = files.get(item.key);
    files.delete(item.key);
    if (!entry) { item.status = 'failed'; return; }
    item.status = 'uploading';
    const { file, target } = entry;
    const result = await sourcing.uploadMedia(file, { supplierId: target.supplierId, capture: target.capture, caption: '' }, controller.signal);
    if (!result.ok) {
      item.status = 'failed';
      item.message = uploadFailure(result.failure);
      item.detail = problemDetail(result.failure);
      return;
    }
    completedInBatch += 1;
    const { media, duplicate, nearDuplicateIds } = result.value;
    item.mediaId = media.id;
    item.nearDuplicates = nearDuplicateIds.length;
    if (duplicate) {
      item.status = 'duplicate';
      item.message = media.supplierId === target.supplierId
        ? 'Already here. Nothing new was stored.'
        : 'Already in Desk under another supplier or the inbox. Nothing new was stored.';
    } else {
      item.status = 'added';
      item.message = null;
    }
  }
  async function finishBatch(): Promise<void> {
    if (!completedInBatch || controller.signal.aborted) return;
    completedInBatch = 0;
    await sourcing.refreshMediaViews();
  }
  function clearFinished(): void {
    for (let index = items.length - 1; index >= 0; index -= 1) {
      if (items[index]!.status !== 'queued' && items[index]!.status !== 'uploading') items.splice(index, 1);
    }
  }
  onScopeDispose(() => { controller.abort(); files.clear(); });
  return { items, running, enqueue, clearFinished };
}
export type SourcingUploads = ReturnType<typeof useSourcingUploads>;
