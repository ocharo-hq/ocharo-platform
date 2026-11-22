<script setup lang="ts">
import { computed } from 'vue';
import { Card } from '@wow-two-beta/fe/presentation/display';
import { FacetBar, type FacetDefinition, type FacetSelection } from '@wow-two-beta/fe/presentation/forms';
import { feedFacetBarClass, type EventsFacetBarLabels } from '@wow-two-beta/fe-events/kit';

import { pillHintFadeDuration, pillHintOpenDelay, withPillHints } from '@/domain/events/EventPillHints';

/**
 * The filter card of an events page, the one card the feed and the calendar share: a `Card` (radius `2xl`, its padding
 * smaller on a phone) holding the fe sdk's `FacetBar` as the feed's own bar looks and reads: a search, the facets
 * (inline ones as pills with their counts, the rest behind "Filters" as removable chips), "Clear all" and the match pill
 * (`n of m` when both counts are given). A pill at count 0 and not chosen greys itself, and the region and kind pills
 * open a hint card (`withPillHints`) after a long pause. The card filters nothing and knows no page: the page hands it
 * the facets, each with its counts, the selection and the search, and takes the choices back. Its words come from the
 * page (`eventsFacetBarLabels` of the kit, so both pages read alike) and its phone layout is the feed's (`feedFacetBarClass`).
 *
 * `actionsId` makes a mount point at the end of the bar's controls: an element with that id inside the card, for the
 * feed's own controls (the sort, "Propose an event", the overflow menu), which `EventsFeed` moves there through its
 * `actionsTo` selector. The calendar names none and the card has no such element.
 */
defineOptions({ name: 'EventFilterCard' });
const props = defineProps<{
  /** The facets in display order, each choice with the events it would match. */
  facetDefinitions: readonly FacetDefinition[];
  /** The bar's words, in the interface language. */
  labels: EventsFacetBarLabels;
  /** The events matching the choices and the search; the pill is left out without it. */
  matchCount?: number | undefined;
  /** The events with nothing applied; with `matchCount` the pill reads `n of m`. */
  totalCount?: number | undefined;
  /** The `id` of the element the page's own controls move into; none leaves no such element. */
  actionsId?: string | undefined;
}>();
/** The choices by facet key. */
const selection = defineModel<FacetSelection>({ required: true });
/** The search text. */
const search = defineModel<string>('search', { required: true });

const hinted = computed(() => withPillHints(props.facetDefinitions));
</script>

<template>
  <Card radius="2xl" class="p-3 sm:p-5" data-test="event-filters">
    <FacetBar
      v-model="selection"
      v-model:search="search"
      :facets="hinted"
      :match-count="matchCount"
      :total-count="totalCount"
      v-bind="labels"
      :hint-open-delay="pillHintOpenDelay"
      :hint-fade-duration="pillHintFadeDuration"
      :class="feedFacetBarClass"
    >
      <template v-if="actionsId" #trailing>
        <div :id="actionsId" class="max-sm:w-full" data-test="event-filter-actions" />
      </template>
    </FacetBar>
  </Card>
</template>
