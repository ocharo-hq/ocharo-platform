import { ref, shallowRef } from 'vue';
import type { CollectionDetail, CollectionNames } from '../../domain/catalogue/CatalogueModels';
import { catalogueApi, describeFailure } from '../../integration/catalogue';
import { useCatalogueStore } from './CatalogueStore';
import { useUiLocale } from '../shared/UseUiLocale';

export interface CollectionDraft {
  id: string | null;
  expectedRevision: number | null;
  names: CollectionNames;
  description: string | null;
  productIds: string[];
  isPublished: boolean;
  sortOrder: number;
}

/** Collections list and editor. */
export function useCollections() {
  const store = useCatalogueStore();
  const { t } = useUiLocale();
  const collections = shallowRef<CollectionDetail[]>([]);
  const busy = ref(false);

  async function load() {
    const result = await catalogueApi.listCollections();
    if (result.ok) collections.value = result.value;
    else store.fail(describeFailure(result.failure, t));
  }

  return {
    collections,
    busy,
    load,
    blank(): CollectionDraft {
      return {
        id: null,
        expectedRevision: null,
        names: { uz: '', ru: null, en: null },
        description: null,
        productIds: [],
        isPublished: false,
        sortOrder: collections.value.length,
      };
    },
    edit(detail: CollectionDetail): CollectionDraft {
      return { ...structuredClone(detail.document), id: detail.document.id, expectedRevision: detail.revision };
    },
    async save(draft: CollectionDraft) {
      busy.value = true;
      const result = await catalogueApi.saveCollection(draft.expectedRevision === null ? null : draft.id, {
        expectedRevision: draft.expectedRevision,
        id: draft.id ?? undefined,
        names: draft.names,
        description: draft.description,
        productIds: draft.productIds,
        isPublished: draft.isPublished,
        sortOrder: draft.sortOrder,
      });
      busy.value = false;
      if (!result.ok) {
        store.fail(describeFailure(result.failure, t));
        return null;
      }
      store.succeed(t('Collection “{name}” saved.', { name: result.value.document.names.uz }));
      await load();
      return result.value;
    },
    async remove(id: string) {
      const result = await catalogueApi.deleteCollection(id);
      if (!result.ok) return store.fail(describeFailure(result.failure, t));
      store.succeed(t('Collection deleted. Its products remain.'));
      await load();
    },
  };
}
