import { computed, shallowRef } from 'vue';
import { brandContactLines, type BrandKit } from '../../domain/catalogue/BrandKit';
import { fetchBrandKit } from '../../integration/brand';
import { useBrandApiUrl } from '../shared/UseOcharoApps';

const kit = shallowRef<BrandKit | null>(null);
let pending: Promise<void> | null = null;

/** The published Ocharo brand kit (logo, links), loaded once per page session. */
export function useBrandKit() {
  pending ??= fetchBrandKit(useBrandApiUrl()).then((value) => {
    kit.value = value;
  });
  return {
    kit,
    /** On-light logo file (immutable URL of the published release), or null to print the name. */
    logoUrl: computed(() => kit.value?.logos['on-light']?.fileUrl ?? kit.value?.logos.master?.fileUrl ?? null),
    contactLines: (extraLines: readonly string[]) => brandContactLines(kit.value, extraLines),
  };
}
