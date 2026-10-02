import { computed, ref, shallowRef } from 'vue';
import { useUiLocale } from '../shared/UseUiLocale';
import type { MediaRole, ProductDetail, ProductInput, RevisionSummary } from '../../domain/catalogue/CatalogueModels';
import { activationProblem, emptyLocale, tierProblem, toInput } from '../../domain/catalogue/CatalogueRules';
import { catalogueLanguages, productLocale } from '../../domain/catalogue/CatalogueLanguages';
import { catalogueApi, describeFailure } from '../../integration/catalogue';
import { useCatalogueStore } from './CatalogueStore';

/** Edits one product: load, dirty tracking, revision-checked save, images, history and restore. */
export function useProductEditor() {
  const store = useCatalogueStore();
  const { t } = useUiLocale();
  const detail = shallowRef<ProductDetail | null>(null);
  const draft = ref<ProductInput | null>(null);
  const baseline = ref('');
  const saving = ref(false);
  const uploading = ref(false);
  const history = shallowRef<RevisionSummary[]>([]);
  const isNew = ref(false);

  const dirty = computed(() => draft.value !== null && JSON.stringify(draft.value) !== baseline.value);
  const problems = computed(() => {
    const value = draft.value;
    if (!value) return [];
    const list: string[] = [];
    if (value.pricing) {
      const tiers = tierProblem(value.pricing.tiers);
      if (tiers) list.push(t(tiers));
    }
    if (value.status === 'active') {
      const media =
        detail.value?.document.media
          .filter((m) => value.media?.some((x) => x.id === m.id && x.isActive !== false))
          .map((m) => ({ ...m, isActive: true })) ?? [];
      const reason = activationProblem({ locales: value.locales, media });
      if (reason) list.push(t(reason));
    }
    for (const { code } of catalogueLanguages)
      if (
        productLocale(value.locales, code)?.status === 'approved' &&
        !productLocale(value.locales, code)?.name?.trim()
      )
        list.push(t('Approved {language} copy needs a name.', { language: code.toUpperCase() }));
    return list;
  });

  function adopt(next: ProductDetail) {
    detail.value = next;
    draft.value = toInput(next.document, next.revision);
    baseline.value = JSON.stringify(draft.value);
    isNew.value = false;
  }

  async function refreshAcknowledged(next: ProductDetail) {
    const fresh = await catalogueApi.getProduct(next.document.id);
    // Retain the server receipt if projection refresh fails: never retry a durable create automatically.
    adopt(fresh.ok ? fresh.value : next);
    return fresh.ok ? null : describeFailure(fresh.failure, t);
  }

  async function loadHistory() {
    if (!detail.value) return;
    const result = await catalogueApi.revisions(detail.value.document.id);
    if (result.ok) history.value = result.value;
  }

  return {
    detail,
    draft,
    dirty,
    saving,
    uploading,
    history,
    isNew,
    problems,
    async load(id: string) {
      const result = await catalogueApi.getProduct(id);
      if (!result.ok) {
        store.fail(describeFailure(result.failure, t));
        return false;
      }
      adopt(result.value);
      await loadHistory();
      return true;
    },
    startNew(garmentType: string) {
      detail.value = null;
      history.value = [];
      isNew.value = true;
      draft.value = {
        garmentType,
        status: 'draft',
        sortOrder: 0,
        sizes: [...(store.settings.value?.sizePreset ?? [])],
        fabricCodes: [],
        decorationCodes: [],
        tags: [],
        locales: { uz: emptyLocale(), ru: emptyLocale(), en: emptyLocale() },
        pricing: null,
        issues: [],
        notes: null,
      };
      baseline.value = JSON.stringify(draft.value);
    },
    async save(changeSummary?: string) {
      if (!draft.value || saving.value) return null;
      if (problems.value.length) {
        store.fail(problems.value.join(' '));
        return null;
      }
      saving.value = true;
      const payload = { ...draft.value, changeSummary };
      const result =
        isNew.value || !detail.value
          ? await catalogueApi.createProduct(payload)
          : await catalogueApi.updateProduct(detail.value.document.id, payload);
      if (!result.ok) {
        saving.value = false;
        store.fail(describeFailure(result.failure, t));
        return null;
      }
      const refreshError = await refreshAcknowledged(result.value);
      await loadHistory();
      store.succeed(`${t('Saved')} · ${result.value.document.id} · ${t('Revision')} ${result.value.revision}`);
      if (refreshError) store.fail(refreshError);
      saving.value = false;
      return result.value.document.id;
    },
    async upload(files: FileList | File[], role: MediaRole) {
      if (saving.value || uploading.value) return;
      if (!detail.value) return store.fail(t('Save the product before adding images.'));
      if (dirty.value) return store.fail(t('Save your changes before adding images.'));
      uploading.value = true;
      for (const file of Array.from(files)) {
        const result = await catalogueApi.uploadImage(detail.value.document.id, file, role, detail.value.revision);
        if (!result.ok) {
          store.fail(`${file.name}: ${describeFailure(result.failure, t)}`);
          break;
        }
        const refreshError = await refreshAcknowledged(result.value);
        if (refreshError) store.fail(refreshError);
      }
      uploading.value = false;
      await loadHistory();
    },
    async restore(revision: number) {
      if (!detail.value || saving.value || uploading.value) return;
      saving.value = true;
      const result = await catalogueApi.restore(detail.value.document.id, revision, detail.value.revision);
      if (!result.ok) {
        saving.value = false;
        return store.fail(describeFailure(result.failure, t));
      }
      const refreshError = await refreshAcknowledged(result.value);
      await loadHistory();
      saving.value = false;
      store.succeed(
        t('Restored revision {revision} as new revision {newRevision}.', {
          revision,
          newRevision: result.value.revision,
        }),
      );
      if (refreshError) store.fail(refreshError);
    },
    reset() {
      if (detail.value) adopt(detail.value);
    },
  };
}
