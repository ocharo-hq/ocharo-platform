<script setup lang="ts">
import { computed } from 'vue';
import { useBrand } from '../../application/brand';
import { slotLabels, slots } from '../../domain/brand/BrandModels';

/** How any app (or a person) uses the brand: stable logo URLs, the JSON kit and CSS tokens. */
defineOptions({ name: 'ConnectPage' });
const brand = useBrand();
const origin = computed(() => window.location.origin);
const snippet = computed(
  () =>
    `<img src="${origin.value}${brand.urls.logo('on-light')}?app=my-app" alt="${brand.kit.value?.name ?? 'Ocharo'}" height="48">`,
);
const cssSnippet = computed(() => `<link rel="stylesheet" href="${origin.value}${brand.urls.tokens}">`);
</script>

<template>
  <section class="space-y-6">
    <div>
      <h1 class="font-editorial text-4xl tracking-tight">Ulanish</h1>
      <p class="mt-2 max-w-3xl text-sm text-muted-foreground">
        Ilovalar logotipni fayl sifatida nusxalamasdan, quyidagi barqaror manzillardan oladi. Nashr yoki tadbir
        o‘zgarganda manzil o‘sha qoladi, fayl almashadi (5 daqiqalik kesh). Aniq bir faylga bog‘lanish kerak bo‘lsa,
        o‘zgarmas media manzilidan foydalaning.
      </p>
    </div>
    <section class="panel">
      <h2 class="font-editorial text-2xl">Barqaror logotip manzillari</h2>
      <table class="mt-3 w-full text-sm">
        <tbody>
          <tr v-for="slot in slots" :key="slot" class="border-t border-border">
            <td class="py-2 pr-3 font-semibold">{{ slotLabels[slot]!.label }}</td>
            <td class="py-2">
              <code class="break-all text-xs">{{ origin }}{{ brand.urls.logo(slot) }}</code>
            </td>
          </tr>
        </tbody>
      </table>
    </section>
    <section class="panel space-y-3 text-sm">
      <h2 class="font-editorial text-2xl">Ma’lumot va uslub</h2>
      <p>
        <span class="font-semibold">JSON to‘plam:</span>
        <code class="break-all text-xs">{{ origin }}{{ brand.urls.kit }}?locale=uz</code> (ru, en ham)
      </p>
      <p>
        <span class="font-semibold">Aniq nashr:</span>
        <code class="break-all text-xs"
          >{{ origin }}/api/public/brand/releases/{{ brand.kit.value?.release ?? 1 }}</code
        >
      </p>
      <p>
        <span class="font-semibold">CSS tokenlar:</span>
        <code class="break-all text-xs">{{ origin }}{{ brand.urls.tokens }}</code>
      </p>
      <p class="text-muted-foreground">
        Ilova o‘zini “Umumiy” sahifasida ko‘rsatishi uchun <code>X-Ocharo-App: nomi</code> sarlavhasini yoki
        <code>?app=nomi</code> ni yuboradi.
      </p>
    </section>
    <section class="panel space-y-3">
      <h2 class="font-editorial text-2xl">Namuna kod</h2>
      <pre class="overflow-x-auto rounded-lg bg-muted p-3 text-xs"><code>{{ snippet }}</code></pre>
      <pre class="overflow-x-auto rounded-lg bg-muted p-3 text-xs"><code>{{ cssSnippet }}</code></pre>
    </section>
  </section>
</template>
