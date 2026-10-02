import { ref } from 'vue';
import { toInput } from '../../domain/catalogue/CatalogueRules';
import { catalogueApi, describeFailure } from '../../integration/catalogue';
import { useCatalogueStore } from './CatalogueStore';
import { useUiLocale } from '../shared/UseUiLocale';

/** Inline single-price edits: reads the current revision, changes one tier, saves revision-checked. */
export function usePriceSheet() {
  const store = useCatalogueStore();
  const { t } = useUiLocale();
  const saving = ref<string | null>(null);
  return {
    saving,
    async setPrice(productId: string, tier: 'retail' | 'wholesale', value: number) {
      if (!Number.isFinite(value) || value < 0) return store.fail(t('Enter a valid price.'));
      saving.value = `${productId}:${tier}`;
      try {
        const current = await catalogueApi.getProduct(productId);
        if (!current.ok) return store.fail(describeFailure(current.failure, t));
        const input = toInput(current.value.document, current.value.revision);
        const pricing = input.pricing ?? { currency: 'UZS', status: 'draft' as const, tiers: [], wording: null };
        const tiers = [...pricing.tiers].sort((a, b) => a.minimumQuantity - b.minimumQuantity);
        if (tier === 'retail') {
          const retail = tiers.find((t) => t.minimumQuantity === 1);
          if (retail) retail.unitPrice = value;
          else
            tiers.unshift({
              minimumQuantity: 1,
              maximumQuantity: tiers.length ? tiers[0]!.minimumQuantity - 1 : null,
              unitPrice: value,
            });
        } else {
          const bulk = tiers.length > 1 ? tiers[tiers.length - 1] : undefined;
          if (bulk) bulk.unitPrice = value;
          else {
            const retail = tiers[0];
            if (retail) retail.maximumQuantity = 9;
            tiers.push({ minimumQuantity: 10, maximumQuantity: null, unitPrice: value });
          }
        }
        input.pricing = { ...pricing, tiers, status: 'draft' };
        const saved = await catalogueApi.updateProduct(productId, {
          ...input,
          changeSummary: `Narx (${tier === 'retail' ? '1 dona' : 'ulgurji'}) o‘zgardi`,
        });
        if (!saved.ok) return store.fail(describeFailure(saved.failure, t));
        store.succeed(
          t('Price saved for {product}. Confirm the draft price before publication.', { product: productId }),
        );
        return saved.value;
      } finally {
        saving.value = null;
      }
    },
  };
}
