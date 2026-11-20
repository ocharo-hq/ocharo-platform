import { computed } from 'vue';
import { useRoute } from 'vue-router';

import { useIdentitySession } from '@/application/common/UseIdentitySession';

/**
 * Which organization the open address names and whether this session is served it. Only the session's own organization
 * is served, and only to a person who may read its events (`events:read`); anything else reads as not found, as the API
 * answers. Every place under `/organizations/{organizationId}/…` asks this before it renders.
 */
export function useServedOrganization() {
  const route = useRoute();
  const { organization, canRead, canEdit, canAdmin, person } = useIdentitySession();
  /** The session organization's id, lower-cased; `null` for a person acting in none. */
  const organizationId = computed(() => organization.value?.id.toLowerCase() ?? null);
  const served = computed(
    () =>
      organizationId.value !== null &&
      organizationId.value === String(route.params.organizationId).toLowerCase() &&
      canRead.value,
  );
  return { organization, organizationId, served, canEdit, canAdmin, person };
}
