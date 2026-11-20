import { onScopeDispose, ref, shallowRef, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import type { IntakeFreshnessModel } from '@wow-two-beta/fe-events/contracts';
import { localIsoDate, type EventsClient } from '@wow-two-beta/fe-events/kit';

import {
  eventsApiPrefix,
  eventsPath,
  readEventsAddress,
  readEventsOrganizationId,
  workflowForPage,
  type EventsLocation,
} from '@/domain/events/Events';

import { createOcharoEventsClient } from '@/integration/events/EventsClient';

import type { EventsAccess } from './UseEventsAccess';

/**
 * The Events page's `fe-events` client over this app's mirror of WoW2 Events for the organization (OE07). It only reads
 * the workflow: a screen that asks for it (the event's tracking form) gets the stored one with Ocharo's labels filling
 * what it lacks (`workflowForPage`). The deploy writes the workflow (`engineering/deployment/deployment.md`), a visit never.
 */
export function createEventsPageClient(organizationId: string): EventsClient {
  const client = createOcharoEventsClient(eventsApiPrefix(organizationId));
  return {
    ...client,
    getWorkflow: async (signal) => {
      const stored = await client.getWorkflow(signal);
      return stored.ok ? { ...stored, value: workflowForPage(stored.value) } : stored;
    },
  };
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

/** How often the freshness read repeats while the page stays open, so "updated 3 hours ago" keeps ageing. */
const intakeFreshnessRefreshMs = 5 * 60 * 1000;

/**
 * Reads how fresh the catalog's intake is (E24): when its sources last received records and how many failed their last
 * poll. It reads on mount, every five minutes and when the tab is back in view; `now` is the instant of the last read.
 * `source` gives the client to read through, `null` while there is none (it reads again when that changes). A read that
 * fails leaves `freshness` null (a person outside the catalog's organization reads 404), so the page shows nothing and
 * adds no error surface.
 */
export function useIntakeFreshness(source: () => EventsClient | null, catalog: string) {
  const freshness = shallowRef<IntakeFreshnessModel | null>(null);
  const now = ref(Date.now());
  let generation = 0;
  async function load(): Promise<void> {
    const run = ++generation;
    const client = source();
    if (!client) {
      freshness.value = null;
      return;
    }
    const result = await client.getIntakeFreshness(catalog);
    if (run !== generation) return;
    now.value = Date.now();
    freshness.value = result.ok ? result.value : null;
  }
  function reload(): void {
    if (document.visibilityState !== 'hidden') void load();
  }
  const timer = setInterval(reload, intakeFreshnessRefreshMs);
  watch(source, () => void load());
  window.addEventListener('focus', reload);
  document.addEventListener('visibilitychange', reload);
  onScopeDispose(() => {
    generation++;
    clearInterval(timer);
    window.removeEventListener('focus', reload);
    document.removeEventListener('visibilitychange', reload);
  });
  void load();
  return { freshness, now };
}

/** Moves between the feed and one event inside one document. */
export function useEventsNavigation(access: EventsAccess, organizationId: () => string, initial: EventsLocation) {
  const current = shallowRef<EventsLocation>(initial);
  const route = useRoute();
  const router = useRouter();
  async function open(next: EventsLocation): Promise<boolean> {
    if (access.busy.value) return false;
    const path = eventsPath(organizationId(), next);
    if (route.path !== path && (await router.push(path))) return false;
    current.value = next;
    return true;
  }
  /* Back and forward arrive through the router, whose guard has already held a move while the session signs out. */
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
