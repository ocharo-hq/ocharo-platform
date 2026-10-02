import { computed, ref } from 'vue';
import type { ProductCopyRow } from '../../domain/catalogue/CatalogueModels';
import { catalogueApi, describeFailure } from '../../integration/catalogue';
import { useCatalogueStore } from './CatalogueStore';
import { useUiLocale } from '../shared/UseUiLocale';

interface EditableRow {
  row: ProductCopyRow;
  baseline: string;
  saving: boolean;
  error: string | null;
}

/** Three-language copy for every product, edited in place and saved row by row (revision-checked). */
export function useCopyGrid() {
  const store = useCatalogueStore();
  const { t } = useUiLocale();
  const rows = ref<EditableRow[]>([]);
  const loading = ref(false);
  const busy = ref(false);
  const dirtyCount = computed(() => rows.value.filter(isDirty).length);

  function isDirty(item: EditableRow) {
    return JSON.stringify(item.row.locales) !== item.baseline;
  }

  async function load() {
    loading.value = true;
    const result = await catalogueApi.copyRows();
    loading.value = false;
    if (!result.ok) return store.fail(describeFailure(result.failure, t));
    rows.value = result.value.map((row) => ({
      row: structuredClone(row),
      baseline: JSON.stringify(row.locales),
      saving: false,
      error: null,
    }));
  }

  async function save(item: EditableRow) {
    if (item.saving) return false;
    item.saving = true;
    item.error = null;
    const result = await catalogueApi.saveCopy(item.row.id, item.row.revision, item.row.locales);
    if (!result.ok) {
      item.saving = false;
      item.error = describeFailure(result.failure, t);
      store.fail(item.error);
      return false;
    }
    const projection = await catalogueApi.copyRows();
    const refreshed = projection.ok ? projection.value.find((row) => row.id === item.row.id) : undefined;
    item.row = structuredClone(refreshed ?? result.value);
    if (!projection.ok) store.fail(describeFailure(projection.failure, t));
    item.baseline = JSON.stringify(item.row.locales);
    item.saving = false;
    return true;
  }

  return {
    rows,
    loading,
    busy,
    dirtyCount,
    isDirty,
    load,
    save,
    async saveAll() {
      if (busy.value) return;
      busy.value = true;
      try {
        let saved = 0;
        for (const item of rows.value.filter(isDirty)) if (await save(item)) saved++;
        const failed = rows.value.filter((item) => item.error).length;
        if (failed) store.fail(t('Saved {saved}; {failed} failed. Check the rows.', { saved, failed }));
        else store.succeed(t('Saved copy for {count} products.', { count: saved }));
      } finally {
        busy.value = false;
      }
    },
  };
}
