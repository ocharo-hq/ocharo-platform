<script setup lang="ts">
import { computed } from 'vue';

import { Button } from '@wow-two-beta/ui-vue/presentation/actions';
import { Badge } from '@wow-two-beta/ui-vue/presentation/display';

import { useSystemStatus } from '@/application/system/UseSystemStatus';
import type { OrganizationPreview } from '@/domain/system/OrganizationPreview';

/** Presents the foundation snapshot and the next product boundary without simulated business data. */
defineOptions({ name: 'FoundationPage' });
defineProps<{ organizationPreview: OrganizationPreview }>();

const { status, loading, busy, failureMessage, refresh } = useSystemStatus();

/** The last worker observation, explicitly localized for the person viewing the page. */
const lastSeen = computed(() => status.value?.worker.lastSeenAtUtc
  ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'medium' })
    .format(new Date(status.value.worker.lastSeenAtUtc))
  : null);
/** The readable heartbeat status, without implying publishing is enabled. */
const workerLabel = computed(() => {
  switch (status.value?.worker.state) {
    case 'healthy': return 'Heartbeat received';
    case 'stale': return 'Heartbeat overdue';
    default: return 'Waiting for first heartbeat';
  }
});
</script>

<template>
  <div class="min-h-screen bg-background text-foreground">
    <a href="#main" class="skip-link">Skip to content</a>
    <header class="border-b border-border bg-card/75">
      <div class="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-4 px-6 py-5 sm:px-10">
        <div class="flex items-center gap-3">
          <span class="brand-mark" aria-hidden="true">b<span>·</span></span>
          <div>
            <p class="text-base font-semibold tracking-tight">Brand Workspace</p>
            <p class="text-xs text-muted-foreground">A place for the work behind a brand</p>
          </div>
        </div>
        <Badge variant="outline">Foundation preview</Badge>
      </div>
    </header>

    <main id="main" class="mx-auto max-w-7xl px-6 pb-16 pt-12 sm:px-10 sm:pt-16">
      <section aria-labelledby="workspace-title" class="mb-12 grid gap-8 lg:grid-cols-[1.5fr_1fr] lg:gap-16">
        <div>
          <p class="mb-4 text-xs font-semibold uppercase tracking-[0.18em] text-primary">The first chapter</p>
          <h1 id="workspace-title" class="max-w-2xl text-4xl font-medium leading-tight tracking-tight sm:text-6xl">
            Make room for<br />the brand to grow.
          </h1>
          <p class="mt-6 max-w-xl text-base leading-7 text-muted-foreground sm:text-lg">
            Content &amp; Publishing is the first planned workspace: a home for assets, drafts,
            review, and a clear path to publication.
          </p>
        </div>
        <aside v-if="organizationPreview.planned" class="self-end rounded-2xl border border-border bg-primary-soft/50 p-6 sm:p-8" aria-label="First organization">
          <p class="mb-4 text-xs font-semibold uppercase tracking-widest text-primary">First organization · planned</p>
          <p class="font-serif text-4xl tracking-wider">{{ organizationPreview.name }}</p>
          <p class="mt-4 text-sm leading-6 text-muted-foreground">
            {{ organizationPreview.description }}
          </p>
        </aside>
      </section>

      <div class="grid items-start gap-6 lg:grid-cols-[1.15fr_1fr]">
        <section aria-labelledby="status-title" class="rounded-2xl border border-border bg-card p-6 sm:p-8">
          <div class="mb-7 flex flex-wrap items-start justify-between gap-4">
            <div>
              <p class="mb-2 text-xs font-medium uppercase tracking-widest text-muted-foreground">Available now</p>
              <h2 id="status-title" class="text-xl font-semibold tracking-tight">Foundation status</h2>
            </div>
            <Button variant="outline" size="sm" :is-loading="busy" loading-text="Checking…" @click="refresh">
              Check status
            </Button>
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
                <dd><Badge :variant="status.worker.state === 'healthy' ? 'success' : 'warning'">{{ workerLabel }}</Badge></dd>
              </div>
              <div v-if="lastSeen" class="flex flex-wrap items-start justify-between gap-3 pt-4">
                <dt class="text-sm text-muted-foreground">Last heartbeat · local time</dt>
                <dd class="text-sm"><time :datetime="status.worker.lastSeenAtUtc ?? undefined">{{ lastSeen }}</time></dd>
              </div>
            </dl>
          </div>
          <p class="mt-6 border-t border-border pt-5 text-xs leading-5 text-muted-foreground">
            A status snapshot checks service communication. It does not establish tenant security
            or enable publishing. Use “Check status” to read it again.
          </p>
        </section>

        <section aria-labelledby="next-title" class="rounded-2xl border border-border bg-card p-6 sm:p-8">
          <p class="mb-2 text-xs font-medium uppercase tracking-widest text-muted-foreground">What comes next</p>
          <h2 id="next-title" class="mb-7 text-xl font-semibold tracking-tight">From foundation to useful work</h2>
          <ol class="space-y-6">
            <li class="flex gap-4">
              <span class="step-number" aria-hidden="true">01</span>
              <div><h3 class="font-medium">Establish the organization</h3>
                <p class="mt-1 text-sm leading-6 text-muted-foreground">Sign-in, membership, and verified boundaries between brands.</p></div>
            </li>
            <li class="flex gap-4">
              <span class="step-number" aria-hidden="true">02</span>
              <div><h3 class="font-medium">Prepare a real content package</h3>
                <p class="mt-1 text-sm leading-6 text-muted-foreground">Approved assets, channel-ready copy, and manual export with a clear source.</p></div>
            </li>
            <li class="flex gap-4">
              <span class="step-number" aria-hidden="true">03</span>
              <div><h3 class="font-medium">Connect publishing carefully</h3>
                <p class="mt-1 text-sm leading-6 text-muted-foreground">Validate account access, then add scheduling and visible delivery outcomes.</p></div>
            </li>
          </ol>
        </section>
      </div>

      <footer class="mt-8 flex flex-wrap justify-between gap-3 text-xs leading-5 text-muted-foreground">
        <p>Technical foundation only. No brand data, social accounts, or publishing actions are available.</p>
        <p>Brand Workspace / Foundation</p>
      </footer>
    </main>
  </div>
</template>
