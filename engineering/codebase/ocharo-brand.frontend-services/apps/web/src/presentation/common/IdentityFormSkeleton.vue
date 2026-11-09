<script setup lang="ts">
import { Card, LabelText } from '@wow-two-beta/fe/presentation/display';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';

/** Reserves the identity editor's field groups while the first acknowledged draft loads. */
defineOptions({ name: 'IdentityFormSkeleton' });
const texts = ['Tavsif (kartochka old tomoni)', 'Shior', 'Joylashuv'];
const fonts = ['Sarlavha shrifti', 'Matn shrifti', 'Izoh'];
</script>

<template>
  <div aria-busy="true" class="space-y-6">
    <span class="sr-only" role="status">Identitet ma’lumotlari yuklanmoqda.</span>
    <Card radius="2xl" padding="xl">
      <div class="grid gap-4 md:grid-cols-[240px_1fr]">
        <div><LabelText class="mb-1.5">Brend nomi</LabelText><SkeletonState class="h-10 w-full" /></div>
        <div><LabelText class="mb-1.5">Yozilish qoidasi</LabelText><SkeletonState class="h-16 w-full" /></div>
      </div>
    </Card>
    <Card radius="2xl" padding="xl">
      <h2 class="font-editorial text-2xl">Matnlar</h2>
      <div v-for="text in texts" :key="text" class="mt-4">
        <LabelText class="mb-1.5">{{ text }}</LabelText>
        <div class="grid gap-3 md:grid-cols-3">
          <SkeletonState v-for="locale in 3" :key="locale" class="h-10 w-full" />
        </div>
      </div>
      <div class="mt-4">
        <LabelText class="mb-1.5">Ovoz va uslub eslatmalari</LabelText><SkeletonState class="h-24 w-full" />
      </div>
    </Card>
    <Card radius="2xl" padding="xl">
      <h2 class="font-editorial text-2xl">Ranglar</h2>
      <div class="mt-4 grid gap-3 md:grid-cols-2">
        <SkeletonState v-for="color in 2" :key="color" class="h-24 w-full" />
      </div>
    </Card>
    <Card radius="2xl" padding="xl">
      <h2 class="font-editorial text-2xl">Shriftlar</h2>
      <div class="mt-4 grid gap-4 md:grid-cols-3">
        <div v-for="font in fonts" :key="font">
          <LabelText class="mb-1.5">{{ font }}</LabelText
          ><SkeletonState class="h-10 w-full" />
        </div>
      </div>
      <SkeletonState class="mt-3 h-8 w-2/3" />
    </Card>
    <Card radius="2xl" padding="xl">
      <h2 class="font-editorial text-2xl">Rasmiy havolalar</h2>
      <div class="mt-4 grid gap-2 md:grid-cols-[120px_160px_180px_1fr_auto]">
        <SkeletonState v-for="field in 4" :key="field" class="h-10 w-full" /><SkeletonState class="h-10 w-10" />
      </div>
    </Card>
  </div>
</template>
