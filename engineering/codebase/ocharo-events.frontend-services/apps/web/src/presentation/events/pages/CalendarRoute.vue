<script setup lang="ts">
import { useServedOrganization } from '../../../application/events/UseServedOrganization';
import CalendarPage from './CalendarPage.vue';
import NotFoundPage from './NotFoundPage.vue';

/** Opens the calendar for the organization the address names, served as the feed is; anything else reads as not found. */
defineOptions({ name: 'CalendarRoute' });
const { served, organizationId } = useServedOrganization();
</script>

<template>
  <CalendarPage v-if="served && organizationId" :key="organizationId" :organization-id="organizationId" />
  <NotFoundPage v-else />
</template>
