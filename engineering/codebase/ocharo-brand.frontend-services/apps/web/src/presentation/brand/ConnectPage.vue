<script setup lang="ts">
import { computed } from 'vue';
import { Card } from '@wow-two-beta/fe/presentation/display';
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
      <h1 class="sr-only">Connect</h1>
      <p class="max-w-3xl text-sm text-muted-foreground">
        Apps get the logo from the stable addresses below instead of copying the file. When a release or an event
        changes, the address stays the same and the file is swapped (5-minute cache). To link to one exact file, use the
        immutable media address.
      </p>
    </div>
    <Card v-bind="{ role: 'region', 'aria-labelledby': 'connect-urls-heading' }" radius="2xl" padding="xl">
      <h2 id="connect-urls-heading" class="font-editorial text-2xl">Stable logo addresses</h2>
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
    </Card>
    <Card
      v-bind="{ role: 'region', 'aria-labelledby': 'connect-kit-heading' }"
      radius="2xl"
      padding="xl"
      class="space-y-3 text-sm"
    >
      <h2 id="connect-kit-heading" class="font-editorial text-2xl">Data and style</h2>
      <p>
        <span class="font-semibold">JSON kit:</span>
        <code class="break-all text-xs">{{ origin }}{{ brand.urls.kit }}?locale=en</code> (uz and ru too)
      </p>
      <p>
        <span class="font-semibold">Exact release:</span>
        <code class="break-all text-xs"
          >{{ origin }}/api/public/brand/releases/{{ brand.kit.value?.release ?? 1 }}</code
        >
      </p>
      <p>
        <span class="font-semibold">CSS tokens:</span>
        <code class="break-all text-xs">{{ origin }}{{ brand.urls.tokens }}</code>
      </p>
      <p class="text-muted-foreground">
        For an app to appear on the Overview page, it sends the <code>X-Ocharo-App: name</code> header or
        <code>?app=name</code>.
      </p>
    </Card>
    <Card
      v-bind="{ role: 'region', 'aria-labelledby': 'connect-code-heading' }"
      radius="2xl"
      padding="xl"
      class="space-y-3"
    >
      <h2 id="connect-code-heading" class="font-editorial text-2xl">Sample code</h2>
      <pre class="overflow-x-auto rounded-lg bg-muted p-3 text-xs"><code>{{ snippet }}</code></pre>
      <pre class="overflow-x-auto rounded-lg bg-muted p-3 text-xs"><code>{{ cssSnippet }}</code></pre>
    </Card>
  </section>
</template>
