import { computed, ref, toRaw } from 'vue';
import { AuthStatus, BffSessionExtensions } from '@wow-two-beta/fe/auth';
import type { ApiFailure } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions, type Result } from '@wow-two-beta/fe/foundation/results';
import { useAppQuery, useQueryCache } from '@wow-two-beta/fe/query';
import type {
  BrandAppearance,
  BrandIdentity,
  ConsumerView,
  DraftView,
  PublicBrand,
  ReleaseSummary,
} from '../../domain/brand/BrandModels';
import { brandApi, brandUrls, describeFailure } from '../../integration/brand';
import { identityBridge } from '../../integration/common/IdentitySession';
import { notifyError } from '../common/UseFeedback';
import { noteRefusal } from '../common/UseIdentitySession';
import { BrandQueryKeys } from './BrandQueryKeys';

/** Everything the frame and its pages read from Brand's API, as one acknowledged moment. */
export interface BrandSnapshot {
  readonly draft: DraftView;
  readonly releases: ReleaseSummary[];
  readonly appearances: BrandAppearance[];
  readonly consumers: ConsumerView[];
  /** The published kit; absent before the first release, and kept from the last read when only it fails. */
  readonly kit: PublicBrand | null;
}

/** The shortest time an asked-for read shows as running, so the user can tell it ran. */
export const RefreshMinimumMs = 400;

/** The copy for a failure that left no API answer. */
const UnexpectedFailure = 'So‘rov bajarilmadi. O‘zgarishlaringiz saqlanib turibdi; qayta urinib ko‘ring.';

// Local interface state shared by the frame and its pages; server values live in the query cache.
const error = ref<string | null>(null);
const notice = ref<string | null>(null);
const mutating = ref(false);
const refreshing = ref(false);
let pendingRefresh: Promise<void> | null = null;

// A new person or organization starts with no banner of the old one; the query client clears their reads.
let sessionIdentity: string | null = null;
identityBridge.subscribeSession(({ status, user }) => {
  const next = status === AuthStatus.Authenticated && user ? BffSessionExtensions.identity(user) : null;
  if (next === sessionIdentity) return;
  sessionIdentity = next;
  error.value = null;
  notice.value = null;
});

/**
 * Reports a failure once through the toast host, in safe operator copy. A cancellation stays quiet, and so does a 401:
 * the frame offers to reconnect instead. A 403 re-reads the session.
 */
function report(failure: ApiFailure): void {
  void noteRefusal(failure);
  if (failure.status === 401) return;
  const message = describeFailure(failure);
  if (!message) return;
  error.value = message;
  notifyError(message);
}

/** Reads the four administrative projections and the published kit together; any administrative failure fails all. */
async function readSnapshot(
  signal: AbortSignal,
  lastKit: PublicBrand | null,
): Promise<Result<BrandSnapshot, ApiFailure>> {
  const [draft, releases, appearances, consumers, kit] = await Promise.all([
    brandApi.draft(signal),
    brandApi.releases(signal),
    brandApi.appearances(signal),
    brandApi.consumers(signal),
    brandApi.publicKit(signal),
  ]);
  if (!draft.ok) return draft;
  if (!releases.ok) return releases;
  if (!appearances.ok) return appearances;
  if (!consumers.ok) return consumers;
  return ResultExtensions.ok({
    draft: draft.value,
    releases: releases.value,
    appearances: appearances.value,
    consumers: consumers.value,
    // The public kit may be absent before the first release; it is not an administrative read failure.
    kit: kit.ok ? kit.value : lastKit,
  });
}

/** Runs one asked-for read at a time, held for {@link RefreshMinimumMs}; a second ask joins the running one. */
function startRefresh(read: () => Promise<void>): Promise<void> {
  if (pendingRefresh) return pendingRefresh;
  refreshing.value = true;
  error.value = null;
  const minimum = new Promise<void>((resolve) => setTimeout(resolve, RefreshMinimumMs));
  pendingRefresh = (async () => {
    try {
      await read();
    } finally {
      await minimum;
      refreshing.value = false;
      pendingRefresh = null;
    }
  })();
  return pendingRefresh;
}

/**
 * The brand's server state and commands. Reads come from the session-bound query cache, shared by every caller, and
 * keep the last complete snapshot when a later read fails. A command waits for a running read, never overlaps another
 * command, and is acknowledged only by a read started after it.
 */
export function useBrand() {
  const cache = useQueryCache();
  const query = useAppQuery({
    key: BrandQueryKeys.snapshot,
    queryFn: async ({ signal }) => {
      const outcome = await readSnapshot(signal, cache.get<BrandSnapshot>(BrandQueryKeys.snapshot)?.kit ?? null);
      if (!outcome.ok) report(outcome.failure);
      return outcome;
    },
  });
  // The cache holds acknowledged snapshots that are never edited in place; callers get them as plain objects.
  const snapshot = computed(() => (query.data.value ? toRaw(query.data.value) : null));
  const draft = computed(() => snapshot.value?.draft ?? null);
  const reading = computed(() => refreshing.value || query.isFirstLoad.value);
  const read = () => cache.invalidate(BrandQueryKeys.snapshot);

  /** A command acknowledgement requires a read started after that acknowledgement. */
  async function refreshAfterMutation(): Promise<void> {
    if (pendingRefresh) await pendingRefresh;
    await startRefresh(read);
  }

  async function run<T>(action: () => Promise<Result<T, ApiFailure>>, message?: string): Promise<T | null> {
    if (mutating.value) return null;
    mutating.value = true;
    try {
      // UI commands are disabled during reads; direct callers wait for an older read to settle.
      if (pendingRefresh) await pendingRefresh;
      error.value = null;
      const result = await action();
      if (!result.ok) {
        report(result.failure);
        return null;
      }
      if (message) notice.value = message;
      await refreshAfterMutation();
      return result.value;
    } catch {
      error.value = UnexpectedFailure;
      notifyError(UnexpectedFailure);
      return null;
    } finally {
      mutating.value = false;
    }
  }

  return {
    draft,
    releases: computed(() => snapshot.value?.releases ?? []),
    appearances: computed(() => snapshot.value?.appearances ?? []),
    consumers: computed(() => snapshot.value?.consumers ?? []),
    kit: computed(() => snapshot.value?.kit ?? null),
    /** The last failure's operator copy, already reported through the toast host. */
    error,
    notice,
    busy: computed(() => mutating.value || reading.value),
    mutating,
    /** True through the first read and every asked-for or acknowledging read. */
    refreshing: reading,
    /** The first read's outcome: the last complete snapshot stays `success` through later failures. */
    queryState: computed<'loading' | 'success' | 'unavailable'>(() =>
      snapshot.value
        ? 'success'
        : query.error.value && !reading.value && !query.isRefreshing.value
          ? 'unavailable'
          : 'loading',
    ),
    urls: brandUrls,
    /** Re-reads every Brand projection; does nothing while a command runs, which reads once it is acknowledged. */
    refresh: () => (mutating.value ? Promise.resolve() : startRefresh(read)),
    currentVersion: (slot: string) =>
      draft.value?.logos.find((v) => v.id === draft.value?.draft.currentLogos[slot]) ?? null,
    versionsFor: (slot: string) => draft.value?.logos.filter((v) => v.slot === slot) ?? [],
    pending: computed(() => (draft.value?.latestRelease ? draft.value.unpublishedChanges : ['Hali nashr qilinmagan'])),
    async saveIdentity(identity: BrandIdentity, expectedRevision: number) {
      if (!draft.value) return null;
      return run(
        () => brandApi.saveIdentity(expectedRevision, identity),
        'Identitet saqlandi (qoralama). Ilovalar nashrdan keyin ko‘radi.',
      );
    },
    async upload(slot: string, file: File, note: string | null) {
      return run(() => brandApi.upload(slot, file, note), `${file.name} yuklandi.`);
    },
    async setCurrent(slot: string, versionId: string) {
      const current = draft.value;
      if (!current) return;
      await run(
        () => brandApi.setCurrent(slot, current.revision, versionId),
        'Joriy fayl tanlandi — nashr qilinganda hamma ilovada almashadi.',
      );
    },
    async setStatus(versionId: string, status: string) {
      await run(() => brandApi.setStatus(versionId, status));
    },
    async publish(note: string | null) {
      const current = draft.value;
      if (!current) return;
      const release = await run(() => brandApi.publish(current.revision, note));
      if (release) notice.value = `${release.version}-nashr chiqdi: ${release.changes.join('; ')}.`;
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
      return run(() => brandApi.saveAppearance(body), 'Tadbir ko‘rinishi saqlandi.');
    },
    async cancelAppearance(id: string) {
      await run(() => brandApi.cancelAppearance(id), 'Tadbir bekor qilindi.');
    },
  };
}
