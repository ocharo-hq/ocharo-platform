import { computed, onScopeDispose, reactive, ref, shallowRef, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import type { ApiFailure } from '@wow-two-beta/fe/foundation/http';
import type { Result } from '@wow-two-beta/fe/foundation/results';
import { useAppQuery, useQueryCache } from '@wow-two-beta/fe/query';

import { notifyError, requestConfirmation } from '@/application/shared/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import {
  eventInput, eventLimits, eventsPath, localIsoDate,
  type EventFilter, type EventInput, type EventPriority, type EventsLocation, type EventStatus, type ShowcaseEvent,
} from '@/domain/workspace/Events';
import { readWorkspaceLocation } from '@/domain/workspace/Models';
import { createEventsApi } from '@/integration/workspace/EventsApi';

import { failureMessage, type AccessControls } from './UseAccess';
import type { WorkspaceControls } from './UseWorkspace';

/** The result of a form save as the form needs it: conflicts keep the form's input. */
export type EventSaveOutcome = 'saved' | 'conflict' | 'failed';
/** Maps a write result for a form: conflicts stay inline, other failures become a safe toast. */
export function eventSaveOutcome(result: Result<unknown, ApiFailure>): EventSaveOutcome {
  if (result.ok) return 'saved';
  if (result.failure.status === 409) return 'conflict';
  if (result.failure.code !== 'cancelled') notifyError(failureMessage(result.failure));
  return 'failed';
}

/** Keeps deadline labels and date filters current across midnight and sleeping browser tabs. */
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

/** Owns Events transport, cache keys and server-confirmed commands for one organization document. */
export function useEvents(access: AccessControls, workspace: WorkspaceControls) {
  const api = createEventsApi(() => access.session.value?.csrfToken ?? '', () => access.recheck());
  const cache = useQueryCache();
  const organizationId = () => workspace.organization.value?.id ?? '';
  // Shares the workspace scope prefix, so the shell's Refresh also reloads Events.
  const scope = () => [access.session.value?.sessionId, organizationId(), 'events'];
  const keys = {
    all: () => scope(),
    list: (filter: EventFilter) => [...scope(), 'list', filter.query.trim(), filter.scope, filter.format, filter.status, filter.from],
    summary: (today: string) => [...scope(), 'summary', today],
    event: (id: string) => [...scope(), 'event', id],
  };
  const enabled = () => Boolean(workspace.organization.value);
  const canEdit = computed(() => workspace.canEdit.value);
  const refreshing = ref(false);

  const dirtyForms = reactive(new Set<string>());
  function setFormDirty(key: string, dirty: boolean): void {
    if (dirty) dirtyForms.add(key); else dirtyForms.delete(key);
  }
  watch(() => dirtyForms.size > 0, dirty => access.setDirty(dirty), { flush: 'sync' });
  onScopeDispose(() => { if (dirtyForms.size) access.setDirty(false); });

  async function refreshViews(targets: unknown[][]): Promise<void> {
    refreshing.value = true;
    try { await Promise.all(targets.map(key => cache.invalidate(key))); }
    catch { notifyError('Saved successfully. The current view could not refresh. Your local edits are preserved.', 'events-refresh'); }
    finally { refreshing.value = false; }
  }
  /** One write can move an event between months, statuses and deadlines, so every Events view reloads. */
  const refreshAll = () => refreshViews([keys.all()]);
  async function run<T>(operation: (signal: AbortSignal) => Promise<Result<T, ApiFailure>>, signal?: AbortSignal) {
    const result = await access.write(operation, signal);
    if (result.ok) await refreshAll();
    return result;
  }
  const o = organizationId;

  return {
    api, keys, enabled, canEdit, refreshing, organizationId, setFormDirty, refreshAll,
    refreshEvent: (id: string) => cache.invalidate(keys.event(id)),
    /** Drops a deleted event from the cache once its page has closed. */
    forgetEvent: (id: string) => cache.remove(keys.event(id)),
    createEvent: (input: EventInput, signal?: AbortSignal) => run(s => api.createEvent(o(), input, s), signal),
    updateEvent: (event: ShowcaseEvent, input: EventInput, signal?: AbortSignal) =>
      run(s => api.updateEvent(o(), event.id, event.revision, input, s), signal),
    /** Changes only the status or priority, sending every other field back as it was read. */
    patchEvent: (event: ShowcaseEvent, changes: { status?: EventStatus; priority?: EventPriority }, signal?: AbortSignal) =>
      run(s => api.updateEvent(o(), event.id, event.revision, { ...eventInput(event), ...changes }, s), signal),
    /** Reloads the feed and summary but not the deleted event, which would only answer 404. */
    async deleteEvent(event: ShowcaseEvent, signal?: AbortSignal) {
      const result = await access.write(s => api.deleteEvent(o(), event.id, event.revision, s), signal);
      if (result.ok) await refreshViews([[...scope(), 'list'], [...scope(), 'summary']]);
      return result;
    },
  };
}
export type EventsControls = ReturnType<typeof useEvents>;

/** Lists events for a filter, keeping the previous rows visible while a new filter loads. */
export function useEventList(events: EventsControls, filter: () => EventFilter) {
  const query = useAppQuery({
    key: () => events.keys.list(filter()),
    enabled: events.enabled,
    queryFn: ({ signal }) => events.api.listEvents(events.organizationId(), { ...filter(), limit: eventLimits.page, offset: 0 }, signal),
  });
  const previous = shallowRef<ShowcaseEvent[]>();
  watch(query.data, value => { if (value) previous.value = value; }, { immediate: true });
  return {
    events: computed(() => query.data.value ?? previous.value),
    loading: query.loading,
    error: query.error,
    refetch: query.refetch,
    truncated: computed(() => (query.data.value?.length ?? 0) >= eventLimits.page),
  };
}

export function useEventSummary(events: EventsControls, today: () => string) {
  const query = useAppQuery({
    key: () => events.keys.summary(today()),
    enabled: events.enabled,
    queryFn: ({ signal }) => events.api.summary(events.organizationId(), today(), signal),
  });
  return { summary: query.data, loading: query.loading, error: query.error, refetch: query.refetch };
}

export function useShowcaseEvent(events: EventsControls, id: () => string | null) {
  const query = useAppQuery({
    key: () => events.keys.event(id() ?? ''),
    enabled: () => events.enabled() && Boolean(id()),
    queryFn: ({ signal }) => events.api.getEvent(events.organizationId(), id()!, signal),
  });
  return { event: query.data, loading: query.loading, error: query.error };
}

/** Moves between the feed and one event inside one document, guarding unsaved forms. */
export function useEventsNavigation(access: AccessControls, organizationId: () => string, initial: EventsLocation) {
  const { t } = useUiLocale();
  const current = shallowRef<EventsLocation>(initial);
  const route = useRoute();
  const router = useRouter();
  const confirmDiscard = () => requestConfirmation(t('Discard unsaved changes and leave this page?'), { confirmLabel: t('Discard changes') });
  async function open(next: EventsLocation): Promise<boolean> {
    if (access.busy.value) return false;
    if (access.dirty.value && !await confirmDiscard()) return false;
    if (access.busy.value) return false;
    access.setDirty(false);
    const path = eventsPath(organizationId(), next);
    if (route.path !== path && await router.push(path)) return false;
    current.value = next;
    return true;
  }
  /* Back and forward arrive through the router, whose guard has already asked about unsaved edits. */
  watch(() => route.path, (path) => {
    const location = readWorkspaceLocation(path);
    if (location.section !== 'events' || !location.events || location.organizationId?.toLowerCase() !== organizationId().toLowerCase()) return;
    current.value = location.events;
  });
  return { current, open };
}
