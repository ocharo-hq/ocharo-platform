import { computed, ref, shallowRef } from 'vue';
import type {
  BrandAppearance,
  BrandIdentity,
  ConsumerView,
  DraftView,
  PublicBrand,
  ReleaseSummary,
} from '../../domain/brand/BrandModels';
import { brandApi, brandUrls, describeFailure } from '../../integration/brand';

/** App-wide brand state: the draft, releases, appearances, consumers, the published kit and one message banner. */
const draft = shallowRef<DraftView | null>(null);
const releases = shallowRef<ReleaseSummary[]>([]);
const appearances = shallowRef<BrandAppearance[]>([]);
const consumers = shallowRef<ConsumerView[]>([]);
const kit = shallowRef<PublicBrand | null>(null);
const error = ref<string | null>(null);
const notice = ref<string | null>(null);
const mutating = ref(false);
const loaded = ref(false);
const refreshing = ref(false);
const busy = computed(() => mutating.value || refreshing.value);
const queryState = ref<'loading' | 'success' | 'unavailable'>('loading');
let pendingRefresh: Promise<void> | null = null;

async function run<T>(
  action: () => Promise<{ ok: true; value: T } | { ok: false; failure: Parameters<typeof describeFailure>[0] }>,
  message?: string,
) {
  if (mutating.value) return null;
  mutating.value = true;
  try {
    // UI commands are disabled during reads; direct callers wait for an older read to settle.
    if (pendingRefresh) await pendingRefresh;
    error.value = null;
    const result = await action();
    if (!result.ok) {
      error.value = describeFailure(result.failure);
      return null;
    }
    if (message) notice.value = message;
    await refreshAfterMutation();
    return result.value;
  } catch {
    error.value = 'So‘rov bajarilmadi. O‘zgarishlaringiz saqlanib turibdi; qayta urinib ko‘ring.';
    return null;
  } finally {
    mutating.value = false;
  }
}

/** Coalesces explicit reads and preserves the last complete administrative snapshot on failure. */
function refresh(): Promise<void> {
  if (pendingRefresh) return pendingRefresh;
  refreshing.value = true;
  error.value = null;
  if (!draft.value) queryState.value = 'loading';
  const minimum = new Promise<void>((resolve) => setTimeout(resolve, 400));
  pendingRefresh = (async () => {
    try {
      const [d, r, a, c, k] = await Promise.all([
        brandApi.draft(),
        brandApi.releases(),
        brandApi.appearances(),
        brandApi.consumers(),
        brandApi.publicKit(),
      ]);
      if (!d.ok || !r.ok || !a.ok || !c.ok) {
        const failure = [d, r, a, c].find((result) => !result.ok);
        if (failure && !failure.ok) error.value = describeFailure(failure.failure);
        queryState.value = draft.value ? 'success' : 'unavailable';
        return;
      }
      draft.value = d.value;
      releases.value = r.value;
      appearances.value = a.value;
      consumers.value = c.value;
      // The public kit may be absent before the first release; it is not an administrative read failure.
      if (k.ok) kit.value = k.value;
      queryState.value = 'success';
    } catch {
      error.value = 'So‘rov bajarilmadi. O‘zgarishlaringiz saqlanib turibdi; qayta urinib ko‘ring.';
      queryState.value = draft.value ? 'success' : 'unavailable';
    } finally {
      await minimum;
      loaded.value = true;
      refreshing.value = false;
      pendingRefresh = null;
    }
  })();
  return pendingRefresh;
}

/** A command acknowledgement requires a read started after that acknowledgement. */
async function refreshAfterMutation(): Promise<void> {
  if (pendingRefresh) await pendingRefresh;
  await refresh();
}

/** Shared brand store and commands. */
export function useBrand() {
  return {
    draft,
    releases,
    appearances,
    consumers,
    kit,
    error,
    notice,
    busy,
    mutating,
    loaded,
    refreshing,
    queryState,
    urls: brandUrls,
    refresh: () => (mutating.value ? Promise.resolve() : refresh()),
    currentVersion: (slot: string) =>
      draft.value?.logos.find((v) => v.id === draft.value?.draft.currentLogos[slot]) ?? null,
    versionsFor: (slot: string) => draft.value?.logos.filter((v) => v.slot === slot) ?? [],
    pending: computed(() => (draft.value?.latestRelease ? draft.value.unpublishedChanges : ['Hali nashr qilinmagan'])),
    async saveIdentity(identity: BrandIdentity, expectedRevision: number) {
      if (!draft.value) return null;
      const view = await run(
        () => brandApi.saveIdentity(expectedRevision, identity),
        'Identitet saqlandi (qoralama). Ilovalar nashrdan keyin ko‘radi.',
      );
      return view;
    },
    async upload(slot: string, file: File, note: string | null) {
      const version = await run(() => brandApi.upload(slot, file, note), `${file.name} yuklandi.`);
      return version;
    },
    async setCurrent(slot: string, versionId: string) {
      if (!draft.value) return;
      await run(
        () => brandApi.setCurrent(slot, draft.value!.revision, versionId),
        'Joriy fayl tanlandi — nashr qilinganda hamma ilovada almashadi.',
      );
    },
    async setStatus(versionId: string, status: string) {
      await run(() => brandApi.setStatus(versionId, status));
    },
    async publish(note: string | null) {
      if (!draft.value) return;
      const release = await run(() => brandApi.publish(draft.value!.revision, note));
      if (release) {
        notice.value = `${release.version}-nashr chiqdi: ${release.changes.join('; ')}.`;
      }
    },
    async saveAppearance(body: {
      id: string | null;
      name: string;
      startsAt: string;
      endsAt: string;
      overrides: Record<string, string>;
      accentHex: string | null;
      note: string | null;
      linkLabel: string | null;
      linkUrl: string | null;
    }) {
      const saved = await run(() => brandApi.saveAppearance(body), 'Tadbir ko‘rinishi saqlandi.');
      return saved;
    },
    async cancelAppearance(id: string) {
      await run(() => brandApi.cancelAppearance(id), 'Tadbir bekor qilindi.');
    },
  };
}
