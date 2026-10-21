import { vi } from 'vitest';
import { ApiFailureFactory } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';

import { useBrand } from '../../src/application/brand';
import type { DraftView } from '../../src/domain/brand/BrandModels';
import { brandApi } from '../../src/integration/brand';

/** A draft on release `latestRelease`, with `changes` waiting to be published. */
export function draftView(latestRelease: number | null = 3, changes: string[] = ['Ranglar', 'Logotip']): DraftView {
  return {
    revision: 4,
    draft: {
      identity: {
        name: 'Ocharo',
        spellingRule: null,
        descriptor: { uz: 'Tavsif', ru: null, en: null },
        tagline: { uz: null, ru: null, en: null },
        location: { uz: null, ru: null, en: null },
        palette: [{ key: 'ink', name: 'Ink', hex: '#272924', role: 'text' }],
        typography: { display: 'Georgia', body: 'Arial', notes: null },
        links: [],
        voiceNotes: null,
      },
      currentLogos: {},
    },
    updatedAt: '2026-10-02T00:00:00Z',
    logos: [],
    latestRelease,
    unpublishedChanges: changes,
  };
}

/** Serves every Brand read from the fixtures above and clears the shared store, so each test starts from nothing. */
export function serveBrandReads(draft: DraftView = draftView()): void {
  const brand = useBrand();
  brand.draft.value = null;
  brand.kit.value = null;
  brand.notice.value = null;
  brand.error.value = null;
  brand.queryState.value = 'loading';
  vi.spyOn(brandApi, 'draft').mockResolvedValue(ResultExtensions.ok(draft));
  vi.spyOn(brandApi, 'releases').mockResolvedValue(ResultExtensions.ok([]));
  vi.spyOn(brandApi, 'appearances').mockResolvedValue(ResultExtensions.ok([]));
  vi.spyOn(brandApi, 'consumers').mockResolvedValue(ResultExtensions.ok([]));
  vi.spyOn(brandApi, 'publicKit').mockResolvedValue(ResultExtensions.fail(ApiFailureFactory.create('protocol')));
}

/** Waits for the frame's first read, which holds a short minimum so the skeleton never flickers. */
export async function settleBrandReads(): Promise<void> {
  await useBrand().refresh();
}
