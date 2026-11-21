<script setup lang="ts">
import { DigestsPanel } from '@wow-two-beta/fe-events/admin';
import { DigestChannelConstants } from '@wow-two-beta/fe-events/contracts';

import { useTelegramDelivery } from '@/application/delivery/UseTelegramDelivery';
import { useUiLocale } from '@/application/shared/UseUiLocale';

/**
 * The organization's Telegram delivery for its admins: WoW2 Events' digest editor for the Events page's catalog, its
 * list, editor with preview and send now, and run history, starting a new digest from Ocharo's weekly default. Each
 * event in a message links to its page in this app; the sidebar leads back to the feed.
 */
defineOptions({ name: 'TelegramDeliveryPage' });
const props = defineProps<{ organizationId: string; subject: string | null }>();
const { t, uiLocale } = useUiLocale();
const { client, catalog, defaults, workflow, linkTemplate } = useTelegramDelivery(props.organizationId);
const channels = [DigestChannelConstants.telegram];
</script>

<template>
  <section class="space-y-6">
    <header class="space-y-2">
      <h1 class="sr-only">{{ t('Telegram delivery') }}</h1>
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
            'Recipients default to every member of the organization; a member who has not linked Telegram shows as not linked in the run history.',
          )
        }}
      </p>
      <p v-if="subject" class="break-all text-sm text-muted-foreground" data-test="own-subject">
        {{ t('Your account id: {id}', { id: subject }) }}
      </p>
    </header>
    <DigestsPanel
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
