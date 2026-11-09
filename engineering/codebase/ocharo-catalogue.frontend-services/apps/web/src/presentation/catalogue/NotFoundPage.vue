<script setup lang="ts">
import { RouterLink } from 'vue-router';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Card } from '@wow-two-beta/fe/presentation/display';
import { useUiLocale } from '../../application/shared/UseUiLocale';

/** Unknown route. */
defineOptions({ name: 'NotFoundPage' });
const { t } = useUiLocale();
</script>

<template>
  <Card
    v-bind="{ role: 'region', 'aria-labelledby': 'not-found-heading' }"
    radius="2xl"
    padding="2xl"
    class="text-center sm:p-12"
  >
    <h1 id="not-found-heading" class="font-editorial text-3xl">{{ t('Page not found.') }}</h1>
    <Button as-child class="mt-6">
      <RouterLink to="/">{{ t('Back to products') }}</RouterLink>
    </Button>
  </Card>
</template>
