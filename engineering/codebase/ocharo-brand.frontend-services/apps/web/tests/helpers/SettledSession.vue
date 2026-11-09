<script setup lang="ts">
import { useBffAuth } from '@wow-two-beta/fe/auth';

/** Renders its slot only once the session has settled, as the frame's guard lets a page in. */
defineOptions({ name: 'SettledSession' });
defineSlots<{ default(): unknown }>();
const auth = useBffAuth();
</script>

<template>
  <slot v-if="!auth.isPending" />
</template>
