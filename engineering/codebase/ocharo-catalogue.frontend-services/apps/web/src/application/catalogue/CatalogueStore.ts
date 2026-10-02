import { computed, ref, shallowRef } from 'vue';
import type { CatalogueSettings, GarmentType } from '../../domain/catalogue/CatalogueModels';
import { catalogueApi, describeFailure } from '../../integration/catalogue';
import { useUiLocale } from '../shared/UseUiLocale';
import { notifyError } from '../common/UseFeedback';
import type { UiLocale } from '../../domain/shared/UiMessages';

const { t, uiLocale } = useUiLocale();

/** App-wide catalogue state: persisted settings and transient feedback. */
const settings = shallowRef<CatalogueSettings | null>(null);
const settingsRevision = ref(0);
const sourceRoot = ref<string | null>(null);
const error = ref<string | null>(null);
const notice = ref<string | null>(null);
let settingsLoad: Promise<void> | null = null;

async function loadSettings(force = false) {
  if (settings.value && !force) return;
  settingsLoad ??= (async () => {
    const result = await catalogueApi.getSettings();
    if (result.ok) {
      settings.value = result.value.settings;
      settingsRevision.value = result.value.revision;
      sourceRoot.value = result.value.sourceRoot;
    } else {
      error.value = describeFailure(result.failure, t);
      notifyError(error.value);
    }
  })().finally(() => {
    settingsLoad = null;
  });
  await settingsLoad;
}

/** Shared catalogue shell state and helpers. */
export function useCatalogueStore() {
  const garmentTypes = computed<GarmentType[]>(() => settings.value?.garmentTypes ?? []);
  return {
    settings,
    settingsRevision,
    sourceRoot,
    garmentTypes,
    error,
    notice,
    loadSettings,
    garmentName(key: string, locale: UiLocale = uiLocale.value) {
      const type = garmentTypes.value.find((g) => g.key === key);
      return type ? type[locale] : key;
    },
    fail(message: string) {
      notice.value = null;
      error.value = message;
      notifyError(message);
    },
    succeed(message: string) {
      error.value = null;
      notice.value = message;
    },
  };
}
