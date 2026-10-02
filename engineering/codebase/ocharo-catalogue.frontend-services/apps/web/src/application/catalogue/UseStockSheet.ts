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
  const busy = ref(false);
  const snapshot = (row: StockRow) => JSON.stringify({ stock: row.stock, madeToOrder: row.madeToOrder });
  const isDirty = (item: EditableStock) => snapshot(item.row) !== item.baseline;

  async function save(item: EditableStock) {
    if (item.saving) return false;
    item.saving = true;
    item.error = null;
    const result = await catalogueApi.saveStock(item.row.id, item.row.revision, item.row.stock, item.row.madeToOrder);
    if (!result.ok) {
      item.saving = false;
      item.error = describeFailure(result.failure, t);
      store.fail(item.error);
      return false;
    }
    const projection = await catalogueApi.stockRows();
    const refreshed = projection.ok ? projection.value.find((row) => row.id === item.row.id) : undefined;
    item.row = structuredClone(refreshed ?? result.value);
    if (!projection.ok) store.fail(describeFailure(projection.failure, t));
    item.baseline = snapshot(item.row);
    item.saving = false;
    return true;
  }

  return {
    rows,
    loading,
    busy,
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
      if (busy.value) return;
      busy.value = true;
      try {
        let saved = 0;
        for (const item of rows.value.filter(isDirty)) if (await save(item)) saved++;
        const failed = rows.value.filter((item) => item.error).length;
        if (failed) store.fail(t('Saved: {saved}. Failed: {failed}. Check the affected rows.', { saved, failed }));
        else store.succeed(t('Stock saved for {count} products.', { count: saved }));
      } finally {
        busy.value = false;
      }
    },
  };
}
