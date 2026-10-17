<script setup lang="ts">
import { computed } from 'vue';
import { Button, CopyButton } from '@wow-two-beta/fe/presentation/actions';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import { Bot, Copy, RefreshCw } from 'lucide-vue-next';

import { notifyError } from '@/application/shared/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { useMcpConnection } from '@/application/workspace/UseMcpConnection';
import type { AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';

/** Offers explicit, short-lived assistant access without saving bearer credentials. */
defineOptions({ name: 'McpAccessPanel' });
const props = defineProps<{ access: AccessControls; workspace: WorkspaceControls }>();
const { t, uiLocale } = useUiLocale();
const connection = useMcpConnection(props.access, props.workspace);
const { capabilities, loading, refreshing, ticket, issuing } = connection;
/** The endpoint on this authenticated application's origin. */
const endpoint = computed(() => new URL('/mcp', window.location.origin).href);
/** The credential expires independently of whether its display remains open. */
const expiresAt = computed(() => ticket.value
  ? new Date(ticket.value.expiresAt).toLocaleTimeString(uiLocale.value) : '');
/** Reports clipboard denial through the existing notification host. */
function copyFailed(): void { notifyError('Copy failed. Select and copy the value manually.'); }
</script>

<template>
  <section class="space-y-4 border-t border-border pt-6" aria-labelledby="mcp-access-title">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h2 id="mcp-access-title" class="flex items-center gap-2 text-xl font-semibold">
        <Bot class="size-5" aria-hidden="true" />{{ t('Assistant access · MCP') }}
      </h2>
      <Button variant="outline" :is-loading="loading || refreshing" :is-disabled="loading || refreshing || issuing" @click="connection.refresh">
        <RefreshCw class="size-4" aria-hidden="true" />{{ t('Refresh') }}
      </Button>
    </div>
    <p class="max-w-2xl text-sm text-muted-foreground">
      {{ t('Read suppliers, shared media metadata, events and deadlines from this organization.') }}
    </p>
    <SkeletonState v-if="loading && !capabilities" class="h-20 w-full rounded-lg" />
    <template v-else-if="capabilities">
      <p v-if="!capabilities.enabled" class="text-sm text-muted-foreground">
        {{ t('Assistant access is not enabled on this server. Ask your operator to configure MCP.') }}
      </p>
      <template v-else>
        <label class="block text-sm">{{ t('MCP endpoint') }}
          <input :value="endpoint" readonly class="native-input mt-2 w-full" />
        </label>
        <p class="max-w-2xl text-sm text-muted-foreground">
          {{ t('Use a trusted client that supports Streamable HTTP and a bearer token. Access expires after five minutes.') }}
        </p>
        <Button :is-loading="issuing" :is-disabled="issuing || access.busy.value" @click="connection.issue">
          {{ t(ticket ? 'Create another access token' : 'Create access token') }}
        </Button>
        <div v-if="ticket" class="space-y-3" aria-live="polite">
          <label class="block text-sm">{{ t('Bearer token') }}
            <input :value="ticket.token" type="password" readonly autocomplete="off" spellcheck="false"
              class="native-input mt-2 w-full" />
          </label>
          <div class="flex flex-wrap items-center gap-3">
            <CopyButton :text="ticket.token" :aria-label="t('Copy access token')" variant="outline" @error="copyFailed">
              <template #default="{ copied }"><Copy class="size-4" aria-hidden="true" />
                {{ t(copied ? 'Copied' : 'Copy access token') }}</template>
            </CopyButton>
            <Button variant="ghost" @click="connection.hide">{{ t('Hide token') }}</Button>
            <span class="text-sm text-muted-foreground">{{ t('Expires at {time}', { time: expiresAt }) }}</span>
          </div>
          <p class="text-sm text-muted-foreground">
            {{ t('Hiding clears this display. Copied access remains valid until expiry or sign-out.') }}
          </p>
        </div>
      </template>
    </template>
  </section>
</template>
