<script setup lang="ts">
/** A logo shown on light, dark and checkered surfaces so contrast problems are visible before publishing. */
defineOptions({ name: 'LogoSwatch' });
const props = defineProps<{ src: string; alt: string; compact?: boolean }>();
</script>

<template>
  <div :class="['grid gap-2', props.compact ? 'grid-cols-3' : 'grid-cols-3']">
    <div class="flex aspect-[4/3] items-center justify-center rounded-lg border border-border bg-[#f8f7f3] p-3">
      <img
        :src="props.src"
        :alt="`${props.alt} — och fon`"
        class="max-h-full max-w-full object-contain"
        loading="lazy"
      />
    </div>
    <div class="flex aspect-[4/3] items-center justify-center rounded-lg bg-[#20211e] p-3">
      <img
        :src="props.src"
        :alt="`${props.alt} — to‘q fon`"
        class="max-h-full max-w-full object-contain"
        loading="lazy"
      />
    </div>
    <div
      class="flex aspect-[4/3] items-center justify-center rounded-lg p-3"
      style="background: repeating-conic-gradient(#e6e2d8 0% 25%, #fffdf8 0% 50%) 50% / 16px 16px"
    >
      <img
        :src="props.src"
        :alt="`${props.alt} — shaffoflik`"
        class="max-h-full max-w-full object-contain"
        loading="lazy"
      />
    </div>
  </div>
</template>
