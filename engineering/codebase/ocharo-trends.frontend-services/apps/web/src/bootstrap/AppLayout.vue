<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue';
import { RouterLink, RouterView, useRoute } from 'vue-router';
import { AppFrame, type AppFrameAccount } from '@wow-two-beta/fe/presentation/layout';
import { useReviewSession } from '../application/trends/UseTrends';

const route = useRoute();
const frame = ref<InstanceType<typeof AppFrame>>();
const { person, signedIn, signOut } = useReviewSession();
const version = __APP_VERSION__;
/** The place itself, named by its route metadata (`label`): the top bar says where you are, the page body does not. */
const crumbs = computed(() => (typeof route.meta.label === 'string' ? [route.meta.label] : []));
const account = computed<AppFrameAccount>(() => ({
  name: person.value?.name ?? person.value?.email ?? undefined,
  description: person.value?.name ? (person.value.email ?? undefined) : undefined,
  versions: [{ label: 'Trends', version }],
  onSignOut: signedIn.value ? signOut : undefined,
}));

watch(
  () => route.path,
  async () => {
    await nextTick();
    frame.value?.scrollToTop();
  },
);
</script>

<template>
  <AppFrame
    ref="frame"
    :version="version"
    :account="account"
    :crumbs="crumbs"
    trail-label="You are here"
    content-max-width="1200px"
  >
    <template #brand>
      <RouterLink to="/" class="trends-brand">
        <span class="trends-mark" aria-hidden="true">o</span>
        <span>Ocharo Trends</span>
      </RouterLink>
    </template>
    <template #bar-end>
      <nav aria-label="Trends navigation" class="trends-nav">
        <RouterLink to="/" :class="{ current: route.name !== 'review' }">Browse</RouterLink>
        <RouterLink to="/review" :class="{ current: route.name === 'review' }">Review</RouterLink>
      </nav>
    </template>
    <RouterView />
  </AppFrame>
</template>
