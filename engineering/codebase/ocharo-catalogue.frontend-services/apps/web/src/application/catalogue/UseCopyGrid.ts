import { computed, ref } from 'vue';
import type { ProductCopyRow } from '../../domain/catalogue/CatalogueModels';
import { catalogueApi, describeFailure } from '../../integration/catalogue';
import { useCatalogueStore } from './CatalogueStore';

interface EditableRow {
  row: ProductCopyRow;
  baseline: string;
  saving: boolean;
  error: string | null;
}

/** Three-language copy for every product, edited in place and saved row by row (revision-checked). */
export function useCopyGrid() {
  const store = useCatalogueStore();
  const rows = ref<EditableRow[]>([]);
  const loading = ref(false);
  const dirtyCount = computed(() => rows.value.filter(isDirty).length);

  function isDirty(item: EditableRow) {
    return JSON.stringify(item.row.locales) !== item.baseline;
  }

  async function load() {
    loading.value = true;
    const result = await catalogueApi.copyRows();
    loading.value = false;
    if (!result.ok) return store.fail(describeFailure(result.failure));
    rows.value = result.value.map((row) => ({
      row: structuredClone(row),
      baseline: JSON.stringify(row.locales),
      saving: false,
      error: null,
    }));
  }

  async function save(item: EditableRow) {
    item.saving = true;
    item.error = null;
    const result = await catalogueApi.saveCopy(item.row.id, item.row.revision, item.row.locales);
    item.saving = false;
    if (!result.ok) {
      item.error = describeFailure(result.failure);
      return false;
    }
    item.row = structuredClone(result.value);
    item.baseline = JSON.stringify(result.value.locales);
    return true;
  }

  return {
    rows,
    loading,
    dirtyCount,
    isDirty,
    load,
    save,
    async saveAll() {
      let saved = 0;
      for (const item of rows.value.filter(isDirty)) if (await save(item)) saved++;
      const failed = rows.value.filter((item) => item.error).length;
      if (failed) store.fail(`${saved} ta saqlandi, ${failed} tasida xato — qatorlarni tekshiring.`);
      else store.succeed(`${saved} ta mahsulot matni saqlandi.`);
    },
  };
}
