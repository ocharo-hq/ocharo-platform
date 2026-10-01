<script setup lang="ts">
import ContentSkeleton from '../common/ContentSkeleton.vue';
import { computed, onMounted, ref, watch } from 'vue';
import { Printer } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/ui-vue/foundation/icons';
import { useBrandKit, useCatalogueStore, usePublicCatalogue } from '../../application/catalogue';
import type { LocaleCode, PublicProduct } from '../../domain/catalogue/CatalogueModels';
import { formatUzs, localeLabels, locales } from '../../domain/catalogue/CatalogueRules';

/** Print-ready lookbook / price sheet built from the public feed (what other platforms see). */
defineOptions({ name: 'PrintPage' });
const store = useCatalogueStore();
const feed = usePublicCatalogue();
const brand = useBrandKit();
const contactLines = computed(() => brand.contactLines(store.settings.value?.contactLines ?? []));
const locale = ref<LocaleCode>('uz');
const collection = ref('');
const showPrices = ref(true);
const mode = ref<'lookbook' | 'order'>('lookbook');
const orderSizes = computed(() => {
  const all = new Set<string>();
  for (const p of products.value) for (const size of p.sizes) all.add(size);
  const order = ['XS', 'S', 'M', 'L', 'XL', 'XXL', 'XXXL'];
  return [...all].sort((a, b) => (order.indexOf(a) + 1 || 99) - (order.indexOf(b) + 1 || 99) || a.localeCompare(b));
});
const labels = {
  uz: {
    title: 'Katalog',
    sizes: 'O‘lchamlar',
    piece: '1 dona',
    bulk: 'dan ortiq',
    from: 'dan',
    order: 'Buyurtma varag‘i',
    product: 'Mahsulot',
    wholesale: 'Ulgurji narx',
    qty: 'Soni',
    total: 'Jami',
    customer: 'Buyurtmachi',
    phone: 'Telefon',
    date: 'Sana',
    note: 'Narxlar bir dona uchun; ulgurji narx ko‘rsatilgan miqdordan boshlab.',
  },
  ru: {
    title: 'Каталог',
    sizes: 'Размеры',
    piece: '1 шт.',
    bulk: 'и более',
    from: 'от',
    order: 'Бланк заказа',
    product: 'Изделие',
    wholesale: 'Оптовая цена',
    qty: 'Кол-во',
    total: 'Итого',
    customer: 'Заказчик',
    phone: 'Телефон',
    date: 'Дата',
    note: 'Цены за единицу; оптовая цена — от указанного количества.',
  },
  en: {
    title: 'Catalogue',
    sizes: 'Sizes',
    piece: '1 piece',
    bulk: 'and more',
    from: 'from',
    order: 'Order sheet',
    product: 'Product',
    wholesale: 'Wholesale price',
    qty: 'Qty',
    total: 'Total',
    customer: 'Customer',
    phone: 'Phone',
    date: 'Date',
    note: 'Prices per piece; the wholesale price applies from the stated quantity.',
  },
} as const;
const text = computed(() => labels[locale.value]);
const products = computed<PublicProduct[]>(() => {
  const all = feed.catalogue.value?.products ?? [];
  if (!collection.value) return all;
  const ids = feed.catalogue.value?.collections.find((c) => c.id === collection.value)?.productIds ?? [];
  return ids.map((id) => all.find((p) => p.id === id)).filter((p): p is PublicProduct => p !== undefined);
});
function price(value: number | null) {
  if (value === null) return '—';
  const formatted = formatUzs(value);
  return locale.value === 'uz' ? formatted : formatted.replace('so‘m', locale.value === 'ru' ? 'сум' : 'UZS');
}
/** A size cell is open when offered and not known to be sold out. */
function sizeOpen(p: PublicProduct, size: string) {
  if (!p.sizes.includes(size)) return false;
  return p.availability === 'unknown' || p.availability === 'made-to-order' || (p.availableSizes ?? []).includes(size);
}
function printSheet() {
  window.print();
}
watch(locale, (value) => feed.load(value));
onMounted(() => feed.load(locale.value));
</script>

<template>
  <section>
    <div class="no-print flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 class="font-editorial text-4xl tracking-tight">Katalog varag‘i</h1>
        <p class="mt-2 text-sm text-muted-foreground">
          Faqat “Katalogda” holatidagi mahsulotlar. Chop etish oynasida “PDF sifatida saqlash”ni tanlang.
        </p>
      </div>
      <div class="flex flex-wrap items-center gap-3">
        <select v-model="locale" class="field w-36" aria-label="Til">
          <option v-for="code in locales" :key="code" :value="code">{{ localeLabels[code] }}</option>
        </select>
        <select v-model="collection" class="field w-48" aria-label="To‘plam">
          <option value="">Barcha mahsulotlar</option>
          <option v-for="c in feed.catalogue.value?.collections ?? []" :key="c.id" :value="c.id">{{ c.name }}</option>
        </select>
        <div role="tablist" class="flex rounded-lg bg-muted p-1 text-sm">
          <button
            role="tab"
            :aria-selected="mode === 'lookbook'"
            :class="[
              'rounded-md px-3 py-1.5',
              mode === 'lookbook' ? 'bg-card font-semibold shadow-sm' : 'text-muted-foreground',
            ]"
            @click="mode = 'lookbook'"
          >
            Lookbook
          </button>
          <button
            role="tab"
            :aria-selected="mode === 'order'"
            :class="[
              'rounded-md px-3 py-1.5',
              mode === 'order' ? 'bg-card font-semibold shadow-sm' : 'text-muted-foreground',
            ]"
            @click="mode = 'order'"
          >
            Buyurtma varag‘i
          </button>
        </div>
        <label v-if="mode === 'lookbook'" class="flex items-center gap-2 text-sm"
          ><input v-model="showPrices" type="checkbox" />Narxlar</label
        >
        <button class="btn-primary" @click="printSheet"><Icon :icon="Printer" :size="15" />Chop etish</button>
      </div>
    </div>

    <article
      v-if="mode === 'order'"
      class="mx-auto mt-6 max-w-[1100px] bg-card p-8 shadow-[var(--shadow-paper)] print:mt-0 print:max-w-none print:p-0 print:shadow-none"
    >
      <header class="flex items-end justify-between border-b border-foreground/80 pb-4">
        <div>
          <img
            v-if="brand.logoUrl.value"
            :src="brand.logoUrl.value"
            :alt="feed.catalogue.value?.brand ?? 'Ocharo'"
            class="h-12 w-[130px] object-cover"
          />
          <p v-else class="font-editorial text-4xl tracking-tight">{{ feed.catalogue.value?.brand ?? 'Ocharo' }}</p>
          <p class="mt-1 text-xs uppercase tracking-[0.3em] text-muted-foreground">{{ text.order }}</p>
        </div>
        <div class="text-right text-xs leading-5 text-muted-foreground">
          <p v-for="line in contactLines" :key="line">{{ line }}</p>
        </div>
      </header>
      <div class="mt-4 grid grid-cols-3 gap-4 text-xs">
        <p class="border-b border-border pb-5">{{ text.customer }}:</p>
        <p class="border-b border-border pb-5">{{ text.phone }}:</p>
        <p class="border-b border-border pb-5">{{ text.date }}:</p>
      </div>
      <div class="mt-4 overflow-x-auto print:overflow-visible">
        <table class="w-full min-w-[760px] border-collapse text-xs">
          <thead>
            <tr class="border-b border-foreground/60 text-left">
              <th class="py-2 pr-2 font-semibold">{{ text.product }}</th>
              <th class="py-2 pr-2 text-right font-semibold">{{ text.piece }}</th>
              <th class="py-2 pr-2 text-right font-semibold">{{ text.wholesale }}</th>
              <th v-for="size in orderSizes" :key="size" class="w-12 py-2 text-center font-semibold">{{ size }}</th>
              <th class="w-16 py-2 text-center font-semibold">{{ text.total }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="p in products" :key="p.id" class="break-inside-avoid border-b border-border">
              <td class="py-1.5 pr-2">
                <div class="flex items-center gap-2">
                  <img
                    v-if="p.images[0]"
                    :src="p.images[0].previewUrl ?? p.images[0].url"
                    alt=""
                    class="h-10 w-8 object-contain"
                  />
                  <span
                    ><span class="font-semibold">{{ p.name }}</span>
                    <span class="text-muted-foreground">{{ p.id }}</span></span
                  >
                </div>
              </td>
              <td class="py-1.5 pr-2 text-right">{{ price(p.pricing?.retail ?? null) }}</td>
              <td class="py-1.5 pr-2 text-right">
                <template v-if="p.pricing && p.pricing.tiers.length > 1">
                  {{ price(p.pricing.wholesale) }}
                  <span class="block text-[10px] text-muted-foreground"
                    >{{ p.pricing.tiers[p.pricing.tiers.length - 1]!.minimumQuantity }}+</span
                  >
                </template>
                <template v-else>—</template>
              </td>
              <td
                v-for="size in orderSizes"
                :key="size"
                :class="['border-l border-border text-center', sizeOpen(p, size) ? '' : 'bg-muted/60']"
              ></td>
              <td class="border-l border-border"></td>
            </tr>
          </tbody>
        </table>
      </div>
      <p class="mt-4 text-[11px] text-muted-foreground">{{ text.note }} {{ feed.catalogue.value?.priceNote }}</p>
    </article>

    <article
      v-else
      class="mx-auto mt-6 max-w-[1100px] bg-card p-10 shadow-[var(--shadow-paper)] print:mt-0 print:max-w-none print:p-0 print:shadow-none"
    >
      <header class="flex items-end justify-between border-b border-foreground/80 pb-5">
        <div>
          <img
            v-if="brand.logoUrl.value"
            :src="brand.logoUrl.value"
            :alt="feed.catalogue.value?.brand ?? 'Ocharo'"
            class="h-16 w-[173px] object-cover"
          />
          <p v-else class="font-editorial text-5xl tracking-tight">{{ feed.catalogue.value?.brand ?? 'Ocharo' }}</p>
          <p class="mt-2 text-xs uppercase tracking-[0.3em] text-muted-foreground">
            {{ text.title
            }}<span v-if="collection">
              · {{ feed.catalogue.value?.collections.find((c) => c.id === collection)?.name }}</span
            >
          </p>
        </div>
        <div class="text-right text-xs leading-5 text-muted-foreground">
          <p v-for="line in contactLines" :key="line">{{ line }}</p>
        </div>
      </header>
      <ContentSkeleton v-if="feed.loading.value" />
      <div class="mt-8 grid grid-cols-2 gap-x-8 gap-y-10 md:grid-cols-3 print:grid-cols-3">
        <figure v-for="p in products" :key="p.id" class="break-inside-avoid">
          <div class="flex aspect-[3/4] items-center justify-center bg-[#f3efe6]">
            <img
              v-if="p.images[0]"
              :src="p.images[0].previewUrl ?? p.images[0].url"
              :alt="p.images[0].alt ?? p.name ?? ''"
              class="h-full w-full object-contain p-3"
            />
          </div>
          <figcaption class="mt-3">
            <p class="font-editorial text-xl leading-tight">{{ p.name }}</p>
            <p class="text-[11px] uppercase tracking-[0.18em] text-muted-foreground">
              {{ p.garmentName }} · {{ p.variantCode }}
            </p>
            <p v-if="p.fabric" class="mt-1 text-xs text-muted-foreground">{{ p.fabric }}</p>
            <p v-if="p.sizes.length" class="mt-1 text-xs">{{ text.sizes }}: {{ p.sizes.join(' · ') }}</p>
            <div v-if="showPrices && p.pricing" class="mt-2 text-sm">
              <p v-for="t in p.pricing.tiers" :key="t.minimumQuantity">
                <span class="text-muted-foreground">
                  {{
                    t.minimumQuantity === 1 && t.maximumQuantity === 1
                      ? text.piece
                      : t.maximumQuantity === null
                        ? `${t.minimumQuantity} ${text.bulk}`
                        : `${t.minimumQuantity}–${t.maximumQuantity}`
                  }}:
                </span>
                <span class="font-semibold">{{ price(t.unitPrice) }}</span>
              </p>
            </div>
          </figcaption>
        </figure>
      </div>
      <footer
        v-if="showPrices && feed.catalogue.value?.priceNote"
        class="mt-10 border-t border-border pt-4 text-xs text-muted-foreground"
      >
        {{ feed.catalogue.value.priceNote }}
      </footer>
    </article>
  </section>
</template>
