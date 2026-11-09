import { flushPromises } from '@vue/test-utils';
import { vi } from 'vitest';
import { ApiFailureFactory } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';

import { RefreshMinimumMs } from '../../src/application/brand/UseBrand';
import type { DraftView } from '../../src/domain/brand/BrandModels';
import { brandApi } from '../../src/integration/brand';

/** A draft on release `latestRelease` (none before the first), with `changes` waiting to be published; empty members are
 * absent, as the API omits them. */
export function draftView(latestRelease: number | null = 3, changes: string[] = ['Ranglar', 'Logotip']): DraftView {
  return {
    revision: 4,
    draft: {
      identity: {
        name: 'Ocharo',
        descriptor: { uz: 'Tavsif' },
        tagline: {},
        location: {},
        palette: [{ key: 'ink', name: 'Ink', hex: '#272924', role: 'text' }],
        typography: { display: 'Georgia', body: 'Arial' },
        links: [],
      },
      currentLogos: {},
    },
    updatedAt: '2026-10-02T00:00:00Z',
    logos: [],
    ...(latestRelease === null ? {} : { latestRelease }),
    unpublishedChanges: changes,
  };
}

/** Serves every Brand read from the fixtures above. Each mount starts from an empty cache: the query client is new. */
export function serveBrandReads(draft: DraftView = draftView()): void {
  vi.spyOn(brandApi, 'draft').mockResolvedValue(ResultExtensions.ok(draft));
  vi.spyOn(brandApi, 'releases').mockResolvedValue(ResultExtensions.ok([]));
  vi.spyOn(brandApi, 'appearances').mockResolvedValue(ResultExtensions.ok([]));
  vi.spyOn(brandApi, 'consumers').mockResolvedValue(ResultExtensions.ok([]));
  vi.spyOn(brandApi, 'publicKit').mockResolvedValue(ResultExtensions.fail(ApiFailureFactory.create('protocol')));
}

/** Waits out an asked-for read, which holds a short minimum so the skeleton never flickers. */
export async function settleBrandReads(): Promise<void> {
  await flushPromises();
  await new Promise((resolve) => setTimeout(resolve, RefreshMinimumMs));
  await flushPromises();
}
