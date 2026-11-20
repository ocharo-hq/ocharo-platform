<script setup lang="ts">
import { useServedOrganization } from '../../../application/events/UseServedOrganization';
import NotFoundPage from '../../events/pages/NotFoundPage.vue';
import RemindersPage from './RemindersPage.vue';

/** Opens the reminder settings for the organization the address names, served as the feed is; anything else reads as not found. */
defineOptions({ name: 'RemindersRoute' });
const { served } = useServedOrganization();
</script>

<template>
  <RemindersPage v-if="served" />
  <NotFoundPage v-else />
</template>
