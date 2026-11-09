<script setup lang="ts">
import { notifyError } from '@/application/common/hooks/UseFeedback';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import { useUiLocale } from '@/application/common/hooks/UseUiLocale';
import { computed, ref, watch, type Component } from 'vue';
import { BookOpen, Contact, FileText, Images, List, Palette, SquareKanban, Workflow } from 'lucide-vue-next';

import type { AccessControls } from '@/application/common/hooks/UseAccess';
import { useOverviewView } from '@/application/common/hooks/UseOverviewView';
import type { WorkspaceControls } from '@/application/common/hooks/UseWorkspace';

import OverviewMapConnections from '../OverviewMapConnections.vue';

import './OverviewPage.css';

const { t, uiLocale } = useUiLocale();

/** Connects the organization's actual creative and preparation records in three personal views. */
defineOptions({ name: 'OverviewPage' });
const props = defineProps<{ access: AccessControls; workspace: WorkspaceControls }>();
const variations = [
  { id: 'map', label: 'Work map', number: '01', description: 'See how the pieces connect.' },
  { id: 'attention', label: 'Attention board', number: '02', description: 'Find what needs your attention.' },
  { id: 'compact', label: 'Compact dashboard', number: '03', description: 'Everything in a quieter list.' },
] as const;
type OverviewVariation = (typeof variations)[number]['id'];
/** Each view's glyph in the view picker. @internal */
const VariationIcons: Readonly<Record<OverviewVariation, Component>> = { map: Workflow, attention: SquareKanban, compact: List };
/** Each work-map node's glyph, the same one its section wears in the sidebar. @internal */
const NodeIcons: Readonly<Record<string, Component>> = { book: BookOpen, card: Contact, image: Images, content: FileText, brand: Palette };
type OverviewDestination = 'catalogue' | 'business-cards' | 'content' | 'media' | 'brands';
const { view: variation, select: selectView } = useOverviewView(variations.map((item) => item.id), 'map');
const preferenceMessage = ref('');
const catalogueCount = ref<number | null>(null);
const cardCount = ref<number | null>(null);
const designLoading = ref(false);
const designError = ref(false);
watch(designError, value => { if(value) notifyError('Saved design totals are unavailable. Content and media still show their own records.'); });
const refreshRevision = ref(0);
const organization = computed(() => props.workspace.organization.value);
const organizationName = computed(() => organization.value?.name ?? t('Your workspace'));
const draftCount = computed(() => props.workspace.drafts.value?.length ?? 0);
const mediaCount = computed(() => props.workspace.media.value?.length ?? 0);
const brandCount = computed(() => props.workspace.brands.value?.length ?? 0);
const reviewCount = computed(() => props.workspace.drafts.value?.filter((draft) => draft.status === 'in-review').length ?? 0);
const approvedCount = computed(() => props.workspace.drafts.value?.filter((draft) => draft.status === 'approved').length ?? 0);
const workingCount = computed(() => props.workspace.drafts.value?.filter((draft) => draft.status === 'draft').length ?? 0);
const savedDesignCount = computed(() => catalogueCount.value === null || cardCount.value === null
  ? null : catalogueCount.value + cardCount.value);
const currentVariation = computed(() => variations.find((item) => item.id === variation.value)!);
const nodes = computed(() => [
  { id: 'catalogue', title: 'Catalogue', eyebrow: 'The collection, presented', value: catalogueCount.value, unit: 'saved designs', action: 'Open catalogue', destination: 'catalogue' as const, icon: 'book' },
  { id: 'cards', title: 'Business cards', eyebrow: 'A personal introduction', value: cardCount.value, unit: 'saved designs', action: 'Open business cards', destination: 'business-cards' as const, icon: 'card' },
  { id: 'media', title: 'Media library', eyebrow: 'The original material', value: mediaCount.value, unit: 'uploaded assets', action: 'Open media library', destination: 'media' as const, icon: 'image' },
  { id: 'content', title: 'Content desk', eyebrow: 'Words, images, review', value: draftCount.value, unit: 'content drafts', action: 'Open content desk', destination: 'content' as const, icon: 'content' },
  { id: 'brand', title: 'Brand identity', eyebrow: 'What holds it together', value: brandCount.value, unit: 'brands', action: 'Open brands', destination: 'brands' as const, icon: 'brand' },
]);
const attentionItems = computed(() => {
  const items: { id: string; title: string; detail: string; destination: OverviewDestination; label: string; tone: string }[] = [];
  if (reviewCount.value > 0) items.push({ id: 'review', title: t('{count} drafts ready for review', {count:reviewCount.value}), detail: 'Read the exact revision before it is approved.', destination: 'content', label: 'Review content', tone: 'review' });
  if (catalogueCount.value === 0) items.push({ id: 'catalogue', title: 'Your first catalogue is waiting', detail: 'Start with a layout, bring in your products, and save an edition.', destination: 'catalogue', label: 'Explore catalogue', tone: 'start' });
  if (cardCount.value === 0) items.push({ id: 'cards', title: 'Make an introduction to keep', detail: 'Choose a business-card direction and make it your own.', destination: 'business-cards', label: 'Explore business cards', tone: 'start' });
  if (brandCount.value === 0) items.push({ id: 'brand', title: 'Give your work a brand home', detail: 'A brand keeps your content and its identity together.', destination: 'brands', label: 'Open brands', tone: 'start' });
  if (mediaCount.value === 0) items.push({ id: 'media', title: 'Bring in your original images', detail: 'Keep source photographs and their provenance together.', destination: 'media', label: 'Open media library', tone: 'start' });
  if (workingCount.value > 0) items.push({ id: 'drafts', title: t('{count} drafts in the making', {count:workingCount.value}), detail: 'Pick up an unfinished piece when you are ready.', destination: 'content', label: 'Continue content', tone: 'work' });
  return items;
});
const leadingAttention = computed(() => attentionItems.value[0]);
const recentDrafts = computed(() => [...(props.workspace.drafts.value ?? [])]
  .sort((left, right) => right.updatedAtUtc.localeCompare(left.updatedAtUtc))
  .slice(0, 4));

watch(
  () => [organization.value?.id, props.access.session.value?.sessionId, props.access.invalidated.value, refreshRevision.value] as const,
  async ([organizationId, sessionId, invalidated], previous, onCleanup) => {
    if (!previous || organizationId !== previous[0] || sessionId !== previous[1] || invalidated) {
      catalogueCount.value = null;
      cardCount.value = null;
    }
    designError.value = false;
    designLoading.value = false;
    if (!organizationId || invalidated) return;
    const controller = new AbortController();
    onCleanup(() => controller.abort());
    designLoading.value = true;
    try {
      const [catalogues, cards] = await Promise.all([
        props.access.api.getDesigns(organizationId, 'catalogue', controller.signal),
        props.access.api.getDesigns(organizationId, 'business-card', controller.signal),
      ]);
      if (controller.signal.aborted) return;
      if (catalogues.ok) catalogueCount.value = catalogues.value.filter((design) => !design.archived).length;
      if (cards.ok) cardCount.value = cards.value.filter((design) => !design.archived).length;
      designError.value = !catalogues.ok || !cards.ok;
    } catch {
      if (!controller.signal.aborted) designError.value = true;
    } finally {
      if (!controller.signal.aborted) designLoading.value = false;
    }
  },
  { immediate: true },
);

/** Remembers only the aesthetic view choice, never organization or business data. */
function selectVariation(next: OverviewVariation): void {
  preferenceMessage.value = selectView(next) ? '' : 'View changed for this visit. Your browser could not remember the preference.';
}

/** Uses the existing access guard for every real workspace destination. */
function open(destination: OverviewDestination): void {
  if (organization.value) props.access.navigate(`/organizations/${organization.value.id}/${destination}`);
}

function metric(value: number | null): string {
  return value === null ? '—' : value.toLocaleString(uiLocale.value);
}

function draftStatus(status: 'draft' | 'in-review' | 'approved'): string {
  return status === 'in-review' ? 'In review' : status === 'approved' ? 'Approved' : 'Draft';
}
</script>

<template>
  <section class="business-overview" :class="`business-overview--${variation}`" aria-labelledby="overview-title">
    <header class="overview-intro">
      <div class="overview-intro-copy">
        <p class="overview-kicker"><span aria-hidden="true"></span> {{ t("Your business, in view") }}</p>
        <h1 id="overview-title">{{ t("The work,") }}<br /><em>{{ t("connected.") }}</em></h1>
        <p class="overview-intro-description">{{ t("A place for {name} to take shape.", {name:organizationName}) }}<br class="overview-desktop-break" /> {{ t("From the collection to the next conversation.") }}</p>
      </div>
      <div class="overview-view-picker">
        <span class="overview-view-label">{{ t("Make the space yours") }}</span>
        <div class="overview-variations" role="group" :aria-label="t('Overview view')">
          <button v-for="item in variations" :key="item.id" type="button" :aria-pressed="variation === item.id" :title="t(item.description)" @click="selectVariation(item.id)">
            <component :is="VariationIcons[item.id]" :size="16" aria-hidden="true" />
            <span>{{ t(item.label) }}</span>
          </button>
        </div>
        <p class="overview-view-caption"><span>{{ currentVariation.number }}</span> {{ t(currentVariation.description) }}</p>
        <p v-if="preferenceMessage" class="overview-preference-message" role="status">{{ t(preferenceMessage) }}</p>
      </div>
    </header>

    <div class="overview-data-notice"><Button variant="outline" :is-disabled="designLoading || access.busy.value" :is-loading="designLoading" @click="refreshRevision += 1">{{ t("Refresh") }}</Button></div>

    <template v-if="variation === 'map'">
      <section class="overview-map-panel" aria-labelledby="work-map-title">
        <div class="overview-panel-heading">
          <div><span class="overview-section-number">{{ t("01 / THE WORK MAP") }}</span><h2 id="work-map-title">{{ t("Every piece has a place.") }}</h2></div>
          <span class="overview-map-legend"><span aria-hidden="true"></span> {{ t("Your connected workflow") }}</span>
        </div>
        <div class="overview-map">
          <OverviewMapConnections />
          <div class="overview-map-hub">
            <span class="overview-hub-topline">{{ organizationName }}</span>
            <span class="overview-hub-monogram" aria-hidden="true">{{ organizationName.slice(0, 1) }}<i>·</i></span>
            <h3>{{ t("Your collection") }}</h3>
            <p><SkeletonState v-if="designLoading" class="inline-block h-6 w-12" /><span v-else>{{ metric(savedDesignCount) }}</span> {{ t("saved designs") }}</p>
            <span class="overview-hub-footnote">{{ t("The starting point for what comes next") }}</span>
          </div>
          <button v-for="node in nodes" :key="node.id" type="button" class="overview-map-node" :class="`overview-map-node--${node.id}`" :disabled="access.busy.value" :aria-label="`${t(node.action)}, ${node.value === null ? t('Total unavailable') : `${metric(node.value)} ${t(node.unit)}`}`" @click="open(node.destination)">
            <span class="overview-node-top"><span class="overview-node-icon" aria-hidden="true"
              ><component :is="NodeIcons[node.icon]" :size="20"
            /></span><span class="overview-node-arrow" aria-hidden="true">↗</span></span>
            <span class="overview-node-title">{{ t(node.title) }}</span>
            <span class="overview-node-description">{{ t(node.eyebrow) }}</span>
            <span class="overview-node-metric"><b><SkeletonState v-if="designLoading && ['catalogue','cards'].includes(node.id)" class="inline-block h-6 w-12" /><span v-else>{{ metric(node.value) }}</span></b> {{ t(node.unit) }}</span>
          </button>
        </div>
        <div class="overview-map-note"><span aria-hidden="true">↳</span> {{ t("The map shows how your work fits together. Moving content between apps is intentional.") }}</div>
      </section>
      <div class="overview-below-map">
        <section class="overview-next-step" aria-labelledby="next-step-title">
          <span class="overview-section-number">{{ t("A LITTLE ATTENTION") }}</span>
          <h2 id="next-step-title">{{ t(leadingAttention?.title ?? 'Room for your next idea.') }}</h2>
          <p>{{ t(leadingAttention?.detail ?? 'No drafts are waiting for review. Open your collection whenever inspiration returns.') }}</p>
          <button type="button" class="overview-text-action" :disabled="access.busy.value" @click="open(leadingAttention?.destination ?? 'catalogue')">{{ t(leadingAttention?.label ?? 'Open catalogue') }} <span aria-hidden="true">↗</span></button>
        </section>
        <aside class="overview-studio" aria-labelledby="studio-title">
          <span class="overview-section-number">{{ t("THE CREATIVE COMPANION") }}</span>
          <h2 id="studio-title">Ocharo <em>{{ t("Studio") }}</em><span aria-hidden="true">✳</span></h2>
          <p>{{ t("Explore garments, textiles and colour. Bring a chosen design into the business as a deliberate export.") }}</p>
          <span class="overview-state-tag">{{ t("Separate app · not connected here") }}</span>
        </aside>
      </div>
    </template>

    <template v-else-if="variation === 'attention'">
      <section class="overview-attention-summary" :aria-label="t('Content preparation status')">
        <div><span class="overview-section-number">{{ t("YOUR CONTENT, RIGHT NOW") }}</span><h2>{{ t("A clearer place") }}<br />{{ t("to pick up the thread.") }}</h2></div>
        <dl><div><dt>{{ t("In the making") }}</dt><dd>{{ workingCount }}</dd></div><div><dt>{{ t("In review") }}</dt><dd>{{ reviewCount }}</dd></div><div><dt>{{ t("Approved") }}</dt><dd>{{ approvedCount }}</dd></div></dl>
      </section>
      <section class="overview-attention-board" :aria-label="t('Things to pick up')">
        <article v-for="(item, index) in attentionItems" :key="item.id" class="overview-attention-card" :class="`overview-attention-card--${item.tone}`">
          <div class="overview-attention-card-top"><span>{{ String(index + 1).padStart(2, '0') }}</span><span>{{ t(item.tone === 'review' ? 'Ready for review' : item.tone === 'work' ? 'In progress' : 'A place to begin') }}</span></div>
          <h2>{{ t(item.title) }}</h2><p>{{ t(item.detail) }}</p>
          <button type="button" class="overview-text-action" :disabled="access.busy.value" @click="open(item.destination)">{{ t(item.label) }} <span aria-hidden="true">↗</span></button>
        </article>
        <article v-if="attentionItems.length === 0" class="overview-attention-card overview-attention-card--quiet">
          <span class="overview-section-number">{{ t("A QUIETER MOMENT") }}</span><h2>{{ t("Nothing waiting for review.") }}</h2><p>{{ t("Your saved work is here whenever you want to return to it.") }}</p>
          <button type="button" class="overview-text-action" :disabled="access.busy.value" @click="open('catalogue')">{{ t("Open catalogue") }} <span aria-hidden="true">↗</span></button>
        </article>
      </section>
      <section class="overview-shortcuts" aria-labelledby="attention-shortcuts-title"><h2 id="attention-shortcuts-title">{{ t("Everything has its place") }}</h2><div><button v-for="node in nodes" :key="node.id" type="button" :disabled="access.busy.value" @click="open(node.destination)"><span>{{ t(node.title) }}</span><span><SkeletonState v-if="designLoading && ['catalogue','cards'].includes(node.id)" class="inline-block h-6 w-12" /><span v-else>{{ metric(node.value) }}</span> <i aria-hidden="true">↗</i></span></button></div></section>
    </template>

    <template v-else>
      <div class="overview-compact-layout">
        <section class="overview-compact-panel" aria-labelledby="compact-title">
          <div class="overview-panel-heading"><div><span class="overview-section-number">{{ t("THE ESSENTIALS") }}</span><h2 id="compact-title">{{ t("Your working collection.") }}</h2></div><span class="overview-compact-mark" aria-hidden="true">{{ organizationName.slice(0, 1) }}·</span></div>
          <button v-for="(node, index) in nodes" :key="node.id" type="button" class="overview-compact-row" :disabled="access.busy.value" @click="open(node.destination)"><span class="overview-row-number">{{ String(index + 1).padStart(2, '0') }}</span><span class="overview-row-title"><b>{{ t(node.title) }}</b><span>{{ t(node.eyebrow) }}</span></span><span class="overview-row-count"><b><SkeletonState v-if="designLoading && ['catalogue','cards'].includes(node.id)" class="inline-block h-6 w-12" /><span v-else>{{ metric(node.value) }}</span></b><span>{{ t(node.unit) }}</span></span><span class="overview-row-arrow" aria-hidden="true">↗</span></button>
        </section>
        <aside class="overview-compact-attention"><span class="overview-section-number">{{ t("PICK UP THE THREAD") }}</span><span class="overview-compact-symbol" aria-hidden="true">↳</span><h2>{{ t(leadingAttention?.title ?? 'The next idea is yours.') }}</h2><p>{{ t(leadingAttention?.detail ?? 'Open a saved design or begin a new direction.') }}</p><button type="button" class="overview-text-action" :disabled="access.busy.value" @click="open(leadingAttention?.destination ?? 'catalogue')">{{ t(leadingAttention?.label ?? 'Open catalogue') }} <span aria-hidden="true">↗</span></button><div class="overview-compact-status"><span>{{ t("Ready for review") }} <b>{{ reviewCount }}</b></span><span>{{ t("Approved content") }} <b>{{ approvedCount }}</b></span></div></aside>
      </div>
    </template>

    <section v-if="recentDrafts.length" class="overview-recent" aria-labelledby="overview-recent-title">
      <div class="overview-recent-heading"><div><span class="overview-section-number">{{ t("ON YOUR DESK") }}</span><h2 id="overview-recent-title">{{ t("Recent content") }}</h2></div><button type="button" class="overview-text-action" :disabled="access.busy.value" @click="open('content')">{{ t("All content") }} <span aria-hidden="true">↗</span></button></div>
      <ul><li v-for="draft in recentDrafts" :key="draft.id"><button type="button" :disabled="access.busy.value" @click="access.navigate(`/organizations/${organization?.id}/drafts/${draft.id}`)"><span><b>{{ draft.title || t('Untitled draft') }}</b><small>{{ t("Revision") }} {{ draft.revision }}</small></span><span class="overview-draft-status" :class="`overview-draft-status--${draft.status}`">{{ t(draftStatus(draft.status)) }}</span><span aria-hidden="true">↗</span></button></li></ul>
    </section>

    <footer class="overview-horizon" :aria-label="t('Future connections')">
      <div><span class="overview-section-number">{{ t("BEYOND THE WORKSPACE") }}</span><p>{{ t("More of the business, when it is ready.") }}</p></div>
      <ul><li><span aria-hidden="true">○</span> {{ t("Marketing") }} <small>{{ t("Planned") }}</small></li><li><span aria-hidden="true">○</span> {{ t("Journal") }} <small>{{ t("Not connected") }}</small></li><li><span aria-hidden="true">○</span> {{ t("Scheduling") }} <small>{{ t("Planned") }}</small></li><li v-if="variation !== 'map'"><span aria-hidden="true">○</span> {{ t("Studio") }} <small>{{ t("Separate app") }}</small></li></ul>
    </footer>
    <span v-if="designLoading" class="sr-only" role="status">{{t("Loading saved design totals…")}}</span>
  </section>
</template>
