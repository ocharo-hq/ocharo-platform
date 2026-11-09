<script setup lang="ts">
import { useIdentitySession } from '../../application/common/UseIdentitySession';
const { canEdit: sessionCanEdit, canApprove: sessionCanApprove } = useIdentitySession();
import { computed } from 'vue';
import { useUiLocale } from '../../application/shared/UseUiLocale';
import type { CatalogueDocumentControls } from '../../application/design/UseCatalogueDocument';
import { catalogueLanguages, type CatalogueLanguage } from '../../domain/catalogue/CatalogueLanguages';
import { catalogueSettings, defaultCopy, refreshCatalogue } from '../../domain/design/CatalogueDesign';
import { localizeOriginalCopy } from '../../domain/design/OriginalCatalogue';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Card } from '@wow-two-beta/fe/presentation/display';
import { Field, TextInput } from '@wow-two-beta/fe/presentation/forms';
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
    <Button as-child variant="outline" tone="neutral">
      <RouterLink to="/catalogues">← {{ t('Your catalogues') }}</RouterLink>
    </Button>
    <Field :label="t('Name')" class="title-field">
      <TextInput
        :model-value="editor.title.value"
        maxlength="160"
        :is-disabled="editor.busy.value"
        @update:model-value="editor.setTitle"
      />
    </Field>
    <Field :label="t('Catalogue language')">
      <CatalogueSelect
        :model-value="editor.snapshot.value?.locale ?? 'uz'"
        :options="languages"
        :label="t('Catalogue language')"
        :disabled="editor.busy.value"
        @update:model-value="locale"
      />
    </Field>
    <div class="actions">
      <Button
        variant="outline"
        tone="neutral"
        :is-disabled="!editor.canUndo.value || editor.busy.value"
        @click="editor.undo"
      >
        {{ t('Undo') }}</Button
      ><Button
        variant="outline"
        tone="neutral"
        :is-disabled="!editor.canRedo.value || editor.busy.value"
        @click="editor.redo"
      >
        {{ t('Redo') }}</Button
      ><Button :is-disabled="!sessionCanEdit || editor.busy.value || !editor.title.value.trim()" @click="editor.save">
        {{ editor.busy.value ? t('Working…') : t('Save catalogue') }}
      </Button>
    </div>
    <p class="status">
      {{ editor.dirty.value ? t('Unsaved changes') : t('Saved') }} · {{ t('Revision') }}
      {{ editor.document.value?.revision }}
    </p>
    <nav class="actions">
      <Button
        v-if="mode === 'web' && editor.snapshot.value?.catalogue?.outputs.pdf"
        as-child
        variant="outline"
        tone="neutral"
      >
        <RouterLink :to="`/catalogues/${editor.document.value?.id}/pdf`">{{ t('PDF designer') }}</RouterLink></Button
      ><Button
        v-if="mode === 'pdf' && editor.snapshot.value?.catalogue?.outputs.web"
        as-child
        variant="outline"
        tone="neutral"
      >
        <RouterLink :to="`/catalogues/${editor.document.value?.id}/web`">{{ t('Web designer') }}</RouterLink></Button
      ><Button variant="outline" tone="neutral" :is-disabled="editor.busy.value" @click="editor.refresh">
        {{ t('Refresh product fields') }}
      </Button>
    </nav>
  </header>
  <Card v-if="editor.recovery.value" radius="2xl" padding="xl" class="recovery">
    <p>{{ t('Unsaved changes from this browser tab are available.') }}</p>
    <Button variant="outline" tone="neutral" @click="editor.restoreRecovery">{{ t('Restore recovery copy') }}</Button
    ><Button variant="ghost" tone="neutral" @click="editor.dismissRecovery">{{ t('Dismiss') }}</Button>
  </Card>
  <p v-if="editor.notice.value" role="status" class="notice">{{ editor.notice.value }}</p>
  <Card radius="2xl" padding="xl" class="publication"
    ><details>
      <summary>{{ t('Publication and saved history') }}</summary>
      <p>
        {{
          t(
            'Changes affect the working catalogue. Published editions change only after explicit preparation and publication.',
          )
        }}
      </p>
      <div class="actions">
        <Button
          variant="outline"
          tone="neutral"
          :is-disabled="editor.busy.value || editor.dirty.value"
          @click="editor.prepare"
        >
          {{ t('Prepare release') }}</Button
        ><Button
          :is-disabled="!sessionCanApprove || editor.busy.value || editor.dirty.value || !editor.prepared.value"
          @click="editor.publish"
        >
          {{ t('Publish catalogue') }}</Button
        ><Button
          variant="outline"
          tone="neutral"
          :is-disabled="!sessionCanApprove || editor.busy.value || editor.dirty.value"
          @click="editor.withdraw"
        >
          {{ t('Withdraw catalogue') }}</Button
        ><Button v-if="link" as-child variant="outline" tone="neutral">
          <RouterLink :to="link">{{ t('Open public catalogue') }}</RouterLink></Button
        ><Button variant="outline" tone="neutral" :is-disabled="editor.busy.value" @click="editor.history">
          {{ t('Load saved history') }}
        </Button>
      </div>
      <ol v-if="editor.revisions.value.length">
        <li v-for="revision in editor.revisions.value" :key="revision.revision">
          <span>{{ t('Revision') }} {{ revision.revision }} · {{ revision.title }}</span
          ><Button
            variant="ghost"
            tone="neutral"
            size="sm"
            :is-disabled="!sessionCanEdit || editor.busy.value"
            @click="editor.restore(revision.revision)"
          >
            {{ t('Restore revision') }}
          </Button>
        </li>
      </ol>
      <ul>
        <li v-for="release in editor.releases.value" :key="release.id">
          {{ t('Revision') }} {{ release.designRevision }} ·
          {{ release.withdrawnAtUtc ? t('Withdrawn') : release.publishedAtUtc ? t('Published') : t('Prepared') }}
        </li>
      </ul>
    </details></Card
  >
  <Card
    v-if="editor.preflight.value?.issues.length"
    v-bind="{ role: 'region', 'aria-labelledby': 'export-check-heading' }"
    radius="2xl"
    padding="xl"
  >
    <h2 id="export-check-heading">{{ t('Export check') }}</h2>
    <ul>
      <li v-for="(issue, index) in editor.preflight.value.issues" :key="index">
        {{ issue.pageTitle }} / {{ issue.elementLabel }}: {{ t(issue.message, issue.variables) }}
      </li>
    </ul>
  </Card>
</template>
<style scoped>
.designer-bar {
  display: flex;
  align-items: end;
  flex-wrap: wrap;
  gap: 16px;
  margin-bottom: 20px;
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
