<script setup lang="ts">
import { computed } from 'vue';

import { Button } from '@wow-two-beta/ui-vue/presentation/actions';
import { Badge } from '@wow-two-beta/ui-vue/presentation/display';

import { useSystemStatus } from '@/application/system/UseSystemStatus';

/** Presents the foundation snapshot and the next product boundary without simulated business data. */
defineOptions({ name: 'FoundationPage' });

const { status, loading, busy, failureMessage, refresh } = useSystemStatus();

/** The last worker observation, explicitly localized for the person viewing the page. */
const lastSeen = computed(() =>
  status.value?.worker.lastSeenAtUtc
    ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'medium' }).format(
        new Date(status.value.worker.lastSeenAtUtc),
      )
    : null,
);
/** The readable heartbeat status, without implying publishing is enabled. */
const workerLabel = computed(() => {
  switch (status.value?.worker.state) {
    case 'healthy':
      return 'Heartbeat received';
    case 'stale':
      return 'Heartbeat overdue';
    default:
      return 'Waiting for first heartbeat';
  }
});
</script>

<template>
  <section aria-labelledby="status-title" class="workspace-card max-w-3xl p-6 sm:p-8">
    <div class="mb-7 flex flex-wrap items-start justify-between gap-4">
      <div>
        <p class="eyebrow">Secondary diagnostics</p>
        <h1 id="status-title" class="mt-3 text-2xl font-semibold">Foundation status</h1>
      </div>
      <Button variant="outline" size="sm" :is-loading="busy" loading-text="Checking…" @click="refresh"
        >Check status</Button
      >
    </div>
    <div aria-live="polite" aria-atomic="true" :aria-busy="busy" class="min-h-52">
      <div v-if="loading" role="status" class="rounded-xl bg-muted p-5">
        <p class="font-medium">Checking the foundation…</p>
        <p class="mt-2 text-sm text-muted-foreground">Reading the API and worker heartbeat.</p>
      </div>
      <div v-else-if="failureMessage" role="alert" class="rounded-xl bg-destructive-soft p-5">
        <p class="font-medium text-destructive-soft-foreground">Status unavailable</p>
        <p class="mt-2 text-sm leading-6 text-destructive-soft-foreground">{{ failureMessage }}</p>
        <p v-if="status" class="mt-2 text-sm text-destructive-soft-foreground">
          A previous snapshot exists, but it does not confirm the current state.
        </p>
      </div>
      <dl v-else-if="status" class="divide-y divide-border">
        <div class="flex flex-wrap items-center justify-between gap-3 pb-4">
          <dt class="text-sm text-muted-foreground">Foundation API</dt>
          <dd><Badge variant="success">Reachable</Badge></dd>
        </div>
        <div class="flex flex-wrap items-center justify-between gap-3 py-4">
          <dt class="text-sm text-muted-foreground">Configured datastore</dt>
          <dd class="text-sm font-medium">{{ status.persistence }}</dd>
        </div>
        <div class="flex flex-wrap items-center justify-between gap-3 py-4">
          <dt class="text-sm text-muted-foreground">Worker heartbeat</dt>
          <dd>
            <Badge :variant="status.worker.state === 'healthy' ? 'success' : 'warning'">{{ workerLabel }}</Badge>
          </dd>
        </div>
        <div v-if="lastSeen" class="flex flex-wrap items-start justify-between gap-3 pt-4">
          <dt class="text-sm text-muted-foreground">Last heartbeat · local time</dt>
          <dd class="text-sm">
            <time :datetime="status.worker.lastSeenAtUtc ?? undefined">{{ lastSeen }}</time>
          </dd>
        </div>
      </dl>
    </div>
    <p class="mt-6 border-t border-border pt-5 text-xs leading-5 text-muted-foreground">
      This is a service snapshot. A healthy worker does not establish publication. No publishing actions are available
      here.
    </p>
  </section>
</template>
