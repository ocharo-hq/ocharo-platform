import { computed, onScopeDispose, reactive, ref, shallowRef, watch } from 'vue';
import type { BulkResult, ProductFilter, ProductSummary } from '../../domain/catalogue/CatalogueModels';
import { catalogueApi, describeFailure } from '../../integration/catalogue';
import { useCatalogueStore } from './CatalogueStore';

/** Loads the product grid for the current filters and runs bulk commands on a selection. */
export function useProductList(initial: ProductFilter = {}) {
  const store = useCatalogueStore();
  const filter = reactive<ProductFilter>({ ...initial });
  const products = shallowRef<ProductSummary[]>([]);
  const loading = ref(false);
  const selected = ref<Set<string>>(new Set());
  let controller: AbortController | null = null;
  let debounce: ReturnType<typeof setTimeout> | undefined;

  async function load() {
    controller?.abort();
    controller = new AbortController();
    const signal = controller.signal;
    loading.value = true;
    const clean = Object.fromEntries(
      Object.entries(filter).filter(([, v]) => v !== undefined && v !== ''),
    ) as ProductFilter;
    const result = await catalogueApi.listProducts(clean, signal);
    if (signal.aborted) return;
    loading.value = false;
    if (result.ok) {
      products.value = result.value;
      selected.value = new Set([...selected.value].filter((id) => result.value.some((p) => p.id === id)));
    } else store.fail(describeFailure(result.failure));
  }

  watch(
    () => ({ ...filter }),
    () => {
      clearTimeout(debounce);
      debounce = setTimeout(load, 180);
    },
  );
  onScopeDispose(() => {
    controller?.abort();
    clearTimeout(debounce);
  });

  function toggle(id: string) {
    const next = new Set(selected.value);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    selected.value = next;
  }

  function report(result: BulkResult, what: string) {
    const skipped = result.skipped.length ? ` O‘tkazib yuborildi: ${result.skipped.join('; ')}` : '';
    store.succeed(`${what}: ${result.changed} ta mahsulot yangilandi.${skipped}`);
  }

  return {
    filter,
    products,
    loading,
    selected,
    selectedCount: computed(() => selected.value.size),
    load,
    toggle,
    selectAll() {
      selected.value = new Set(products.value.map((p) => p.id));
    },
    clearSelection() {
      selected.value = new Set();
    },
    async setStatus(status: string) {
      const result = await catalogueApi.bulkStatus([...selected.value], status);
      if (!result.ok) return store.fail(describeFailure(result.failure));
      report(result.value, 'Holat');
      await load();
    },
    async adjustPrices(mode: 'percent' | 'add', value: number, roundTo: number, tier: 'retail' | 'wholesale' | null) {
      const result = await catalogueApi.bulkPricing([...selected.value], mode, value, roundTo, tier);
      if (!result.ok) return store.fail(describeFailure(result.failure));
      report(result.value, 'Narxlar');
      await load();
    },
    async duplicate(id: string) {
      const result = await catalogueApi.duplicateProduct(id);
      if (!result.ok) {
        store.fail(describeFailure(result.failure));
        return null;
      }
      store.succeed(`${result.value.document.id} qoralama sifatida yaratildi.`);
      return result.value.document.id;
    },
  };
}
