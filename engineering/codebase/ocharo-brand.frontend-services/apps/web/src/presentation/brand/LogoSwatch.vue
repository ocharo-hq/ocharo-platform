<script setup lang="ts">
/**
 * A logo shown on paper, ink and checkered surfaces so contrast problems are visible before publishing. The surfaces
 * are fixed artwork tones, the same in the interface's light and dark modes.
 */
defineOptions({ name: 'LogoSwatch' });
const props = defineProps<{ src: string; alt: string; compact?: boolean }>();
</script>

<template>
  <div :class="['grid gap-2', props.compact ? 'grid-cols-3' : 'grid-cols-3']">
    <div class="flex aspect-[4/3] items-center justify-center rounded-lg border border-border bg-artwork-paper p-3">
      <img
        :src="props.src"
        :alt="`${props.alt} — on light background`"
        class="max-h-full max-w-full object-contain"
        loading="lazy"
      />
    </div>
    <div class="flex aspect-[4/3] items-center justify-center rounded-lg bg-artwork-ink p-3">
      <img
        :src="props.src"
        :alt="`${props.alt} — on dark background`"
        class="max-h-full max-w-full object-contain"
        loading="lazy"
      />
    </div>
    <div class="artwork-check flex aspect-[4/3] items-center justify-center rounded-lg p-3">
      <img
        :src="props.src"
        :alt="`${props.alt} — transparency`"
        class="max-h-full max-w-full object-contain"
        loading="lazy"
      />
    </div>
  </div>
</template>
