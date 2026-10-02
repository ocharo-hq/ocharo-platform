import { onScopeDispose, ref, shallowRef } from 'vue';
import { publishedCatalogueApi, type PublishedCatalogueApi } from '../../integration/public/PublishedCatalogueApi';
import {
  catalogueLanguageSupported,
  validPublishedSlug,
  validReleaseId,
  type PublishedCatalogue,
} from '../../domain/public/PublishedCatalogue';

/** Clears other editions atomically; a failed same-edition refresh retains the last successful content. */
export function usePublishedCatalogue(api: PublishedCatalogueApi = publishedCatalogueApi) {
  const edition = shallowRef<PublishedCatalogue | null>(null);
  const loading = ref(false),
    notFound = ref(false),
    error = ref<string | null>(null);
  let identity = '';
  let request = 0,
    controller: AbortController | null = null;
  async function load(slug: string, locale: string, release?: string) {
    const ticket = ++request;
    controller?.abort();
    controller = new AbortController();
    const nextIdentity = JSON.stringify([slug, locale, release ?? '']);
    if (identity !== nextIdentity) edition.value = null;
    identity = nextIdentity;
    error.value = null;
    notFound.value = false;
    if (!validPublishedSlug(slug) || !catalogueLanguageSupported(locale) || (release && !validReleaseId(release))) {
      edition.value = null;
      loading.value = false;
      notFound.value = true;
      return;
    }
    loading.value = true;
    const result = await api.read(slug, locale, release, controller.signal);
    if (ticket !== request) return;
    loading.value = false;
    if (!result.ok) {
      if (result.failure.code === 'cancelled') return;
      if (result.failure.status === 404) notFound.value = true;
      else error.value = 'We could not load this catalogue. Please retry.';
      return;
    }
    edition.value = result.value;
  }
  onScopeDispose(() => {
    ++request;
    controller?.abort();
  });
  return { edition, loading, notFound, error, load };
}
