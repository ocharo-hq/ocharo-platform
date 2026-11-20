import { computed } from 'vue';

import { useIdentitySession } from '@/application/common/UseIdentitySession';
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
 * Telegram delivery is offered only with `events:admin`.
 */
export function useEventsSections() {
  const { organization, canAdmin } = useIdentitySession();
  const links = computed<readonly EventsSectionLink[]>(() => {
    const id = organization.value?.id.toLowerCase();
    if (!id || !isEventIdentifier(id)) return [];
    return visibleEventsSections({ canAdmin: canAdmin.value }).map((entry) => ({
      ...entry,
      href: eventsSectionPath(entry.id, id),
    }));
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
