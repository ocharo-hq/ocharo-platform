import { computed, onMounted, onScopeDispose, ref, shallowRef, watch } from 'vue';
import { onBeforeRouteLeave, onBeforeRouteUpdate, useRoute } from 'vue-router';
import { notifyError, requestConfirmation } from '../common/UseFeedback';
import { useUiLocale } from '../shared/UseUiLocale';
import {
  designApi,
  DesignRequestError,
  type DesignRevision,
  type PreparedRelease,
  type ReleaseSummary,
} from '../../integration/design/DesignApi';
import { inspectDesign } from '../../integration/design/DesignPreflight';
import { describeFailure } from '../../integration/catalogue/CatalogueApi';
import type { DesignPreflightReport } from '../../domain/design/DesignPreflight';
import { exportDesignPdf, exportDesignPng, renderCatalogueBundle } from '../../integration/design/DesignExport';
import {
  cloneSnapshot,
  validateSnapshot,
  type DesignDocument,
  type DesignSnapshot,
} from '../../domain/design/EditorModels';
import { eligiblePdf, refreshCatalogue, type DesignSource } from '../../domain/design/CatalogueDesign';
export { designApi } from '../../integration/design/DesignApi';
/** Resolves request failures in the application while leaving server details unchanged. */
export function describeDesignFailure(
  cause: unknown,
  translate: (source: string, variables?: Record<string, string | number>) => string,
  fallback = 'Unable to save this catalogue.',
): string {
  return cause instanceof DesignRequestError
    ? describeFailure(cause.failure, translate)
    : translate(cause instanceof Error ? cause.message : fallback);
}
export function useCatalogueDocument() {
  const route = useRoute(),
    { t } = useUiLocale(),
    document = shallowRef<DesignDocument | null>(null),
    snapshot = ref<DesignSnapshot | null>(null),
    title = ref(''),
    source = shallowRef<DesignSource | null>(null),
    loading = ref(true),
    busy = ref(false),
    error = ref(''),
    notice = ref(''),
    prepared = shallowRef<PreparedRelease | null>(null),
    revisions = ref<DesignRevision[]>([]),
    releases = ref<ReleaseSummary[]>([]),
    preflight = ref<DesignPreflightReport | null>(null),
    recovery = ref<{ title: string; snapshot: DesignSnapshot } | null>(null),
    baseline = ref('');
  const undoStack = ref<DesignSnapshot[]>([]),
    redoStack = ref<DesignSnapshot[]>([]),
    controller = new AbortController();
  let generation = 0;
  const serialize = () => JSON.stringify({ title: title.value, snapshot: snapshot.value }),
    dirty = computed(() => !!snapshot.value && serialize() !== baseline.value),
    id = () => String(route.params.id ?? '');
  const key = () => `ocharo-catalogue-design:${id()}`;
  function fail(cause: unknown) {
    error.value = describeDesignFailure(cause, t);
    notifyError(error.value);
  }
  function accept(value: DesignDocument) {
    document.value = value;
    snapshot.value = cloneSnapshot(value.snapshot);
    title.value = value.title;
    baseline.value = serialize();
    undoStack.value = [];
    redoStack.value = [];
    prepared.value = null;
  }
  async function load() {
    const run = ++generation;
    // Route ownership changes atomically: a failed B load must never expose A under B's URL.
    document.value = null;
    snapshot.value = null;
    source.value = null;
    title.value = '';
    baseline.value = '';
    recovery.value = null;
    revisions.value = [];
    releases.value = [];
    prepared.value = null;
    preflight.value = null;
    undoStack.value = [];
    redoStack.value = [];
    notice.value = '';
    loading.value = true;
    error.value = '';
    try {
      const [value, library] = await Promise.all([
        designApi.get(id(), controller.signal),
        designApi.source(controller.signal),
      ]);
      if (run !== generation || controller.signal.aborted) return;
      accept(value);
      source.value = library;
      let stored: string | null = null;
      try {
        stored = sessionStorage.getItem(key());
      } catch {
        /* Storage is optional. */
      }
      if (stored)
        try {
          const parsed = JSON.parse(stored) as { title: string; snapshot: unknown };
          const checked = validateSnapshot(parsed.snapshot);
          if (JSON.stringify({ title: parsed.title, snapshot: checked }) !== baseline.value)
            recovery.value = { title: parsed.title, snapshot: checked };
        } catch {
          sessionStorage.removeItem(key());
        }
    } catch (cause) {
      if (run === generation && !controller.signal.aborted) fail(cause);
    } finally {
      if (run === generation) loading.value = false;
    }
  }
  function replace(next: DesignSnapshot) {
    if (!snapshot.value) return;
    undoStack.value.push(cloneSnapshot(snapshot.value));
    if (undoStack.value.length > 50) undoStack.value.shift();
    redoStack.value = [];
    snapshot.value = cloneSnapshot(next);
    prepared.value = null;
  }
  function change(edit: (next: DesignSnapshot) => void) {
    if (!snapshot.value || busy.value) return;
    const next = cloneSnapshot(snapshot.value);
    edit(next);
    replace(next);
  }
  function undo() {
    const previous = undoStack.value.pop();
    if (previous && snapshot.value) {
      redoStack.value.push(cloneSnapshot(snapshot.value));
      snapshot.value = previous;
      prepared.value = null;
    }
  }
  function redo() {
    const next = redoStack.value.pop();
    if (next && snapshot.value) {
      undoStack.value.push(cloneSnapshot(snapshot.value));
      snapshot.value = next;
      prepared.value = null;
    }
  }
  async function run(action: () => Promise<void>) {
    if (busy.value) return;
    busy.value = true;
    error.value = '';
    notice.value = '';
    try {
      await action();
    } catch (cause) {
      fail(cause);
    } finally {
      busy.value = false;
    }
  }
  async function save(): Promise<void> {
    await run(async () => {
      if (!document.value || !snapshot.value) return;
      const next = source.value ? refreshCatalogue(snapshot.value, source.value.products) : snapshot.value;
      const saved = await designApi.save(document.value.id, title.value.trim(), next, document.value.revision);
      accept(saved);
      sessionStorage.removeItem(key());
      recovery.value = null;
      notice.value = t('Catalogue saved.');
    });
  }
  async function refresh() {
    await run(async () => {
      source.value = await designApi.source();
      if (snapshot.value) replace(refreshCatalogue(snapshot.value, source.value.products));
      notice.value = t('Product fields refreshed. Save to keep this version.');
    });
  }
  const exportable = computed(() =>
    snapshot.value && source.value ? eligiblePdf(snapshot.value, source.value.products) : null,
  );
  async function download(pageId?: string, format: 'pdf' | 'png' = 'pdf') {
    await run(async () => {
      if (snapshot.value) {
        source.value = await designApi.source();
        const refreshed = refreshCatalogue(snapshot.value, source.value.products);
        if (JSON.stringify(refreshed) !== JSON.stringify(snapshot.value)) replace(refreshed);
      }
      const eligible = exportable.value?.snapshot;
      if (!eligible?.pages.length) throw new Error('No active, approved pages are available for export.');
      if (pageId && !eligible.pages.some((page) => page.id === pageId))
        throw new Error('This page contains products that are not approved for the selected language.');
      if (!(await check(eligible, pageId ? [pageId] : undefined))) return;
      if (format === 'png') {
        const page = eligible.pages.find((page) => page.id === pageId);
        if (!page) throw new Error('Choose a page to export.');
        await exportDesignPng(page, title.value);
      } else
        await exportDesignPdf(
          eligible,
          title.value,
          (page, total) => (notice.value = t('Rendering {page} of {total}', { page, total })),
          pageId ? [pageId] : undefined,
        );
      notice.value = t('Download ready.');
    });
  }
  async function prepare() {
    if (dirty.value) {
      notifyError(t('Save your changes before preparing a release.'));
      return;
    }
    await run(async () => {
      if (!document.value || !snapshot.value) return;
      const token = await designApi.preparation(document.value.id);
      if (token.designRevision !== document.value.revision)
        throw new Error('This catalogue changed. Reload before preparing.');
      const selected = cloneSnapshot(snapshot.value);
      selected.pages = selected.pages.filter((page) => token.pdfPageIds.includes(page.id));
      if (snapshot.value.catalogue?.outputs.pdf && !(await check(selected))) return;
      const bundle = snapshot.value.catalogue?.outputs.pdf
        ? await renderCatalogueBundle(
            selected,
            title.value,
            (page, total) => (notice.value = t('Rendering {page} of {total}', { page, total })),
          )
        : undefined;
      prepared.value = await designApi.prepare(
        document.value.id,
        { expectedRevision: document.value.revision, fingerprint: token.fingerprint },
        bundle,
      );
      notice.value = t('Prepared for review. Publish when ready.');
    });
  }
  async function publish() {
    if (!prepared.value || dirty.value || !document.value) return;
    if (
      !(await requestConfirmation(t('Publish this prepared catalogue? The current public edition will be replaced.'), {
        confirmLabel: t('Publish catalogue'),
      }))
    )
      return;
    await run(async () => {
      await designApi.publish(document.value!.id, prepared.value!.id, document.value!.revision);
      notice.value = t('Catalogue published.');
    });
  }
  async function history() {
    await run(async () => {
      if (document.value)
        [revisions.value, releases.value] = await Promise.all([
          designApi.revisions(document.value.id),
          designApi.releases(document.value.id),
        ]);
    });
  }
  async function withdraw() {
    if (
      !document.value ||
      dirty.value ||
      !(await requestConfirmation(
        t('Withdraw all published editions of this catalogue? Existing public links will stop serving it.'),
        { confirmLabel: t('Withdraw catalogue') },
      ))
    )
      return;
    await run(async () => {
      await designApi.withdraw(document.value!.id, document.value!.revision);
      prepared.value = null;
      releases.value = await designApi.releases(document.value!.id);
      notice.value = t('Published catalogue withdrawn.');
    });
  }
  async function check(value: DesignSnapshot, pageIds?: string[]) {
    preflight.value = await inspectDesign(value, pageIds);
    if (preflight.value.issues.some((issue) => issue.severity === 'error')) {
      notice.value = t('Resolve the export issues before downloading.');
      return false;
    }
    return (
      !preflight.value.issues.length ||
      (await requestConfirmation(
        t('The page check found {count} warnings. Continue with the current layout?', {
          count: preflight.value.issues.length,
        }),
        { confirmLabel: t('Continue export') },
      ))
    );
  }
  async function restore(revision: number) {
    if (
      !document.value ||
      !(await requestConfirmation(
        t('Restore this revision as an unsaved draft? Current changes can be recovered with Undo.'),
        { confirmLabel: t('Restore revision') },
      ))
    )
      return;
    await run(async () => {
      const old = await designApi.revision(document.value!.id, revision);
      replace(old.snapshot);
      title.value = old.title;
      notice.value = t('Historical revision loaded. Save to create a new revision.');
    });
  }
  async function leave() {
    return (
      !busy.value &&
      (!dirty.value ||
        (await requestConfirmation(
          t('Leave without saving catalogue changes? A recovery copy remains in this browser tab.'),
          { confirmLabel: t('Leave designer') },
        )))
    );
  }
  function beforeUnload(event: BeforeUnloadEvent) {
    if (dirty.value) {
      event.preventDefault();
      event.returnValue = '';
    }
  }
  watch(
    [snapshot, title],
    () => {
      if (dirty.value)
        try {
          sessionStorage.setItem(key(), serialize());
        } catch {
          /* Storage quota never blocks editing. */
        }
    },
    { deep: true },
  );
  onBeforeRouteLeave(leave);
  onBeforeRouteUpdate(async (to, from) => to.params.id === from.params.id || (await leave()));
  watch(
    () => route.params.id,
    () => void load(),
  );
  onMounted(() => {
    void load();
    window.addEventListener('beforeunload', beforeUnload);
  });
  onScopeDispose(() => {
    controller.abort();
    window.removeEventListener('beforeunload', beforeUnload);
  });
  return {
    document,
    snapshot,
    title,
    source,
    loading,
    busy,
    error,
    notice,
    dirty,
    prepared,
    revisions,
    releases,
    preflight,
    recovery,
    exportable,
    canUndo: computed(() => undoStack.value.length > 0),
    canRedo: computed(() => redoStack.value.length > 0),
    load,
    replace,
    change,
    undo,
    redo,
    save,
    setTitle(value: string) {
      if (!busy.value) title.value = value;
    },
    refresh,
    download,
    prepare,
    publish,
    withdraw,
    history,
    restore,
    restoreRecovery() {
      if (recovery.value) {
        replace(recovery.value.snapshot);
        title.value = recovery.value.title;
        recovery.value = null;
      }
    },
    dismissRecovery() {
      recovery.value = null;
      sessionStorage.removeItem(key());
    },
  };
}
export type CatalogueDocumentControls = ReturnType<typeof useCatalogueDocument>;
