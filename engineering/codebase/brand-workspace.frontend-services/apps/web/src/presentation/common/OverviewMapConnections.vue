<script setup lang="ts">
/**
 * The work map's connecting lines: a drawing of where each node meets the collection hub, laid behind the nodes. It is
 * Desk's own artwork rather than an icon, so it lives here instead of in the page template.
 */
defineOptions({ name: 'OverviewMapConnections' });
</script>

<template>
  <svg class="overview-map-connections" viewBox="0 0 1000 490" preserveAspectRatio="none" aria-hidden="true">
    <path d="M270 104C370 104 333 235 500 235M748 104C638 104 667 235 500 235M240 307C360 307 350 235 500 235M760 307C650 307 645 235 500 235M500 235V407" />
    <circle cx="347" cy="159" r="3" /><circle cx="655" cy="159" r="3" /><circle cx="351" cy="277" r="3" /><circle cx="651" cy="277" r="3" /><circle cx="500" cy="360" r="3" />
  </svg>
</template>
