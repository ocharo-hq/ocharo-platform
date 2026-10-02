<script setup lang="ts">
import { useRefresh } from '@wow-two-beta/ui-vue/query';
import { SkeletonStateGroup, SkeletonStateSlot } from '@wow-two-beta/ui-vue/presentation/feedback';
import RefreshButton from '../common/RefreshButton.vue';
import ResultEmpty from '../common/ResultEmpty.vue';
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter, type LocationQueryRaw } from 'vue-router';
import { ArrowDownToLine, Bookmark, Check, Share2 } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { usePublishedCatalogue } from '../../application/public/UsePublishedCatalogue';
import { useUiLocale } from '../../application/shared/UseUiLocale';
import { notifyError, useErrorToast } from '../../application/common/UseFeedback';
import { catalogueLanguages } from '../../domain/catalogue/CatalogueLanguages';
import { chosenImages, pageProducts, visiblePrices } from '../../domain/public/PublishedCatalogue';
import type { PriceTier } from '../../domain/catalogue/CatalogueModels';
import CatalogueSelect from '../common/CatalogueSelect.vue';
import ContentSkeleton from '../common/ContentSkeleton.vue';

defineOptions({ name: 'CatalogueEditionPage' });
const route = useRoute(),
  router = useRouter(),
  reader = usePublishedCatalogue();
const { t } = useUiLocale();
const edition = reader.edition,
  loading = reader.loading;
const view = ref<'web' | 'pdf'>('web');
const notice = ref('');
const saved = ref<string[]>([]);
const storageKey = 'ocharo.catalogue.saved-pieces';
try {
  const stored: unknown = JSON.parse(localStorage.getItem(storageKey) ?? '[]');
  if (Array.isArray(stored)) saved.value = stored.filter((id): id is string => typeof id === 'string').slice(0, 500);
} catch {
  /* Saving stays available for this tab. */
}
const contentLocale = computed(() => (typeof route.query.locale === 'string' ? route.query.locale : 'uz'));
const releaseId = computed(() => (typeof route.query.release === 'string' ? route.query.release : undefined));
const direction = computed(
  () => catalogueLanguages.find((language) => language.code === edition.value?.locale)?.dir ?? 'ltr',
);
const languageOptions = computed(() =>
  catalogueLanguages
    .filter((language) => edition.value?.availableLocales.includes(language.code))
    .map((language) => ({ value: language.code, label: language.label })),
);
const translatedError = computed(() => (reader.error.value ? t(reader.error.value) : null));
useErrorToast(translatedError);
async function reload() {
  notice.value = '';
  const hadEdition = !!edition.value;
  await reader.load(String(route.params.slug ?? ''), contentLocale.value, releaseId.value);
  if (!hadEdition) view.value = edition.value?.web ? 'web' : 'pdf';
}
const { refresh, refreshing } = useRefresh(reload);
const resultBusy = computed(() => loading.value || refreshing.value);
watch(() => [route.params.slug, contentLocale.value, releaseId.value], reload, { immediate: true });
function changeLanguage(locale: string) {
  if (!languageOptions.value.some((option) => option.value === locale)) return;
  const query: LocationQueryRaw = { ...route.query, locale };
  delete query.release;
  void router.push({ path: route.path, query });
}
function amount(value: number) {
  return new Intl.NumberFormat(edition.value?.locale ?? 'uz', { maximumFractionDigits: 0 }).format(value);
}
function quantity(tier: PriceTier) {
  return `${amount(tier.minimumQuantity)}${tier.maximumQuantity === null ? '+' : tier.maximumQuantity === tier.minimumQuantity ? '' : '–' + amount(tier.maximumQuantity)}`;
}
function toggleSaved(id: string) {
  saved.value = saved.value.includes(id)
    ? saved.value.filter((value) => value !== id)
    : [...saved.value, id].slice(-500);
  try {
    localStorage.setItem(storageKey, JSON.stringify(saved.value));
  } catch {
    notifyError(t('Your saved pieces are available in this tab only.'));
  }
}
async function share(section?: string) {
  if (!edition.value) return;
  const url = new URL(window.location.href);
  url.searchParams.set('locale', edition.value.locale);
  url.searchParams.set('release', edition.value.id);
  url.hash = section ?? '';
  try {
    await navigator.clipboard.writeText(url.toString());
    notice.value = t('Link copied');
  } catch {
    notifyError(t('Could not copy this link.'));
  }
}
</script>

<template>
  <section>
    <div class="no-print flex items-center justify-between gap-4 mb-5">
      <p class="text-xs uppercase tracking-[.2em] text-muted-foreground">Ocharo</p>
      <RefreshButton :busy="resultBusy" @refresh="refresh" />
    </div>
    <div class="catalogue-result-region">
      <ContentSkeleton v-if="resultBusy && !edition" :label="t('Loading catalogue')" :cards="3" variant="product" />
      <div v-else-if="!edition" class="catalogue-unavailable">
        <ResultEmpty
          :label="
            reader.notFound.value ? t('This catalogue is not published in this language.') : t('Content unavailable')
          "
        />
      </div>
      <SkeletonStateGroup v-else :is-loading="resultBusy" :label="t('Loading catalogue')">
        <section class="published-catalogue" :lang="edition.locale" :dir="direction">
          <div class="reader-tools no-print">
            <div class="reader-language">
              <label class="label" for="catalogue-language">{{ t('Catalogue language') }}</label
              ><CatalogueSelect
                id="catalogue-language"
                :model-value="edition.locale"
                :options="languageOptions"
                :label="t('Catalogue language')"
                @update:model-value="changeLanguage"
              />
            </div>
            <div class="reader-actions">
              <div v-if="edition.web && edition.pdf" class="reader-view" role="group" :aria-label="t('Catalogue view')">
                <button type="button" :aria-pressed="view === 'web'" @click="view = 'web'">
                  {{ t('Read online') }}</button
                ><button type="button" :aria-pressed="view === 'pdf'" @click="view = 'pdf'">
                  {{ t('PDF pages') }}
                </button>
              </div>
              <a
                v-if="edition.pdf"
                :href="edition.pdf.pdfUrl"
                :download="`ocharo-${edition.slug}-${edition.locale}.pdf`"
                class="btn-primary"
                ><Icon :icon="ArrowDownToLine" :size="17" />{{ t('Download catalogue PDF') }}</a
              >
              <button type="button" class="btn-outline" @click="share()">
                <Icon :icon="Share2" :size="17" />{{ t('Share catalogue') }}
              </button>
            </div>
            <p role="status" class="reader-notice">{{ notice }}</p>
          </div>
          <article v-if="view === 'web' && edition.web" class="web-reader" :class="`theme-${edition.web.theme}`">
            <header class="collection-intro">
              <p class="collection-kicker">
                {{ edition.web.copy.header || 'Ocharo'
                }}<span v-if="edition.web.copy.collection"> · {{ edition.web.copy.collection }}</span>
              </p>
              <h1>{{ edition.title }}</h1>
              <p class="collection-description">{{ edition.description }}</p>
              <p v-if="edition.web.copy.coverNote" class="cover-note">{{ edition.web.copy.coverNote }}</p>
              <nav v-if="edition.web.pages.length > 1" :aria-label="t('Collection sections')" class="section-links">
                <a v-for="page in edition.web.pages" :key="page.id" :href="`#section-${page.id}`">{{ page.title }}</a>
              </nav>
            </header>
            <ResultEmpty v-if="!edition.web.pages.length" :label="t('No products found.')" />
            <section
              v-for="page in edition.web.pages"
              :id="`section-${page.id}`"
              :key="page.id"
              class="collection-page"
              :class="page.layout"
            >
              <div class="collection-section-heading">
                <h2>{{ page.title }}</h2>
                <button type="button" class="section-share no-print" @click="share(`section-${page.id}`)">
                  <Icon :icon="Share2" :size="16" /><span>{{ t('Copy page link') }}</span>
                </button>
              </div>
              <ResultEmpty v-if="!pageProducts(edition, page).length" :label="t('No products found.')" />
              <div v-else class="products">
                <article
                  v-for="product in pageProducts(edition, page)"
                  :id="`product-${page.id}-${product.id}`"
                  :key="product.id"
                  class="product"
                >
                  <SkeletonStateSlot class="catalogue-value" :is-block="true"
                    ><div class="photographs">
                      <figure v-for="image in chosenImages(product, edition.web, page.layout)" :key="image.sha256">
                        <img :src="image.url" :alt="image.alt || product.name || ''" loading="lazy" />
                      </figure>
                      <div
                        v-if="!chosenImages(product, edition.web, page.layout).length"
                        class="image-empty"
                        role="img"
                        :aria-label="product.name ?? undefined"
                      />
                    </div>
                  </SkeletonStateSlot>
                  <div class="product-copy">
                    <h3>{{ product.name }}</h3>
                    <p v-if="page.layout !== 'grid' && product.description" class="description">
                      {{ product.description }}
                    </p>
                    <SkeletonStateSlot class="catalogue-value" :is-block="true"
                      ><dl>
                        <div v-if="product.fabric">
                          <dt>{{ edition.web.copy.materials }}</dt>
                          <dd>{{ product.fabric }}</dd>
                        </div>
                        <div v-if="product.decoration">
                          <dt>{{ edition.web.copy.decoration }}</dt>
                          <dd>{{ product.decoration }}</dd>
                        </div>
                        <div v-if="product.sizes.length">
                          <dt>{{ edition.web.copy.sizes }}</dt>
                          <dd>{{ product.sizes.join(', ') }}</dd>
                        </div>
                      </dl>
                      <div v-if="edition.web.priceDisplay.show" class="prices">
                        <p v-for="tier in visiblePrices(product, edition.web)" :key="tier.minimumQuantity">
                          <b
                            >{{ amount(tier.unitPrice)
                            }}<template v-if="edition.web.priceDisplay.showCurrency"> UZS</template></b
                          ><span v-if="edition.web.priceDisplay.showQuantity">
                            · {{ quantity(tier) }} {{ edition.web.copy.pieces }}</span
                          >
                        </p>
                        <p v-if="!product.pricing && edition.web.copy.priceOnRequest">
                          {{ edition.web.copy.priceOnRequest }}
                        </p>
                      </div> </SkeletonStateSlot
                    ><button
                      type="button"
                      class="save-piece no-print"
                      :aria-pressed="saved.includes(product.id)"
                      @click="toggleSaved(product.id)"
                    >
                      <Icon :icon="saved.includes(product.id) ? Check : Bookmark" :size="17" />{{
                        t(saved.includes(product.id) ? 'Saved on this device' : 'Save piece')
                      }}
                    </button>
                  </div>
                </article>
              </div>
            </section>
            <footer v-if="edition.web.copy.footer" class="collection-footer">{{ edition.web.copy.footer }}</footer>
          </article>
          <article v-else-if="edition.pdf" class="pdf-reader">
            <header>
              <p class="text-xs uppercase tracking-[.2em] text-muted-foreground">Ocharo</p>
              <h1 class="mt-3 font-editorial text-5xl">{{ edition.title }}</h1>
              <p class="mt-4 max-w-xl text-muted-foreground">{{ edition.description }}</p>
            </header>
            <div class="pdf-grid">
              <figure v-for="page in edition.pdf.pages" :key="page.id">
                <SkeletonStateSlot class="catalogue-value" :is-block="true"
                  ><a :href="page.pdfUrl" :download="`ocharo-${edition.slug}-${page.number}.pdf`"
                    ><img
                      :src="page.imageUrl"
                      :alt="page.title"
                      :style="{ aspectRatio: `${page.widthMm}/${page.heightMm}` }"
                      loading="lazy" /></a
                ></SkeletonStateSlot>
                <figcaption>
                  <span>{{ page.number }} · {{ page.title }}</span
                  ><a :href="page.pdfUrl" :download="`ocharo-${edition.slug}-${page.number}.pdf`" class="btn-outline"
                    ><Icon :icon="ArrowDownToLine" :size="16" />{{ t('Download page PDF') }}</a
                  >
                </figcaption>
              </figure>
            </div>
          </article>
        </section>
      </SkeletonStateGroup>
    </div>
  </section>
</template>

<style scoped>
.reader-tools {
  display: flex;
  align-items: end;
  flex-wrap: wrap;
  gap: 16px 24px;
  margin-bottom: 24px;
}
.reader-tools :deep(button),
.reader-tools a,
.pdf-grid a.btn-outline {
  min-height: 44px;
}
.reader-language {
  width: 220px;
  max-width: 100%;
}
.reader-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px;
  margin-inline-start: auto;
}
.reader-view {
  display: flex;
  padding: 4px;
  border: 1px solid var(--color-border);
  border-radius: 12px;
  background: var(--color-card);
}
.reader-view button {
  min-height: 44px;
  padding: 8px 14px;
  border-radius: 8px;
}
.reader-view button[aria-pressed='true'] {
  background: var(--color-primary);
  color: var(--color-primary-foreground);
}
.reader-notice {
  width: 100%;
  min-height: 1rem;
  font-size: 12px;
}
.web-reader {
  container-type: inline-size;
  overflow-wrap: anywhere;
  background: #f6f0e5;
  color: #302b25;
  --catalogue-line: #d1c3af;
  --catalogue-muted: #847562;
  font:
    16px/1.7 'DM Sans',
    system-ui,
    sans-serif;
}
.web-reader * {
  box-sizing: border-box;
}
.collection-intro,
.collection-page,
.collection-footer {
  padding: 65px 6%;
  margin: 0 auto;
}
.collection-kicker {
  font-size: 12px;
  text-transform: uppercase;
  letter-spacing: 0.12em;
}
.collection-intro h1,
.collection-page h2,
.product h3 {
  font-family: Georgia, 'Times New Roman', serif;
  font-weight: 400;
  letter-spacing: -0.05em;
  line-height: 1.06;
  margin: 0;
}
.collection-intro h1 {
  font-size: clamp(48px, 6.8cqw, 100px);
  margin-top: 24px;
}
.collection-description {
  max-width: 650px;
  font-size: 18px;
  color: var(--catalogue-muted);
  margin: 24px 0;
}
.cover-note {
  max-width: 650px;
  margin-top: 20px;
}
.section-links {
  display: flex;
  flex-wrap: wrap;
  gap: 12px 24px;
  border-top: 1px solid var(--catalogue-line);
  padding-top: 20px;
  margin-top: 40px;
}
.section-links a {
  display: inline-flex;
  align-items: center;
  min-height: 44px;
}
.collection-page {
  border-top: 1px solid var(--catalogue-line);
}
.collection-section-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
  margin-bottom: 35px;
}
.collection-page h2 {
  font-size: clamp(34px, 4cqw, 55px);
}
.section-share,
.save-piece {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 9px;
  min-height: 44px;
  font: inherit;
  font-size: 13px;
}
.section-share {
  flex-shrink: 0;
}
.products {
  display: grid;
  grid-template-columns: 1fr;
  gap: 38px;
}
.product {
  display: grid;
  gap: 28px;
  min-width: 0;
}
.product + .product {
  border-top: 1px solid var(--catalogue-line);
  padding-top: 38px;
}
.photographs {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
  align-content: start;
  min-width: 0;
}
.photographs figure {
  margin: 0;
  min-width: 0;
}
.photographs figure:first-child {
  grid-column: 1/-1;
}
.photographs img {
  display: block;
  width: 100%;
  aspect-ratio: 3/4;
  max-height: 700px;
  object-fit: contain;
}
.image-empty {
  grid-column: 1/-1;
  min-height: 200px;
  display: grid;
  place-items: center;
  border: 1px solid var(--catalogue-line);
  color: var(--catalogue-muted);
  padding: 20px;
  text-align: center;
}
.collage .photographs figure:first-child {
  grid-column: auto;
}
.collage .photographs figure:nth-child(3n) {
  grid-column: 1/-1;
}
.product-copy {
  min-width: 0;
  align-self: center;
}
.product h3 {
  font-size: clamp(36px, 5cqw, 64px);
}
.description {
  color: var(--catalogue-muted);
  font-size: 18px;
  margin: 20px 0 28px;
}
.prices {
  font-size: 22px;
}
.prices p {
  margin: 12px 0;
}
.product-copy dl {
  display: grid;
  grid-template-columns: 1fr 1fr;
  border-top: 1px solid var(--catalogue-line);
  margin: 25px 0;
}
.product-copy dl > div {
  padding: 16px 15px 16px 0;
  border-bottom: 1px solid var(--catalogue-line);
}
dt {
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.09em;
  color: var(--catalogue-muted);
}
dd {
  margin: 8px 0 0;
  font-size: 13px;
}
.save-piece {
  border: 1px solid var(--catalogue-line);
  padding: 8px 16px;
  margin-top: 24px;
}
.save-piece[aria-pressed='true'] {
  background: #302b25;
  color: #f6f0e5;
}
.collection-page.grid .products {
  gap: 40px 30px;
}
.collection-page.grid .product {
  display: block;
  border: 0;
  padding: 0;
}
.collection-page.grid .product-copy {
  padding-top: 24px;
}
.collection-page.grid h3 {
  font-size: 30px;
}
.collection-page.grid .prices {
  font-size: 16px;
}
.collection-page.grid .photographs {
  display: block;
}
.collection-footer {
  border-top: 1px solid var(--catalogue-line);
  padding-top: 28px;
  padding-bottom: 28px;
}
.pdf-reader {
  padding: 24px 0;
}
.pdf-grid {
  display: grid;
  gap: 32px;
  margin-top: 40px;
}
.pdf-grid img {
  width: 100%;
  object-fit: contain;
  border: 1px solid var(--color-border);
  background: white;
}
.pdf-grid figcaption {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-top: 16px;
  font-size: 14px;
}
button:focus-visible,
a:focus-visible {
  outline: 3px solid currentColor;
  outline-offset: 4px;
}
@container (min-width:768px) {
  .product {
    grid-template-columns: minmax(0, 1.15fr) minmax(0, 1fr);
    gap: 32px;
  }
  .product h3 {
    font-size: 42px;
  }
  .collection-page.grid .products {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
@container (min-width:1024px) {
  .product {
    grid-template-columns: minmax(0, 1.5fr) minmax(280px, 1fr);
    gap: 6cqw;
  }
  .photographs figure:first-child {
    grid-row: span 2;
    grid-column: 1;
  }
  .photographs figure:first-child img {
    height: 100%;
  }
  .photographs figure:not(:first-child) img {
    max-height: 360px;
    aspect-ratio: 4/3;
  }
  .collage .photographs figure:first-child {
    grid-row: auto;
  }
  .collage .photographs figure:nth-child(3n) {
    grid-column: auto;
  }
  .collage .photographs img {
    aspect-ratio: 3/4;
    max-height: 450px;
    height: auto;
  }
  .product h3 {
    font-size: clamp(44px, 4.5cqw, 64px);
  }
  .collection-page.grid .products {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
  .theme-grid .product {
    grid-template-columns: 1fr 1fr;
    gap: 45px;
  }
  .theme-grid .collection-page {
    padding-top: 45px;
    padding-bottom: 45px;
  }
}
@media (min-width: 768px) {
  .pdf-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
@media (min-width: 1200px) {
  .pdf-grid {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}
@media (max-width: 767px) {
  .reader-actions {
    margin-inline-start: 0;
    width: 100%;
  }
  .reader-actions > * {
    flex: 1;
  }
  .collection-intro,
  .collection-page,
  .collection-footer {
    padding: 40px 5%;
  }
  .collection-section-heading {
    align-items: start;
    flex-direction: column;
  }
  .section-share {
    justify-content: start;
  }
  .collection-intro h1 {
    font-size: 48px;
  }
}
</style>
