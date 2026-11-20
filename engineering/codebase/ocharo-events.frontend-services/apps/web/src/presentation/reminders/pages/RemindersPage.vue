<script setup lang="ts">
import { Card, EmptyState } from '@wow-two-beta/fe/presentation/display';

import { useUiLocale } from '@/application/shared/UseUiLocale';

/**
 * The scheduled reminders' settings: which events remind whom, on which channel and when. The reminder system is not
 * built yet, so the page says so, inside a card aligned under its title like every other page's content (not a bare
 * empty state floating centered in the page); it fills in with that system (OE05's read-back is replaced by it).
 */
defineOptions({ name: 'RemindersPage' });
const { t } = useUiLocale();
</script>

<template>
  <section class="space-y-6">
    <header class="flex flex-wrap items-baseline gap-x-4 gap-y-1">
      <h1 class="page-title my-0">{{ t('Reminders') }}</h1>
    </header>
    <Card radius="2xl" padding="lg">
      <EmptyState
        class="items-start text-left"
        :title="t('Reminder settings are on their way')"
        :description="
          t('Choose which events remind whom, on which channel and when. This page fills in with the reminder system.')
        "
      />
    </Card>
  </section>
</template>
