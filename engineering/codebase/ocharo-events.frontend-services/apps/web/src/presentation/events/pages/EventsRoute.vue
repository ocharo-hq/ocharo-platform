<script setup lang="ts">
import { computed } from 'vue';
import { useRoute } from 'vue-router';

import { useIdentitySession } from '../../../application/common/UseIdentitySession';
import { useEventsAccess, type EventsWorkspace } from '../../../application/events/UseEventsAccess';
import { readEventsAddress } from '../../../domain/events/Events';
import EventsPage from './EventsPage.vue';
import NotFoundPage from './NotFoundPage.vue';

/**
 * Opens the Events page for the organization the address names. Only the session's own organization is served, and only
 * to a person who may read it; anything else reads as not found, as the API answers.
 */
defineOptions({ name: 'EventsRoute' });
const route = useRoute();
const { organization, canRead, canEdit } = useIdentitySession();
const access = useEventsAccess();
const workspace: EventsWorkspace = { organization, canEdit };
const served = computed(
  () =>
    organization.value !== null &&
    organization.value.id.toLowerCase() === String(route.params.organizationId).toLowerCase() &&
    canRead.value,
);
// The page keeps its own view across the feed and one event, so only the first address seeds it.
const initialLocation = readEventsAddress(route.path) ?? undefined;
</script>

<template>
  <EventsPage v-if="served" :access="access" :workspace="workspace" :initial-location="initialLocation" />
  <NotFoundPage v-else />
</template>
