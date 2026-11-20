<script setup lang="ts">
import { RouterLink } from 'vue-router';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Card } from '@wow-two-beta/fe/presentation/display';

import { useServedOrganization } from '@/application/events/UseServedOrganization';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { eventsPath } from '@/domain/events/Events';
import NotFoundPage from '@/presentation/events/pages/NotFoundPage.vue';
import TelegramDeliveryPage from './TelegramDeliveryPage.vue';

/**
 * Opens the Telegram delivery settings for the organization the address names. Only the session's own organization is
 * served, as the API does; a member who may read its events but not set its delivery (`events:admin`) is told so, and
 * anyone else reads not found.
 */
defineOptions({ name: 'TelegramDeliveryRoute' });
const { t } = useUiLocale();
const { served, organizationId, canAdmin, person } = useServedOrganization();
</script>

<template>
  <TelegramDeliveryPage
    v-if="served && canAdmin && organizationId"
    :key="organizationId"
    :organization-id="organizationId"
    :subject="person?.sub ?? null"
  />
  <Card
    v-else-if="served && organizationId"
    v-bind="{ role: 'region', 'aria-labelledby': 'delivery-refused-heading' }"
    radius="2xl"
    padding="2xl"
    class="text-center"
  >
    <h1 id="delivery-refused-heading" class="font-editorial text-3xl">
      {{ t("Only the organization's admins set its Telegram delivery.") }}
    </h1>
    <Button as-child tone="primary" class="mt-6">
      <RouterLink :to="eventsPath(organizationId, { view: 'feed', eventId: null })">{{
        t('Back to events')
      }}</RouterLink>
    </Button>
  </Card>
  <NotFoundPage v-else />
</template>
