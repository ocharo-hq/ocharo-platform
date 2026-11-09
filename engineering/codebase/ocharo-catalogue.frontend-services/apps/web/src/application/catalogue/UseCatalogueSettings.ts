import { computed, toRaw } from 'vue';
import { useAppQuery, useQueryCache } from '@wow-two-beta/fe/query';
import type { GarmentType, SettingsEnvelope } from '../../domain/catalogue/CatalogueModels';
import type { UiLocale } from '../../domain/shared/UiMessages';
import { catalogueApi, describeFailure } from '../../integration/catalogue';
import { useUiLocale } from '../shared/UseUiLocale';
import { CatalogueQueryKeys } from './CatalogueQueryKeys';
import { useCatalogueStore } from './CatalogueStore';

/**
 * The catalogue settings from the session-bound query cache, shared by the frame and every page: the brand name, prices
 * note, size preset, contact lines and garment types, with their revision. A failed read is reported once and keeps
 * the last settings read.
 */
export function useCatalogueSettings() {
  const store = useCatalogueStore();
  const { t, uiLocale } = useUiLocale();
  const cache = useQueryCache();
  const read = async ({ signal }: { signal: AbortSignal }) => {
    const result = await catalogueApi.getSettings(signal);
    if (!result.ok) store.fail(describeFailure(result.failure, t));
    return result;
  };
  const query = useAppQuery({ key: CatalogueQueryKeys.settings, queryFn: read });
  // The cache holds acknowledged settings that are never edited in place; callers get them as plain objects, which a
  // form can clone.
  const envelope = computed<SettingsEnvelope | null>(() => (query.data.value ? toRaw(query.data.value) : null));
  const garmentTypes = computed<GarmentType[]>(() => envelope.value?.settings.garmentTypes ?? []);
  return {
    settings: computed(() => envelope.value?.settings ?? null),
    settingsRevision: computed(() => envelope.value?.revision ?? 0),
    sourceRoot: computed(() => envelope.value?.sourceRoot ?? null),
    garmentTypes,
    /** True through the first read, while no settings are known yet. */
    loading: query.isFirstLoad,
    /** A garment type's name in `locale`, else its key. */
    garmentName(key: string, locale: UiLocale = uiLocale.value) {
      const type = garmentTypes.value.find((g) => g.key === key);
      return type ? type[locale] : key;
    },
    /** Re-reads the settings. */
    reload: () => cache.invalidate(CatalogueQueryKeys.settings),
    /** Reads the settings unless a current read is cached, and answers them; null when the read failed. */
    async ensure(): Promise<SettingsEnvelope | null> {
      await cache.prefetch({ key: CatalogueQueryKeys.settings, queryFn: read });
      return cache.get<SettingsEnvelope>(CatalogueQueryKeys.settings) ?? null;
    },
    /** Holds an acknowledged save's settings until the read that follows it lands. */
    accept: (value: SettingsEnvelope) => cache.set(CatalogueQueryKeys.settings, value),
  };
}
