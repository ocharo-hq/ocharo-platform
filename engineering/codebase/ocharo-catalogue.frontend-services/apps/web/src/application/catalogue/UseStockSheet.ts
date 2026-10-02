import { computed, ref } from 'vue';
import type { StockRow } from '../../domain/catalogue/CatalogueModels';
import { catalogueApi, describeFailure } from '../../integration/catalogue';
import { useCatalogueStore } from './CatalogueStore';
import { useUiLocale } from '../shared/UseUiLocale';

interface EditableStock {
  row: StockRow;
  baseline: string;
  saving: boolean;
  error: string | null;
}

/** Pieces on hand per size for every product, saved row by row (revision-checked). */
export function useStockSheet() {
  const store = useCatalogueStore();
  const { t } = useUiLocale();
  const rows = ref<EditableStock[]>([]);
  const loading = ref(false);
  const snapshot = (row: StockRow) => JSON.stringify({ stock: row.stock, madeToOrder: row.madeToOrder });
  const isDirty = (item: EditableStock) => snapshot(item.row) !== item.baseline;

  async function save(item: EditableStock) {
    item.saving = true;
    item.error = null;
    const result = await catalogueApi.saveStock(item.row.id, item.row.revision, item.row.stock, item.row.madeToOrder);
    item.saving = false;
    if (!result.ok) {
      item.error = describeFailure(result.failure, t);
      return false;
    }
    item.row = structuredClone(result.value);
    item.baseline = snapshot(result.value);
    return true;
  }

  return {
    rows,
    loading,
    isDirty,
    dirtyCount: computed(() => rows.value.filter(isDirty).length),
    total: computed(() =>
      rows.value.reduce((n, item) => n + Object.values(item.row.stock ?? {}).reduce((a, b) => a + b, 0), 0),
    ),
    async load() {
      loading.value = true;
      const result = await catalogueApi.stockRows();
      loading.value = false;
      if (!result.ok) return store.fail(describeFailure(result.failure, t));
      rows.value = result.value.map((row) => ({
        row: structuredClone(row),
        baseline: snapshot(row),
        saving: false,
        error: null,
      }));
    },
    save,
    track(item: EditableStock, tracked: boolean) {
      item.row.stock = tracked ? Object.fromEntries(item.row.sizes.map((s) => [s, item.row.stock?.[s] ?? 0])) : null;
    },
    setQuantity(item: EditableStock, size: string, value: number) {
      const stock = { ...(item.row.stock ?? Object.fromEntries(item.row.sizes.map((s) => [s, 0]))) };
      stock[size] = Number.isFinite(value) && value >= 0 ? Math.floor(value) : 0;
      item.row.stock = stock;
    },
    async saveAll() {
      let saved = 0;
      for (const item of rows.value.filter(isDirty)) if (await save(item)) saved++;
      const failed = rows.value.filter((item) => item.error).length;
      if (failed) store.fail(t('Saved: {saved}. Failed: {failed}. Check the affected rows.', { saved, failed }));
      else store.succeed(t('Stock saved for {count} products.', { count: saved }));
    },
  };
}
