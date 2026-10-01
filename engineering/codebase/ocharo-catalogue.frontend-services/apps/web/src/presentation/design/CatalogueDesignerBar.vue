<script setup lang="ts">
import { computed } from 'vue';
import { useUiLocale } from '../../application/shared/UseUiLocale';
import type { CatalogueDocumentControls } from '../../application/design/UseCatalogueDocument';
import { catalogueLanguages, type CatalogueLanguage } from '../../domain/catalogue/CatalogueLanguages';
import { catalogueSettings, defaultCopy, refreshCatalogue } from '../../domain/design/CatalogueDesign';
import { localizeOriginalCopy } from '../../domain/design/OriginalCatalogue';
import CatalogueSelect from '../common/CatalogueSelect.vue';
defineOptions({ name: 'CatalogueDesignerBar' });
const props = defineProps<{ editor: CatalogueDocumentControls; mode: 'web' | 'pdf' }>(),
  { t } = useUiLocale();
const languages = catalogueLanguages.map((item) => ({ value: item.code, label: item.label }));
const link = computed(() =>
  props.editor.document.value
    ? `/c/${props.editor.snapshot.value?.catalogue?.slug}?locale=${props.editor.snapshot.value?.locale}`
    : '',
);
function locale(value: string) {
  const snapshot = props.editor.snapshot.value;
  if (!snapshot || !props.editor.source.value) return;
  const next = refreshCatalogue(
    localizeOriginalCopy(snapshot, value as CatalogueLanguage),
    props.editor.source.value.products,
    value as CatalogueLanguage,
  );
  next.catalogueSettings = catalogueSettings(next);
  next.catalogueSettings.copyByLocale[value as CatalogueLanguage] ??= defaultCopy(value as CatalogueLanguage);
  props.editor.replace(next);
}
</script>
<template>
  <header class="designer-bar">
    <RouterLink to="/catalogues" class="btn-outline">← {{ t('Your catalogues') }}</RouterLink>
    <label class="title-field"
      >{{ t('Name')
      }}<input
        :value="editor.title.value"
        class="field"
        maxlength="160"
        :disabled="editor.busy.value"
        @input="editor.setTitle(($event.target as HTMLInputElement).value)"
    /></label>
    <label
      >{{ t('Catalogue language')
      }}<CatalogueSelect
        :model-value="editor.snapshot.value?.locale ?? 'uz'"
        :options="languages"
        :label="t('Catalogue language')"
        :disabled="editor.busy.value"
        @update:model-value="locale"
    /></label>
    <div class="actions">
      <button class="btn-outline" :disabled="!editor.canUndo.value || editor.busy.value" @click="editor.undo">
        {{ t('Undo') }}</button
      ><button class="btn-outline" :disabled="!editor.canRedo.value || editor.busy.value" @click="editor.redo">
        {{ t('Redo') }}</button
      ><button class="btn-primary" :disabled="editor.busy.value || !editor.title.value.trim()" @click="editor.save">
        {{ editor.busy.value ? t('Working…') : t('Save catalogue') }}
      </button>
    </div>
    <p class="status">
      {{ editor.dirty.value ? t('Unsaved changes') : t('Saved') }} · {{ t('Revision') }}
      {{ editor.document.value?.revision }}
    </p>
    <nav class="actions">
      <RouterLink
        v-if="mode === 'web' && editor.snapshot.value?.catalogue?.outputs.pdf"
        :to="`/catalogues/${editor.document.value?.id}/pdf`"
        class="btn-outline"
        >{{ t('PDF designer') }}</RouterLink
      ><RouterLink
        v-if="mode === 'pdf' && editor.snapshot.value?.catalogue?.outputs.web"
        :to="`/catalogues/${editor.document.value?.id}/web`"
        class="btn-outline"
        >{{ t('Web designer') }}</RouterLink
      ><button class="btn-outline" :disabled="editor.busy.value" @click="editor.refresh">
        {{ t('Refresh product fields') }}
      </button>
    </nav>
  </header>
  <div v-if="editor.recovery.value" class="panel recovery">
    <p>{{ t('Unsaved changes from this browser tab are available.') }}</p>
    <button class="btn-outline" @click="editor.restoreRecovery">{{ t('Restore recovery copy') }}</button
    ><button class="btn-ghost" @click="editor.dismissRecovery">{{ t('Dismiss') }}</button>
  </div>
  <p v-if="editor.notice.value" role="status" class="notice">{{ editor.notice.value }}</p>
  <p v-if="editor.error.value" role="status" class="error">{{ t(editor.error.value) }}</p>
  <details class="panel publication">
    <summary>{{ t('Publication and saved history') }}</summary>
    <p>
      {{
        t(
          'Changes affect the working catalogue. Published editions change only after explicit preparation and publication.',
        )
      }}
    </p>
    <div class="actions">
      <button class="btn-outline" :disabled="editor.busy.value || editor.dirty.value" @click="editor.prepare">
        {{ t('Prepare release') }}</button
      ><button
        class="btn-primary"
        :disabled="editor.busy.value || editor.dirty.value || !editor.prepared.value"
        @click="editor.publish"
      >
        {{ t('Publish catalogue') }}</button
      ><button class="btn-outline" :disabled="editor.busy.value || editor.dirty.value" @click="editor.withdraw">
        {{ t('Withdraw catalogue') }}</button
      ><RouterLink v-if="link" :to="link" class="btn-outline">{{ t('Open public catalogue') }}</RouterLink
      ><button class="btn-outline" :disabled="editor.busy.value" @click="editor.history">
        {{ t('Load saved history') }}
      </button>
    </div>
    <ol v-if="editor.revisions.value.length">
      <li v-for="revision in editor.revisions.value" :key="revision.revision">
        <span>{{ t('Revision') }} {{ revision.revision }} · {{ revision.title }}</span
        ><button class="btn-ghost" :disabled="editor.busy.value" @click="editor.restore(revision.revision)">
          {{ t('Restore revision') }}
        </button>
      </li>
    </ol>
    <ul>
      <li v-for="release in editor.releases.value" :key="release.id">
        {{ t('Revision') }} {{ release.designRevision }} ·
        {{ release.withdrawnAtUtc ? t('Withdrawn') : release.publishedAtUtc ? t('Published') : t('Prepared') }}
      </li>
    </ul>
  </details>
  <section v-if="editor.preflight.value?.issues.length" class="panel">
    <h2>{{ t('Export check') }}</h2>
    <ul>
      <li v-for="(issue, index) in editor.preflight.value.issues" :key="index">
        {{ issue.pageTitle }} / {{ issue.elementLabel }}: {{ t(issue.message, issue.variables) }}
      </li>
    </ul>
  </section>
</template>
<style scoped>
.designer-bar {
  display: flex;
  align-items: end;
  flex-wrap: wrap;
  gap: 16px;
  margin-bottom: 20px;
}
.designer-bar label {
  display: grid;
  gap: 6px;
  font-size: 12px;
}
.title-field {
  flex: 1;
  min-width: 190px;
}
.actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  align-items: center;
}
.status {
  font-size: 12px;
  color: var(--color-muted-foreground);
  width: 100%;
}
.publication {
  margin: 16px 0;
}
.publication summary {
  cursor: pointer;
  font-weight: 600;
}
.publication p {
  font-size: 13px;
  margin: 15px 0;
  max-width: 800px;
  color: var(--color-muted-foreground);
}
.publication li {
  display: flex;
  justify-content: space-between;
  border-top: 1px solid var(--color-border);
  padding: 8px;
}
.notice,
.error {
  font-size: 14px;
  padding: 10px 0;
}
.error {
  color: var(--color-destructive);
}
.recovery {
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
}
</style>
