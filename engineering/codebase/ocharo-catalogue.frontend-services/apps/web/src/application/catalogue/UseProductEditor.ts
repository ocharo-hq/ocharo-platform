import { computed, ref, shallowRef } from 'vue';
import type { MediaRole, ProductDetail, ProductInput, RevisionSummary } from '../../domain/catalogue/CatalogueModels';
import { activationProblem, emptyLocale, tierProblem, toInput } from '../../domain/catalogue/CatalogueRules';
import { catalogueApi, describeFailure } from '../../integration/catalogue';
import { useCatalogueStore } from './CatalogueStore';

/** Edits one product: load, dirty tracking, revision-checked save, images, history and restore. */
export function useProductEditor() {
  const store = useCatalogueStore();
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
      if (tiers) list.push(tiers);
    }
    if (value.status === 'active') {
      const media = detail.value?.document.media.filter((m) => value.media?.some((x) => x.id === m.id)) ?? [];
      const reason = activationProblem({ locales: value.locales, media });
      if (reason) list.push(reason);
    }
    for (const code of ['uz', 'ru', 'en'] as const)
      if (value.locales[code].status === 'approved' && !value.locales[code].name?.trim())
        list.push(`Tasdiqlangan ${code.toUpperCase()} matnga nom kerak.`);
    return list;
  });

  function adopt(next: ProductDetail) {
    detail.value = next;
    draft.value = toInput(next.document, next.revision);
    baseline.value = JSON.stringify(draft.value);
    isNew.value = false;
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
        store.fail(describeFailure(result.failure));
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
      saving.value = false;
      if (!result.ok) {
        store.fail(describeFailure(result.failure));
        return null;
      }
      adopt(result.value);
      await loadHistory();
      store.succeed(`Saqlandi — ${result.value.document.id}, versiya ${result.value.revision}.`);
      return result.value.document.id;
    },
    async upload(files: FileList | File[], role: MediaRole) {
      if (!detail.value) return store.fail('Rasm qo‘shishdan oldin mahsulotni saqlang.');
      if (dirty.value) return store.fail('Avval o‘zgarishlarni saqlang, keyin rasm qo‘shing.');
      uploading.value = true;
      for (const file of Array.from(files)) {
        const result = await catalogueApi.uploadImage(detail.value.document.id, file, role, detail.value.revision);
        if (!result.ok) {
          store.fail(`${file.name}: ${describeFailure(result.failure)}`);
          break;
        }
        adopt(result.value);
      }
      uploading.value = false;
      await loadHistory();
    },
    async restore(revision: number) {
      if (!detail.value) return;
      const result = await catalogueApi.restore(detail.value.document.id, revision, detail.value.revision);
      if (!result.ok) return store.fail(describeFailure(result.failure));
      adopt(result.value);
      await loadHistory();
      store.succeed(`${revision}-versiya tiklandi (yangi versiya ${result.value.revision}).`);
    },
    reset() {
      if (detail.value) adopt(detail.value);
    },
  };
}
