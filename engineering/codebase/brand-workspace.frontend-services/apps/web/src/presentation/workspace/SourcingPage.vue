<script setup lang="ts">
import { computed, ref } from 'vue';
import { Images, Users, SlidersHorizontal } from 'lucide-vue-next';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';
import { notifyError } from '@/application/shared/UseFeedback';
import { useSourcingPreferences } from '@/application/workspace/UseSourcingPreferences';
import SourcingViewSettings from './SourcingViewSettings.vue';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { AccessControls } from '@/application/workspace/UseAccess';
import { useMediaSummary, useSourcing, useSourcingNavigation } from '@/application/workspace/UseSourcing';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
import { sourcingPath, type MediaFilter, type SourcingLocation } from '@/domain/workspace/Sourcing';

import SourcingGallery from './SourcingGallery.vue';
import SourcingSupplierDetail from './SourcingSupplierDetail.vue';
import SourcingSupplierList from './SourcingSupplierList.vue';
import './Sourcing.css';

/** Records suppliers, their contact routes and shared media. It never contacts a supplier. */
defineOptions({ name: 'SourcingPage' });
const props = defineProps<{ access: AccessControls; workspace: WorkspaceControls; initialLocation?: SourcingLocation | undefined }>();
const { t } = useUiLocale();
const sourcing = useSourcing(props.access, props.workspace);
const viewSettings = useSourcingPreferences(props.access, sourcing.organizationId);
const preferences = viewSettings.preferences;
const settingsOpen = ref(false);
const initial = props.initialLocation ?? { view: 'suppliers' as const, supplierId: null };
const startsWithMaterials = initial.view === 'suppliers' && preferences.value.start === 'materials' &&
  window.location.pathname.replace(/\/$/, '') === sourcingPath(sourcing.organizationId(), initial);
const startingLocation: SourcingLocation = startsWithMaterials ? { view: 'materials', supplierId: null } : initial;
// The first history entry must describe the same view, so Back returns to the starting gallery.
if (startsWithMaterials) window.history.replaceState(window.history.state, '', sourcingPath(sourcing.organizationId(), startingLocation));
const navigation = useSourcingNavigation(props.access, sourcing.organizationId, startingLocation);
const current = navigation.current;
const { summary } = useMediaSummary(sourcing);
const inboxFilter: MediaFilter = { kind: 'inbox' };
const allFilter: MediaFilter = { kind: 'all' };
const showsDetail = computed(() => current.value.view !== 'suppliers');
function open(location: SourcingLocation): void { void navigation.open(location); }
async function openSettings(): Promise<void> {
  // Hiding a section must never unmount a form containing unsaved supplier edits.
  if (!viewSettings.canChange()) {
    notifyError(t('Save or cancel supplier edits before changing the view.'));
    return;
  }
  settingsOpen.value = true;
}

</script>

<template>
  <section class="space-y-6">
    <header class="flex flex-wrap items-start justify-between gap-4"><div>
      <p class="eyebrow">{{ t("Supplier sourcing") }}</p>
      <h1 class="page-title">{{ t("Sourcing") }}</h1>
      <p class="max-w-2xl text-muted-foreground">{{ t("Record material suppliers, the routes that reach them, and the photos and files they shared. Desk records outreach; it never sends messages.") }}</p>
    </div><Button variant="outline" :is-disabled="access.busy.value || !viewSettings.supported" @click="openSettings"><SlidersHorizontal :size="16" aria-hidden="true" />{{ t('View settings') }}</Button></header>
    <nav class="flex flex-wrap gap-2" :aria-label="t('Sourcing views')">
      <Button :variant="current.view !== 'materials' ? 'soft' : 'ghost'" :aria-current="current.view !== 'materials' ? 'page' : undefined" :is-disabled="access.busy.value" @click="open({ view: 'suppliers', supplierId: null })"><Users :size="16" aria-hidden="true" />{{ t('Suppliers') }}</Button>
      <Button :variant="current.view === 'materials' ? 'soft' : 'ghost'" :aria-current="current.view === 'materials' ? 'page' : undefined" :is-disabled="access.busy.value" @click="open({ view: 'materials', supplierId: null })"><Images :size="16" aria-hidden="true" />{{ t('Materials') }}</Button>
    </nav>
    <section v-if="current.view === 'materials'" class="space-y-4" aria-labelledby="sourcing-materials-title">
      <div><h2 id="sourcing-materials-title" class="text-xl font-semibold">{{ t('Shared materials and files') }}</h2><p class="mt-1 text-sm text-muted-foreground">{{ t('Browse supplier photos, PDFs and videos. Open a supplier from any linked file.') }}</p></div>
      <SourcingGallery :access="access" :sourcing="sourcing" :filter="allFilter" @open-supplier="id => open({ view: 'supplier', supplierId: id })" @open-inbox="open({ view: 'inbox', supplierId: null })" />
    </section>
    <div v-else class="sourcing-layout" :data-detail="String(showsDetail)">
      <aside class="sourcing-list-pane" :aria-label="t('Suppliers')">
        <SourcingSupplierList :access="access" :sourcing="sourcing" :current="current" :unassigned="summary?.unassigned ?? null" @open="open" />
      </aside>
      <div class="sourcing-detail-pane">
        <button v-if="showsDetail" type="button" class="sourcing-back mb-4 text-sm font-medium text-primary underline-offset-4 hover:underline disabled:opacity-50" :disabled="access.busy.value" @click="open({ view: 'suppliers', supplierId: null })">← {{ t("All suppliers") }}</button>
        <SourcingSupplierDetail v-if="current.view === 'supplier' && current.supplierId" :key="current.supplierId" :access="access" :sourcing="sourcing" :supplier-id="current.supplierId" :preferences="preferences" />
        <section v-else-if="current.view === 'inbox'" class="space-y-5" aria-labelledby="sourcing-inbox-title">
          <div class="workspace-card space-y-2 p-5 sm:p-6">
            <h2 id="sourcing-inbox-title" class="text-2xl font-semibold">{{ t("Inbox") }}</h2>
            <p class="text-sm leading-6 text-muted-foreground">{{ t("Drop photos, PDFs and videos here first, then sort them to suppliers. Nothing here is linked to a supplier yet.") }}</p>
          </div>
          <SourcingGallery :access="access" :sourcing="sourcing" :filter="inboxFilter" />
        </section>
        <p v-else class="editor-prompt">{{ t("Choose a supplier to see its contacts and media, or open the inbox to sort new files.") }}</p>
      </div>
    </div>
    <SourcingViewSettings v-if="settingsOpen" :initial="preferences" :save="viewSettings.save" @close="settingsOpen = false" />
  </section>
</template>
