<script setup lang="ts">
import { computed } from 'vue';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import type { AccessControls } from '@/application/workspace/UseAccess';
import { useMediaSummary, useSourcing, useSourcingNavigation } from '@/application/workspace/UseSourcing';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
import type { MediaFilter, SourcingLocation } from '@/domain/workspace/Sourcing';

import SourcingGallery from './SourcingGallery.vue';
import SourcingSupplierDetail from './SourcingSupplierDetail.vue';
import SourcingSupplierList from './SourcingSupplierList.vue';
import './Sourcing.css';

/** Records suppliers, their contact routes and shared media. It never contacts a supplier. */
defineOptions({ name: 'SourcingPage' });
const props = defineProps<{ access: AccessControls; workspace: WorkspaceControls; initialLocation?: SourcingLocation | undefined }>();
const { t } = useUiLocale();
const sourcing = useSourcing(props.access, props.workspace);
const navigation = useSourcingNavigation(props.access, sourcing.organizationId, props.initialLocation ?? { view: 'suppliers', supplierId: null });
const current = navigation.current;
const { summary } = useMediaSummary(sourcing);
const inboxFilter: MediaFilter = { kind: 'inbox' };
const showsDetail = computed(() => current.value.view !== 'suppliers');
function open(location: SourcingLocation): void { void navigation.open(location); }
</script>

<template>
  <section class="space-y-6">
    <header>
      <p class="eyebrow">{{ t("Supplier sourcing") }}</p>
      <h1 class="page-title">{{ t("Sourcing") }}</h1>
      <p class="max-w-2xl text-muted-foreground">{{ t("Record material suppliers, the routes that reach them, and the photos and files they shared. Desk records outreach; it never sends messages.") }}</p>
    </header>
    <div class="sourcing-layout" :data-detail="String(showsDetail)">
      <aside class="sourcing-list-pane" :aria-label="t('Suppliers')">
        <SourcingSupplierList :access="access" :sourcing="sourcing" :current="current" :unassigned="summary?.unassigned ?? null" @open="open" />
      </aside>
      <div class="sourcing-detail-pane">
        <button v-if="showsDetail" type="button" class="sourcing-back mb-4 text-sm font-medium text-primary underline-offset-4 hover:underline disabled:opacity-50" :disabled="access.busy.value" @click="open({ view: 'suppliers', supplierId: null })">← {{ t("All suppliers") }}</button>
        <SourcingSupplierDetail v-if="current.view === 'supplier' && current.supplierId" :key="current.supplierId" :access="access" :sourcing="sourcing" :supplier-id="current.supplierId" />
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
  </section>
</template>
