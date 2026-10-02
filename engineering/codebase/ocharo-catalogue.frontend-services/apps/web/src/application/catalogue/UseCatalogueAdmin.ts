import { ref, shallowRef } from 'vue';
import type {
  CatalogueSettings,
  ImportReport,
  LocaleCode,
  PublicCatalogue,
} from '../../domain/catalogue/CatalogueModels';
import { catalogueApi, catalogueExports, describeFailure } from '../../integration/catalogue';
import { useCatalogueStore } from './CatalogueStore';
import { useUiLocale } from '../shared/UseUiLocale';

/** Settings, workspace import and exports. */
export function useCatalogueAdmin() {
  const store = useCatalogueStore();
  const { t } = useUiLocale();
  const busy = ref(false);
  const report = shallowRef<ImportReport | null>(null);
  return {
    busy,
    report,
    exports: catalogueExports,
    async saveSettings(settings: CatalogueSettings) {
      if (busy.value) return;
      busy.value = true;
      try {
        const result = await catalogueApi.saveSettings(settings, store.settingsRevision.value);
        if (!result.ok) return store.fail(describeFailure(result.failure, t));
        store.settings.value = result.value.settings;
        store.settingsRevision.value = result.value.revision;
        store.succeed(t('Settings saved.'));
        await store.loadSettings(true);
      } finally {
        busy.value = false;
      }
    },
    async importWorkspace(updateExisting: boolean, includeReferenceImages: boolean) {
      if (busy.value) return;
      busy.value = true;
      const result = await catalogueApi.importWorkspace(updateExisting, includeReferenceImages);
      busy.value = false;
      if (!result.ok) return store.fail(describeFailure(result.failure, t));
      report.value = result.value;
      store.succeed(
        t('Import: {created} created, {updated} updated, {unchanged} unchanged.', {
          created: result.value.created,
          updated: result.value.updated,
          unchanged: result.value.unchanged,
        }),
      );
    },
  };
}

/** The public feed, as the print/lookbook view and sibling apps see it. */
export function usePublicCatalogue() {
  const store = useCatalogueStore();
  const { t } = useUiLocale();
  const catalogue = shallowRef<PublicCatalogue | null>(null);
  const loading = ref(false);
  return {
    catalogue,
    loading,
    async load(locale: LocaleCode) {
      loading.value = true;
      const result = await catalogueApi.publicCatalogue(locale);
      loading.value = false;
      if (result.ok) catalogue.value = result.value;
      else store.fail(describeFailure(result.failure, t));
    },
  };
}
