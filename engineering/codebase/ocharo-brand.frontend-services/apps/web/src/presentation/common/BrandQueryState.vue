<script setup lang="ts">
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import { slotLabels, slots } from '../../domain/brand/BrandModels';

/** First-read outcomes shaped like the selected brand route's actual content. */
defineOptions({ name: 'BrandQueryState' });
defineProps<{ state: 'loading' | 'success' | 'unavailable'; kind: 'overview' | 'logos' | 'appearances' | 'connect' }>();
const titles = {
  overview: 'Brend markazi',
  logos: 'Logotiplar',
  appearances: 'Tadbir ko‘rinishlari',
  connect: 'Ulanish',
} as const;
</script>

<template>
  <section class="space-y-6">
    <div>
      <h1 class="font-editorial text-4xl tracking-tight">{{ titles[kind] }}</h1>
      <SkeletonState v-if="state === 'loading'" class="mt-2 h-10 w-full max-w-3xl" />
    </div>
    <div v-if="state === 'loading'" aria-busy="true" class="space-y-6">
      <span class="sr-only" role="status">Brend ma’lumotlari yuklanmoqda.</span>
      <template v-if="kind === 'logos'">
        <section v-for="slot in slots" :key="slot" class="panel" :aria-labelledby="`loading-slot-${slot}`">
          <div class="flex flex-wrap items-start justify-between gap-4">
            <div>
              <h2 :id="`loading-slot-${slot}`" class="font-editorial text-2xl">{{ slotLabels[slot]!.label }}</h2>
              <p class="text-sm text-muted-foreground">{{ slotLabels[slot]!.hint }}</p>
            </div>
            <div class="flex flex-wrap items-center gap-2">
              <SkeletonState class="h-10 w-56" /><SkeletonState class="h-10 w-24" />
            </div>
          </div>
          <div class="mt-4 grid grid-cols-3 gap-2">
            <SkeletonState v-for="surface in 3" :key="surface" class="aspect-[4/3] w-full" />
          </div>
          <ul class="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            <li v-for="version in 3" :key="version" class="flex gap-3 rounded-xl border border-border p-3">
              <SkeletonState class="h-16 w-20 shrink-0" />
              <div class="min-w-0 flex-1 space-y-2">
                <SkeletonState class="h-4 w-3/4" /><SkeletonState class="h-3 w-full" /><SkeletonState
                  class="h-5 w-2/3"
                />
              </div>
            </li>
          </ul>
        </section>
      </template>
      <template v-else-if="kind === 'appearances'">
        <ul class="space-y-3">
          <li v-for="appearance in 3" :key="appearance" class="panel flex flex-wrap items-center justify-between gap-3">
            <div class="min-w-0 flex-1 space-y-2">
              <SkeletonState class="h-7 w-64 max-w-full" /><SkeletonState class="h-5 w-96 max-w-full" />
            </div>
            <div class="flex gap-2"><SkeletonState class="h-10 w-24" /><SkeletonState class="h-10 w-28" /></div>
          </li>
        </ul>
      </template>
      <template v-else-if="kind === 'connect'">
        <section class="panel">
          <h2 class="font-editorial text-2xl">Barqaror logotip manzillari</h2>
          <table class="mt-3 w-full text-sm">
            <tbody>
              <tr v-for="slot in slots" :key="slot" class="border-t border-border">
                <td class="py-2 pr-3 font-semibold">{{ slotLabels[slot]!.label }}</td>
                <td class="py-2"><SkeletonState class="h-4 w-full" /></td>
              </tr>
            </tbody>
          </table>
        </section>
        <section class="panel space-y-3 text-sm">
          <h2 class="font-editorial text-2xl">Ma’lumot va uslub</h2>
          <SkeletonState v-for="line in 4" :key="line" class="h-5 w-full" />
        </section>
        <section class="panel space-y-3">
          <h2 class="font-editorial text-2xl">Namuna kod</h2>
          <SkeletonState v-for="snippet in 2" :key="snippet" class="h-10 w-full" />
        </section>
      </template>
      <template v-else>
        <section class="panel">
          <div class="flex flex-wrap items-start justify-between gap-4">
            <div>
              <h2 class="font-editorial text-2xl">Nashrga tayyor o‘zgarishlar</h2>
              <SkeletonState class="mt-1 h-5 w-56" />
            </div>
            <div class="flex flex-wrap items-center gap-2">
              <SkeletonState class="h-10 w-64" /><SkeletonState class="h-10 w-32" />
            </div>
          </div>
          <div class="mt-4 flex flex-wrap gap-2">
            <SkeletonState v-for="change in 3" :key="change" class="h-6 w-28" />
          </div>
        </section>
        <section class="panel" aria-label="Brendni o‘qiyotgan ilovalar">
          <h2 class="font-editorial text-2xl">Brendni o‘qiyotgan ilovalar</h2>
          <ul class="mt-3 grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
            <li v-for="consumer in 3" :key="consumer" class="rounded-xl border border-border p-3 text-sm">
              <SkeletonState class="h-5 w-2/3" /><SkeletonState class="mt-1 h-4 w-full" /><SkeletonState
                class="mt-1 h-6 w-24"
              />
            </li>
          </ul>
        </section>
        <section class="panel" aria-label="Nashrlar tarixi">
          <h2 class="font-editorial text-2xl">Nashrlar tarixi</h2>
          <ol class="mt-3 space-y-2 text-sm">
            <li v-for="release in 2" :key="release" class="rounded-lg border border-border px-3 py-2">
              <SkeletonState class="h-5 w-64 max-w-full" /><SkeletonState class="mt-1 h-4 w-2/3" /><SkeletonState
                class="mt-1 h-4 w-full"
              />
            </li>
          </ol>
        </section>
      </template>
    </div>
    <div v-else class="brand-query-region text-sm text-muted-foreground">
      <p>{{ state === 'unavailable' ? 'Brend ma’lumotlari hozir mavjud emas.' : 'Hali brend ma’lumotlari yo‘q.' }}</p>
    </div>
  </section>
</template>
