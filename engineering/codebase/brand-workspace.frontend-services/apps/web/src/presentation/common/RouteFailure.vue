<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';

import { useUiLocale } from '@/application/common/hooks/UseUiLocale';

const { t } = useUiLocale();

/** What a place shows when it cannot open: its code failed to load, or it failed while rendering. */
defineOptions({ name: 'RouteFailure' });
const emit = defineEmits<{ retry: [] }>();
</script>

<template>
  <section role="alert" class="mx-auto max-w-xl px-6 py-24 text-center">
    <h1 class="text-2xl font-semibold">{{ t('Could not open this page') }}</h1>
    <p class="mt-3 text-sm text-muted-foreground">{{ t('Check your connection, then try again.') }}</p>
    <Button class="mt-6" @click="emit('retry')">{{ t('Retry') }}</Button>
  </section>
</template>
