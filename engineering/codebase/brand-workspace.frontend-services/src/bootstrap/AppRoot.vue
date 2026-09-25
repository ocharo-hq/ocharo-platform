<script setup lang="ts">
import FoundationPage from '@/presentation/system/pages/FoundationPage.vue';

import { organizationPreview } from './PreviewConfiguration';

/** Composes the public foundation page before tenant and identity features exist. */
defineOptions({ name: 'AppRoot' });
</script>

<template>
  <FoundationPage :organization-preview="organizationPreview" />
</template>
