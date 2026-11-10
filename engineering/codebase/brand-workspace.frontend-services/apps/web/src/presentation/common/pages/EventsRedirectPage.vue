<script setup lang="ts">
import { computed, onMounted } from 'vue';

import { useOcharoApps } from '@/application/common/hooks/UseOcharoApps';
import { useUiLocale } from '@/application/common/hooks/UseUiLocale';

import NotFoundPage from './NotFoundPage.vue';

/**
 * Events moved to its own app: its old Desk address opens that app in this tab when the app's address is set, and
 * reads as an unknown address otherwise.
 */
defineOptions({ name: 'EventsRedirectPage' });
const { t } = useUiLocale();
const target = computed(() => useOcharoApps().find((app) => app.key === 'events')?.url ?? null);

onMounted(() => {
  if (target.value) window.location.replace(target.value);
});
</script>

<template>
  <p v-if="target" role="status" class="query-label">{{ t('Opening Events…') }}</p>
  <NotFoundPage v-else />
</template>
