import { computed, onScopeDispose, reactive, ref, shallowRef, watch } from 'vue';
import type { BulkResult, ProductFilter, ProductSummary } from '../../domain/catalogue/CatalogueModels';
import { catalogueApi, describeFailure } from '../../integration/catalogue';
import { useCatalogueStore } from './CatalogueStore';
import { useUiLocale } from '../shared/UseUiLocale';

/** Loads the product grid for the current filters and runs bulk commands on a selection. */
export function useProductList(initial: ProductFilter = {}) {
  const store = useCatalogueStore();
  const { t } = useUiLocale();
  const filter = reactive<ProductFilter>({ ...initial });
  const products = shallowRef<ProductSummary[]>([]);
  const loading = ref(false);
  const pending = ref<string | null>(null);
  const loadFailed = ref(false);
  const loaded = ref(false);
  const selected = ref<Set<string>>(new Set());
  let controller: AbortController | null = null;
  let debounce: ReturnType<typeof setTimeout> | undefined;

  async function load() {
    controller?.abort();
    controller = new AbortController();
    const signal = controller.signal;
    loading.value = true;
    loadFailed.value = false;
    const clean = Object.fromEntries(
      Object.entries(filter).filter(([, v]) => v !== undefined && v !== ''),
    ) as ProductFilter;
    const result = await catalogueApi.listProducts(clean, signal);
    if (signal.aborted) return;
    loading.value = false;
    if (result.ok) {
      products.value = result.value;
      loaded.value = true;
      selected.value = new Set([...selected.value].filter((id) => result.value.some((p) => p.id === id)));
    } else {
      loadFailed.value = true;
      store.fail(describeFailure(result.failure, t));
    }
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
    const skipped = result.skipped.length ? ` ${t('Skipped: {items}', { items: result.skipped.join('; ') })}` : '';
    store.succeed(`${t('{action}: updated {count} products.', { action: t(what), count: result.changed })}${skipped}`);
  }

  return {
    filter,
    products,
    loading,
    pending,
    loadFailed,
    loaded,
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
      if (pending.value) return;
      pending.value = `status:${status}`;
      try {
        const result = await catalogueApi.bulkStatus([...selected.value], status);
        if (!result.ok) return store.fail(describeFailure(result.failure, t));
        report(result.value, 'Status');
        await load();
      } finally {
        pending.value = null;
      }
    },
    async adjustPrices(mode: 'percent' | 'add', value: number, roundTo: number, tier: 'retail' | 'wholesale' | null) {
      if (pending.value) return;
      pending.value = 'prices';
      try {
        const result = await catalogueApi.bulkPricing([...selected.value], mode, value, roundTo, tier);
        if (!result.ok) return store.fail(describeFailure(result.failure, t));
        report(result.value, 'Prices');
        await load();
      } finally {
        pending.value = null;
      }
    },
    async duplicate(id: string) {
      if (pending.value) return null;
      pending.value = `duplicate:${id}`;
      try {
        const result = await catalogueApi.duplicateProduct(id);
        if (!result.ok) {
          store.fail(describeFailure(result.failure, t));
          return null;
        }
        store.succeed(t('Created {product} as a draft.', { product: result.value.document.id }));
        await load();
        return result.value.document.id;
      } finally {
        pending.value = null;
      }
    },
  };
}
