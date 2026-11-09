import { computed, toRaw } from 'vue';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import { useAppQuery } from '@wow-two-beta/fe/query';
import { brandContactLines, type BrandKit } from '../../domain/catalogue/BrandKit';
import { fetchBrandKit } from '../../integration/brand';
import { useBrandApiUrl } from '../shared/UseOcharoApps';
import { CatalogueQueryKeys } from './CatalogueQueryKeys';

/** The published Ocharo brand kit (logo, links) from the query cache; absent when Brand is not configured or running. */
export function useBrandKit() {
  const brandApiUrl = useBrandApiUrl();
  const query = useAppQuery({
    key: CatalogueQueryKeys.brandKit(brandApiUrl),
    queryFn: async () => ResultExtensions.ok<BrandKit | null>(await fetchBrandKit(brandApiUrl)),
  });
  const kit = computed(() => (query.data.value ? toRaw(query.data.value) : null));
  return {
    kit,
    /** On-light logo file (immutable URL of the published release), or null to print the name. */
    logoUrl: computed(() => kit.value?.logos['on-light']?.fileUrl ?? kit.value?.logos.master?.fileUrl ?? null),
    contactLines: (extraLines: readonly string[]) => brandContactLines(kit.value, extraLines),
  };
}
