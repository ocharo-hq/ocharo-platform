<script setup lang="ts">
import { useBrand } from '../../src/application/brand';

/** Hands the shared brand store, as a page sees it, to the test that mounted it. */
defineOptions({ name: 'BrandProbe' });
const props = defineProps<{ receive: (brand: ReturnType<typeof useBrand>) => void }>();
props.receive(useBrand());
</script>

<template>
  <span hidden></span>
</template>
