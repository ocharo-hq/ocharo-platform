<script setup lang="ts">
import { useRoute } from 'vue-router';

import { useEventsAccess, type EventsWorkspace } from '../../../application/events/UseEventsAccess';
import { useServedOrganization } from '../../../application/events/UseServedOrganization';
import { readEventsAddress } from '../../../domain/events/Events';
import EventsPage from './EventsPage.vue';
import NotFoundPage from './NotFoundPage.vue';

/**
 * Opens the Events feed for the organization the address names. Only the session's own organization is served, and only
 * to a person who may read it; anything else reads as not found, as the API answers.
 */
defineOptions({ name: 'EventsRoute' });
const route = useRoute();
const { served, organization, canEdit } = useServedOrganization();
const access = useEventsAccess();
const workspace: EventsWorkspace = { organization, canEdit };
// The page keeps its own view across the feed and one event, so only the first address seeds it.
const initialLocation = readEventsAddress(route.path) ?? undefined;
</script>

<template>
  <EventsPage v-if="served" :access="access" :workspace="workspace" :initial-location="initialLocation" />
  <NotFoundPage v-else />
</template>
