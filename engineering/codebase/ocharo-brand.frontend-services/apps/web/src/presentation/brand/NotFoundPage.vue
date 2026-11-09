<script setup lang="ts">
import { RouterLink } from 'vue-router';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Card } from '@wow-two-beta/fe/presentation/display';
import { useUiLocale } from '../../application/shared/UseUiLocale';

/** An address no Brand place answers, inside the frame, with the way back to the overview. */
defineOptions({ name: 'NotFoundPage' });
const { t } = useUiLocale();
</script>

<template>
  <Card
    v-bind="{ role: 'region', 'aria-labelledby': 'not-found-heading' }"
    radius="2xl"
    padding="2xl"
    class="text-center"
  >
    <h1 id="not-found-heading" class="font-editorial text-3xl">{{ t('Page not found.') }}</h1>
    <Button as-child tone="primary" class="mt-6">
      <RouterLink to="/">{{ t('Back to the overview') }}</RouterLink>
    </Button>
  </Card>
</template>
