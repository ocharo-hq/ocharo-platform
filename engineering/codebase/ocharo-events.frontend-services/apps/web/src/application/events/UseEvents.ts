import { onScopeDispose, ref, shallowRef, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { localIsoDate, type EventsClient } from '@wow-two-beta/fe-events/kit';

import { requestConfirmation } from '@/application/common/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import {
  eventsApiPrefix,
  eventsPath,
  ocharoWorkflowSpec,
  readEventsAddress,
  readEventsOrganizationId,
  workflowAligned,
  type EventsLocation,
} from '@/domain/events/Events';

import { createOcharoEventsClient } from '@/integration/events/EventsClient';

import type { EventsAccess } from './UseEventsAccess';

/** The Events page's `fe-events` client over this app's mirror of WoW2 Events for the organization (OE07). */
export function createEventsPageClient(organizationId: string): EventsClient {
  return createOcharoEventsClient(eventsApiPrefix(organizationId));
}

/** Keeps the person's calendar day current across midnight and sleeping browser tabs, for deadlines and past events. */
export function useEventToday() {
  const today = ref(localIsoDate());
  let timer: ReturnType<typeof setTimeout>;
  function update(): void {
    clearTimeout(timer);
    const now = new Date();
    today.value = localIsoDate(now);
    const midnight = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1);
    timer = setTimeout(update, Math.max(1, midnight.getTime() - now.getTime()));
  }
  update();
  window.addEventListener('focus', update);
  document.addEventListener('visibilitychange', update);
  onScopeDispose(() => {
    clearTimeout(timer);
    window.removeEventListener('focus', update);
    document.removeEventListener('visibilitychange', update);
  });
  return today;
}

/**
 * Aligns the workflow WoW2 Events holds for the organization with Ocharo's (`ocharoWorkflowSpec`): its stage labels in
 * the three interface languages, `skipped` and `rejected` as lost, and the custom fields' titles with the source link
 * read-only. It writes once, at the stored revision, only when the stored one differs; Events refuses anyone but an
 * admin, so the page asks only for an admin. Answers whether the stored workflow now reads as Ocharo's.
 */
export async function alignOcharoWorkflow(client: EventsClient, signal?: AbortSignal): Promise<boolean> {
  const stored = await client.getWorkflow(signal);
  if (!stored.ok) return false;
  if (workflowAligned(stored.value)) return true;
  const written = await client.putWorkflow(
    { ...ocharoWorkflowSpec(), expectedRevision: stored.value.revision },
    signal,
  );
  return written.ok;
}

/** Moves between the feed and one event inside one document, guarding unsaved forms. */
export function useEventsNavigation(access: EventsAccess, organizationId: () => string, initial: EventsLocation) {
  const { t } = useUiLocale();
  const current = shallowRef<EventsLocation>(initial);
  const route = useRoute();
  const router = useRouter();
  const confirmDiscard = () =>
    requestConfirmation(t('Discard unsaved changes and leave this page?'), { confirmLabel: t('Discard changes') });
  async function open(next: EventsLocation): Promise<boolean> {
    if (access.busy.value) return false;
    if (access.dirty.value && !(await confirmDiscard())) return false;
    if (access.busy.value) return false;
    access.setDirty(false);
    const path = eventsPath(organizationId(), next);
    if (route.path !== path && (await router.push(path))) return false;
    current.value = next;
    return true;
  }
  /* Back and forward arrive through the router, whose guard has already asked about unsaved edits. */
  watch(
    () => route.path,
    (path) => {
      const view = readEventsAddress(path);
      if (!view || readEventsOrganizationId(path)?.toLowerCase() !== organizationId().toLowerCase()) return;
      current.value = view;
    },
  );
  return { current, open };
}
