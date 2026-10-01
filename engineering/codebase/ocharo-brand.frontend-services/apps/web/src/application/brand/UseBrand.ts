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
const busy = ref(false);
const loaded = ref(false);

async function run<T>(
  action: () => Promise<{ ok: true; value: T } | { ok: false; failure: Parameters<typeof describeFailure>[0] }>,
  message?: string,
) {
  busy.value = true;
  error.value = null;
  const result = await action();
  busy.value = false;
  if (!result.ok) {
    error.value = describeFailure(result.failure);
    return null;
  }
  if (message) notice.value = message;
  return result.value;
}

async function refresh() {
  const [d, r, a, c, k] = await Promise.all([
    brandApi.draft(),
    brandApi.releases(),
    brandApi.appearances(),
    brandApi.consumers(),
    brandApi.publicKit(),
  ]);
  if (d.ok) draft.value = d.value;
  else error.value = describeFailure(d.failure);
  if (r.ok) releases.value = r.value;
  if (a.ok) appearances.value = a.value;
  if (c.ok) consumers.value = c.value;
  kit.value = k.ok ? k.value : null;
  loaded.value = true;
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
    loaded,
    urls: brandUrls,
    refresh,
    currentVersion: (slot: string) =>
      draft.value?.logos.find((v) => v.id === draft.value?.draft.currentLogos[slot]) ?? null,
    versionsFor: (slot: string) => draft.value?.logos.filter((v) => v.slot === slot) ?? [],
    pending: computed(() => (draft.value?.latestRelease ? draft.value.unpublishedChanges : ['Hali nashr qilinmagan'])),
    async saveIdentity(identity: BrandIdentity) {
      if (!draft.value) return;
      const view = await run(
        () => brandApi.saveIdentity(draft.value!.revision, identity),
        'Identitet saqlandi (qoralama). Ilovalar nashrdan keyin ko‘radi.',
      );
      if (view) draft.value = view;
    },
    async upload(slot: string, file: File, note: string | null) {
      const version = await run(() => brandApi.upload(slot, file, note), `${file.name} yuklandi.`);
      if (version) await refresh();
      return version;
    },
    async setCurrent(slot: string, versionId: string) {
      if (!draft.value) return;
      const view = await run(
        () => brandApi.setCurrent(slot, draft.value!.revision, versionId),
        'Joriy fayl tanlandi — nashr qilinganda hamma ilovada almashadi.',
      );
      if (view) draft.value = view;
    },
    async setStatus(versionId: string, status: string) {
      if (await run(() => brandApi.setStatus(versionId, status))) await refresh();
    },
    async publish(note: string | null) {
      if (!draft.value) return;
      const release = await run(() => brandApi.publish(draft.value!.revision, note));
      if (release) {
        notice.value = `${release.version}-nashr chiqdi: ${release.changes.join('; ')}.`;
        await refresh();
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
      if (saved) await refresh();
      return saved;
    },
    async cancelAppearance(id: string) {
      if (await run(() => brandApi.cancelAppearance(id), 'Tadbir bekor qilindi.')) await refresh();
    },
  };
}
