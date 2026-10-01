<script setup lang="ts">
import { computed } from 'vue';
import { catalogueLanguages, productLocale } from '../../domain/catalogue/CatalogueLanguages';
import type { ProductDetail } from '../../domain/catalogue/CatalogueModels';
import type { CatalogueWebPage, DesignSnapshot } from '../../domain/design/EditorModels';
import {
  catalogueCopy,
  catalogueSettings,
  productEligible,
  productMedia,
  mediaUrl,
  amount,
  publicPricing,
} from '../../domain/design/CatalogueDesign';
defineOptions({ name: 'CatalogueWebPreview' });
const props = defineProps<{
  snapshot: DesignSnapshot;
  products: ProductDetail[];
  title: string;
  brandName?: string;
  pageId?: string;
}>();
const metadata = computed(() => props.snapshot.catalogue!),
  settings = computed(() => catalogueSettings(props.snapshot)),
  copy = computed(() => catalogueCopy(props.snapshot));
const direction = computed(() => catalogueLanguages.find((item) => item.code === props.snapshot.locale)?.dir ?? 'ltr');
function pageProducts(page: CatalogueWebPage) {
  return page.productIds
    .map((id) => props.products.find((product) => product.document.id === id))
    .filter((product): product is ProductDetail => !!product && productEligible(product, props.snapshot.locale));
}
const pages = computed(() =>
  metadata.value.web.pages.filter(
    (page) => page.isActive && (!props.pageId || page.id === props.pageId) && pageProducts(page).length > 0,
  ),
);
function images(product: ProductDetail, layout: CatalogueWebPage['layout']) {
  return productMedia(product, settings.value.mediaGroup).slice(
    0,
    layout === 'grid' ? 1 : layout === 'collage' ? 6 : 3,
  );
}
function productCopy(product: ProductDetail, locale: string) {
  return productLocale(product.document.locales, locale) ?? { name: '', fabric: '', description: '' };
}
function price(value: number, currency: string) {
  return amount(value, props.snapshot.locale) + ' ' + currency;
}
</script>
<template>
  <main class="web-catalogue" :class="`theme-${metadata.web.theme}`" :lang="snapshot.locale" :dir="direction">
    <header>
      <span class="brand">{{ copy.header || brandName || '' }}</span
      ><span>{{ copy.collection }}</span>
    </header>
    <div class="catalogue-intro">
      <h1>{{ title }}</h1>
      <p>{{ metadata.description }}</p>
      <p v-if="copy.coverNote">{{ copy.coverNote }}</p>
    </div>
    <section v-for="page in pages" :key="page.id" :class="['web-page', page.layout]">
      <h2>{{ page.title }}</h2>
      <div class="products">
        <article v-for="product in pageProducts(page)" :key="product.document.id">
          <div class="photographs">
            <div
              v-if="!images(product, page.layout).length"
              class="image-empty"
              role="img"
              :aria-label="productCopy(product, snapshot.locale).name || product.document.id"
            />
            <figure v-for="image in images(product, page.layout)" :key="image.sha256">
              <img
                :src="mediaUrl(image)"
                :alt="image.alt || productCopy(product, snapshot.locale).name || product.document.id"
                loading="lazy"
              />
            </figure>
          </div>
          <div class="product-copy">
            <h3>{{ productCopy(product, snapshot.locale).name }}</h3>
            <p v-if="page.layout !== 'grid'" class="description">
              {{ productCopy(product, snapshot.locale).description }}
            </p>
            <dl>
              <div>
                <dt>{{ copy.materials }}</dt>
                <dd>{{ productCopy(product, snapshot.locale).fabric }}</dd>
              </div>
              <div v-if="product.document.sizes.length">
                <dt>{{ copy.sizes }}</dt>
                <dd>{{ product.document.sizes.join(', ') }}</dd>
              </div>
            </dl>
            <div
              v-if="settings.priceDisplay.show && product.document.pricing && publicPricing(product.document.pricing)"
              class="prices"
            >
              <p
                v-for="(tier, index) in product.document.pricing.tiers.filter((tier) =>
                  tier.minimumQuantity === 1 ? settings.priceDisplay.showRetail : settings.priceDisplay.showWholesale,
                )"
                :key="index"
              >
                <b>{{
                  settings.priceDisplay.showCurrency
                    ? price(tier.unitPrice, product.document.pricing.currency)
                    : new Intl.NumberFormat(snapshot.locale, { maximumFractionDigits: 0 }).format(tier.unitPrice)
                }}</b
                ><span v-if="settings.priceDisplay.showQuantity">
                  · {{ tier.minimumQuantity
                  }}{{
                    tier.maximumQuantity === null
                      ? '+'
                      : tier.maximumQuantity !== tier.minimumQuantity
                        ? '–' + tier.maximumQuantity
                        : ''
                  }}
                  {{ copy.pieces }}</span
                >
              </p>
            </div>
            <p v-else-if="settings.priceDisplay.show" class="prices">{{ copy.priceOnRequest }}</p>
          </div>
        </article>
      </div>
    </section>
    <footer>{{ copy.footer }}</footer>
  </main>
</template>
<style scoped>
/* Matches the public named-catalogue layout, using container widths inside the editor. */
.web-catalogue {
  container-type: inline-size;
  background: #f6f0e5;
  color: #302b25;
  min-height: 600px;
  font:
    16px/1.7 'DM Sans',
    system-ui,
    sans-serif;
  overflow-wrap: anywhere;
  --line: #d1c3af;
  --muted: #847562;
}
.web-catalogue * {
  box-sizing: border-box;
}
.web-catalogue > header,
.web-catalogue > footer {
  padding: 28px 5%;
  display: flex;
  justify-content: space-between;
  border-bottom: 1px solid var(--line);
  gap: 20px;
}
.brand {
  font:
    31px Georgia,
    serif;
  letter-spacing: -0.055em;
}
.catalogue-intro,
.web-page {
  padding: 75px 6%;
  max-width: 1550px;
  margin: auto;
}
.catalogue-intro p {
  max-width: 540px;
  margin-top: 24px;
  color: var(--muted);
  font-size: 18px;
}
.web-page {
  border-top: 1px solid var(--line);
}
h1,
h2,
h3 {
  font-family: Georgia, 'Times New Roman', serif;
  font-weight: 400;
  letter-spacing: -0.05em;
  line-height: 1.06;
  margin: 0;
}
h1 {
  font-size: clamp(48px, 6.8cqw, 100px);
}
h2 {
  font-size: clamp(34px, 4cqw, 55px);
  margin-bottom: 35px;
}
h3 {
  font-size: clamp(36px, 5cqw, 64px);
}
.products {
  display: grid;
  grid-template-columns: 1fr;
  gap: 38px;
}
.products article {
  display: grid;
  gap: 28px;
  min-width: 0;
}
.products article + article {
  border-top: 1px solid var(--line);
  padding-top: 38px;
}
.photographs {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
  align-content: start;
  min-width: 0;
}
.image-empty {
  grid-column: 1/-1;
  min-height: 220px;
  border: 1px solid var(--line);
  display: grid;
  place-items: center;
  color: var(--muted);
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
.photographs figcaption {
  font-size: 11px;
  margin-top: 8px;
  color: var(--muted);
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
.product-copy p {
  margin: 20px 0 28px;
}
.description {
  color: var(--muted);
  font-size: 18px;
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
  border-top: 1px solid var(--line);
  margin: 25px 0;
}
.product-copy dl > div {
  padding: 16px 15px 16px 0;
  border-bottom: 1px solid var(--line);
}
dt {
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.09em;
  color: var(--muted);
}
dd {
  margin: 8px 0 0;
  font-size: 13px;
}
.web-page.grid .products {
  gap: 40px 30px;
}
.web-page.grid .products article {
  display: block;
  border: 0;
  padding: 0;
}
.web-page.grid .product-copy {
  padding-top: 24px;
}
.web-page.grid h3 {
  font-size: 30px;
}
.web-page.grid .prices {
  font-size: 16px;
}
.web-page.grid .photographs {
  display: block;
}
.web-page.grid .photographs img {
  aspect-ratio: 3/4;
}
.web-catalogue > footer {
  border-top: 1px solid var(--line);
  border-bottom: 0;
}
@container (min-width:768px) {
  .products article {
    grid-template-columns: minmax(0, 1.15fr) minmax(0, 1fr);
    gap: 32px;
  }
  h3 {
    font-size: 42px;
  }
  .web-page.grid .products {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
@container (min-width:1024px) {
  .products article {
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
  h3 {
    font-size: clamp(44px, 4.5cqw, 64px);
  }
  .web-page.grid .products {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
  .theme-grid.web-catalogue .products article {
    grid-template-columns: 1fr 1fr;
    gap: 45px;
  }
  .theme-grid.web-catalogue .web-page {
    padding-top: 45px;
    padding-bottom: 45px;
  }
}
@container (max-width:767px) {
  .catalogue-intro,
  .web-page {
    padding: 45px 5%;
  }
  .web-catalogue > header {
    flex-direction: column;
  }
}
</style>
