import { computed } from 'vue';
import { useRoute } from 'vue-router';

import { useIdentitySession } from '@/application/common/UseIdentitySession';
import { withSharedFilters } from '@/domain/events/EventFilterQuery';
import { isEventIdentifier } from '@/domain/events/Events';
import {
  EventsGroupLabels,
  EventsSectionGroup,
  eventsSectionPath,
  visibleEventsSections,
  type EventsSectionLink,
} from '@/domain/shared/EventsSections';

/**
 * The sections the session is offered, addressed for its organization: the sidebar's groups, and the same sections flat
 * for the phone's bottom bar. A person acting in no organization has no addresses, so none are offered; the admins'
 * Telegram delivery is offered only with `events:admin`. The feed's and the calendar's links carry the shared filter
 * keys (`q`, `stages`, `kinds`, `cities`, `sources`, `regions`) of the address now open, and no other key, so a filter
 * set on one page is there on the other; the other links stay clean.
 */
export function useEventsSections() {
  const { organization, canAdmin } = useIdentitySession();
  const route = useRoute();
  const links = computed<readonly EventsSectionLink[]>(() => {
    const id = organization.value?.id.toLowerCase();
    if (!id || !isEventIdentifier(id)) return [];
    return visibleEventsSections({ canAdmin: canAdmin.value }).map((entry) => {
      const path = eventsSectionPath(entry.id, id);
      return { ...entry, href: entry.sharesFilters ? withSharedFilters(path, route.query) : path };
    });
  });
  /** The sidebar's groups in order, each with its heading (none for the main group); an empty group is left out. */
  const groups = computed(() =>
    Object.values(EventsSectionGroup)
      .map((group) => ({
        group,
        label: EventsGroupLabels[group],
        sections: links.value.filter((section) => section.group === group),
      }))
      .filter((entry) => entry.sections.length > 0),
  );
  return { groups, links };
}
