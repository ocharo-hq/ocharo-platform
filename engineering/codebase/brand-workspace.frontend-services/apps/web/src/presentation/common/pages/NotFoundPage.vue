<script setup lang="ts">
import { RouterLink } from 'vue-router';

import { useUiLocale } from '@/application/common/hooks/UseUiLocale';

const { t } = useUiLocale();

/** The place for an address Desk does not know: it says so and leads back to the start. */
defineOptions({ name: 'NotFoundPage' });
</script>

<template>
  <section class="mx-auto max-w-xl px-6 py-24 text-center" aria-labelledby="not-found-title">
    <p class="font-mono text-sm text-muted-foreground">404</p>
    <h1 id="not-found-title" class="mt-2 text-2xl font-semibold">{{ t('Page not found') }}</h1>
    <p class="mt-3 text-sm text-muted-foreground">{{ t('This address does not open a Desk page.') }}</p>
    <RouterLink to="/" class="mt-6 inline-block text-sm font-medium text-primary hover:underline">{{ t('Back to Desk') }}</RouterLink>
  </section>
</template>
