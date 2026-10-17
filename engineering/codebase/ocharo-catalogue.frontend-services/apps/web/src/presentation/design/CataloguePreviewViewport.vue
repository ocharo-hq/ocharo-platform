<script setup lang="ts">
import { computed, ref, type CSSProperties } from 'vue';
import { useResizeObserver } from '@wow-two-beta/fe/foundation/observers';

defineOptions({ name: 'CataloguePreviewViewport' });
const props = defineProps<{ width: number }>();
const pane = ref<HTMLElement | null>(null);
const content = ref<HTMLElement | null>(null);
const paneWidth = ref(0);
const contentHeight = ref(0);
const viewportWidth = computed(() => Math.max(1, props.width));
const scale = computed(() => Math.min(1, paneWidth.value / viewportWidth.value));
const dimensions = computed<CSSProperties>(() => ({
  '--preview-width': `${viewportWidth.value}px`,
  '--preview-fit-width': `${viewportWidth.value * scale.value}px`,
  '--preview-fit-height': `${contentHeight.value * scale.value}px`,
  '--preview-scale': scale.value,
}));

useResizeObserver(pane, (entry) => {
  paneWidth.value = entry.contentRect.width;
});
useResizeObserver(content, (entry) => {
  contentHeight.value = entry.contentRect.height;
});
</script>

<template>
  <div ref="pane" class="catalogue-preview-viewport w-full min-w-0 overflow-hidden" :style="dimensions">
    <div class="relative mx-auto h-(--preview-fit-height) w-(--preview-fit-width)">
      <div
        ref="content"
        class="catalogue-preview-content absolute top-0 left-0 flow-root w-(--preview-width) origin-top-left scale-(--preview-scale)"
      >
        <slot />
      </div>
    </div>
  </div>
</template>
