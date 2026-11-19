<script setup lang="ts">
import { computed, onMounted } from 'vue';
import { RouterLink } from 'vue-router';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { DigestsPanel } from '@wow-two-beta/fe-events/admin';
import { DigestChannelConstants } from '@wow-two-beta/fe-events/contracts';
import { eventsFailureMessage } from '@wow-two-beta/fe-events/kit';

import { useTelegramDelivery } from '@/application/delivery/UseTelegramDelivery';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { eventsPath } from '@/domain/events/Events';

/**
 * The organization's Telegram delivery for its admins: WoW2 Events' digest editor for the Events page's catalog, its
 * list, editor with preview and send now, and run history, starting a new digest from Ocharo's weekly default. Each
 * event in a message links to its page in this app. Beside the panel an admin can add the "Deadline reminders" digest (OE05),
 * created switched off.
 */
defineOptions({ name: 'TelegramDeliveryPage' });
const props = defineProps<{ organizationId: string; subject: string | null }>();
const { t, uiLocale } = useUiLocale();
const {
  client,
  catalog,
  defaults,
  workflow,
  linkTemplate,
  reminderPresent,
  reminderAdding,
  reminderFailure,
  panelVersion,
  readReminder,
  addReminder,
} = useTelegramDelivery(props.organizationId);
onMounted(readReminder);
const channels = [DigestChannelConstants.telegram];
const eventsHref = computed(() => eventsPath(props.organizationId, { view: 'feed', eventId: null }));
</script>

<template>
  <section class="space-y-6">
    <header class="space-y-2">
      <p class="eyebrow">{{ t('Settings') }}</p>
      <h1 class="page-title">{{ t('Telegram delivery') }}</h1>
      <p class="max-w-2xl text-muted-foreground">
        {{
          t(
            "Ocharo's events go out through the Ocharo Telegram bot to the people named here, on the schedule set here. Each event links to its page in Events.",
          )
        }}
      </p>
      <p class="max-w-2xl text-sm text-muted-foreground" data-test="recipients-hint">
        {{
          t(
            'Recipients default to role:member, every member of the organization at each run; to reach one person only, type their Ocharo account id instead, one per line. Someone who has not linked Telegram to their Ocharo account shows as not_linked in the run history.',
          )
        }}
      </p>
      <p v-if="subject" class="break-all text-sm text-muted-foreground" data-test="own-subject">
        {{ t('Your account id: {id}', { id: subject }) }}
      </p>
    </header>
    <RouterLink :to="eventsHref" class="text-sm font-medium text-primary underline-offset-4 hover:underline"
      >← {{ t('All events') }}</RouterLink
    >
    <section
      v-if="reminderPresent === false"
      class="max-w-2xl space-y-2 rounded-md border border-border p-4"
      aria-labelledby="deadline-reminders-title"
      data-test="deadline-reminders"
    >
      <h2 id="deadline-reminders-title" class="font-semibold">{{ t('Deadline reminders') }}</h2>
      <p class="text-sm text-muted-foreground">
        {{
          t(
            'Every day at 09:00 (Tashkent), every member gets the events that are shortlisted, applying or applied and have an application deadline within 7 days. It is added switched off; switch it on in the list below.',
          )
        }}
      </p>
      <p v-if="reminderFailure" class="text-sm text-destructive" role="alert" data-test="deadline-reminders-failure">
        {{ eventsFailureMessage(reminderFailure, uiLocale, 'admin') }}
      </p>
      <Button size="sm" :is-disabled="reminderAdding" data-test="deadline-reminders-add" @click="addReminder">
        {{ t('Add deadline reminders') }}
      </Button>
    </section>
    <DigestsPanel
      :key="panelVersion"
      :client="client"
      :catalog="catalog"
      :locale="uiLocale"
      :defaults="defaults"
      :channels="channels"
      :link-template="linkTemplate"
      :workflow="workflow"
    />
  </section>
</template>
