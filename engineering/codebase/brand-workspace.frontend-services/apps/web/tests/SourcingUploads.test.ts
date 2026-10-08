import { effectScope } from 'vue';
import { flushPromises } from '@vue/test-utils';
import { describe, expect, it, vi } from 'vitest';
import { ApiFailureFactory } from '@wow-two-beta/ui-vue/foundation/http';
import { ResultExtensions } from '@wow-two-beta/ui-vue/foundation/results';

import type { SourcingControls } from '@/application/workspace/UseSourcing';
import { useSourcingUploads } from '@/application/workspace/UseSourcingUploads';
import type { SourcingMedia } from '@/domain/workspace/Sourcing';

function media(id: string, supplierId: string | null): SourcingMedia {
  return {
    id, supplierId, sha256: 'a'.repeat(64), contentType: 'image/jpeg', byteSize: 10, width: 10, height: 10, hasThumbnail: true,
    capture: 'original', originalName: `${id}.jpg`, caption: '', sourceLabel: null, sourceMessageId: null, receivedAt: null, takenAt: null,
    hasLocation: false, revision: 1, createdAt: '2026-10-08T00:00:00Z', updatedAt: '2026-10-08T00:00:00Z',
  };
}
const file = (name: string, type: string, size = 10) => {
  const value = new File(['x'], name, { type });
  Object.defineProperty(value, 'size', { value: size });
  return value;
};

describe('sourcing upload queue', () => {
  it('uploads one file per request, at most three at a time, and reports every outcome', async () => {
    let inFlight = 0;
    let peak = 0;
    const pending: Array<() => void> = [];
    const outcomes: Record<string, () => unknown> = {
      'added.jpg': () => ResultExtensions.ok({ media: media('m1', 'sup'), duplicate: false, nearDuplicateIds: [] }),
      'same.png': () => ResultExtensions.ok({ media: media('m2', 'sup'), duplicate: true, nearDuplicateIds: [] }),
      'elsewhere.webp': () => ResultExtensions.ok({ media: media('m3', null), duplicate: true, nearDuplicateIds: [] }),
      'similar.jpg': () => ResultExtensions.ok({ media: media('m4', 'sup'), duplicate: false, nearDuplicateIds: ['m1', 'm9'] }),
      'broken.pdf': () => ResultExtensions.fail(ApiFailureFactory.create('http', { status: 400, headers: new Headers() }, { title: 'The file is not a PDF.', code: 'validation' })),
    };
    const uploadMedia = vi.fn((upload: File) => new Promise(resolve => {
      inFlight += 1;
      peak = Math.max(peak, inFlight);
      pending.push(() => { inFlight -= 1; resolve(outcomes[upload.name]!()); });
    }));
    const refreshMediaViews = vi.fn(async () => {});
    const scope = effectScope();
    const uploads = scope.run(() => useSourcingUploads({ uploadMedia, refreshMediaViews } as unknown as SourcingControls))!;

    uploads.enqueue(
      [file('added.jpg', 'image/jpeg'), file('same.png', 'image/png'), file('elsewhere.webp', 'image/webp'), file('similar.jpg', 'image/jpeg'),
        file('broken.pdf', 'application/pdf'), file('clip.gif', 'image/gif'), file('huge.jpg', 'image/jpeg', 21 * 1024 * 1024)],
      [{ file: file('movie.mov', 'video/quicktime'), reason: 'type' }],
      { supplierId: 'sup', capture: 'screenshot' },
    );
    const status = (name: string) => uploads.items.find(item => item.name === name)!;
    expect(uploadMedia).toHaveBeenCalledTimes(3);
    expect(uploadMedia).toHaveBeenCalledWith(expect.any(File), { supplierId: 'sup', capture: 'screenshot', caption: '' }, expect.any(AbortSignal));
    expect(status('added.jpg').status).toBe('uploading');
    expect(status('similar.jpg').status).toBe('queued');
    expect(status('clip.gif')).toMatchObject({ status: 'failed', message: 'Only JPEG, PNG, WebP, PDF and MP4 files are accepted.' });
    expect(status('huge.jpg')).toMatchObject({ status: 'failed', message: 'Images are limited to 20 MiB.' });
    expect(status('movie.mov').status).toBe('failed');
    expect(uploads.running.value).toBe(true);

    while (pending.length) { pending.shift()!(); await flushPromises(); }
    expect(peak).toBe(3);
    expect(uploadMedia).toHaveBeenCalledTimes(5);
    expect(status('added.jpg')).toMatchObject({ status: 'added', nearDuplicates: 0, mediaId: 'm1' });
    expect(status('same.png')).toMatchObject({ status: 'duplicate', message: 'Already here. Nothing new was stored.' });
    expect(status('elsewhere.webp')).toMatchObject({ status: 'duplicate', message: 'Already in Desk under another supplier or the inbox. Nothing new was stored.' });
    expect(status('similar.jpg')).toMatchObject({ status: 'added', nearDuplicates: 2 });
    expect(status('broken.pdf')).toMatchObject({ status: 'failed', message: 'The file was rejected. Check its type and contents.', detail: 'The file is not a PDF.' });
    expect(uploads.running.value).toBe(false);
    expect(refreshMediaViews).toHaveBeenCalledTimes(1);

    uploads.clearFinished();
    expect(uploads.items).toHaveLength(0);
    scope.stop();
  });

  it('does not refresh views when nothing in the batch reached the server', async () => {
    const uploadMedia = vi.fn();
    const refreshMediaViews = vi.fn(async () => {});
    const scope = effectScope();
    const uploads = scope.run(() => useSourcingUploads({ uploadMedia, refreshMediaViews } as unknown as SourcingControls))!;
    uploads.enqueue([file('notes.txt', 'text/plain')], [], { supplierId: null, capture: 'original' });
    await flushPromises();
    expect(uploadMedia).not.toHaveBeenCalled();
    expect(refreshMediaViews).not.toHaveBeenCalled();
    expect(uploads.items[0]?.status).toBe('failed');
    scope.stop();
  });
});
