<script setup lang="ts">
import { RouterLink } from 'vue-router';
import { useUiLocale } from '../../application/shared/UseUiLocale';

/** Unknown route. */
defineOptions({ name: 'NotFoundPage' });
const { t } = useUiLocale();
</script>

<template>
  <section class="panel p-12 text-center">
    <h1 class="font-editorial text-3xl">{{ t('Page not found.') }}</h1>
    <RouterLink to="/" class="btn-primary mt-6">{{ t('Back to products') }}</RouterLink>
  </section>
</template>
