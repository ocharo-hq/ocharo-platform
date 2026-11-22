<script setup lang="ts">
import { computed, ref, toRaw, watch } from 'vue';
import { Plus, Save, Trash2 } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/fe/foundation/icons';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Card, LabelText } from '@wow-two-beta/fe/presentation/display';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import { ColorInput, ColorPicker, Field, TextAreaInput, TextInput } from '@wow-two-beta/fe/presentation/forms';
import { useBrand } from '../../application/brand';
import { useIdentitySession } from '../../application/common/UseIdentitySession';
import type { BrandIdentity } from '../../domain/brand/BrandModels';
import IdentityFormSkeleton from '../common/IdentityFormSkeleton.vue';

/** Name, spelling rule, descriptor/tagline/location in three languages, palette, type and official links. */
defineOptions({ name: 'IdentityPage' });
const brand = useBrand();
const { canEdit } = useIdentitySession();
const form = ref<BrandIdentity | null>(null);
const baseline = ref('');
const baselineRevision = ref<number | null>(null);
const saving = ref(false);
watch(
  () => brand.draft.value,
  (view) => {
    if (!view || (form.value && JSON.stringify(form.value) !== baseline.value)) return;
    const identity = view.draft.identity;
    form.value = structuredClone(identity);
    baseline.value = JSON.stringify(identity);
    baselineRevision.value = view.revision;
  },
  { immediate: true },
);
const dirty = computed(() => form.value !== null && JSON.stringify(form.value) !== baseline.value);
async function save() {
  if (!form.value || baselineRevision.value === null || brand.busy.value || saving.value) return;
  const submitted = structuredClone(toRaw(form.value));
  const expectedRevision = baselineRevision.value;
  saving.value = true;
  try {
    const acknowledged = await brand.saveIdentity(submitted, expectedRevision);
    if (acknowledged) {
      baseline.value = JSON.stringify(submitted);
      baselineRevision.value = acknowledged.revision;
    }
  } finally {
    saving.value = false;
  }
}
const locales = [
  { code: 'uz', label: 'Uzbek' },
  { code: 'ru', label: 'Russian' },
  { code: 'en', label: 'English' },
] as const;
const texts = [
  { key: 'descriptor', label: 'Description (card front)' },
  { key: 'tagline', label: 'Tagline' },
  { key: 'location', label: 'Location' },
] as const;
</script>

<template>
  <section class="space-y-6 pb-20">
    <div class="flex flex-wrap items-center justify-between gap-4">
      <div>
        <h1 class="sr-only">Identity</h1>
        <p class="text-sm text-muted-foreground">Saved to the draft; the apps see it after the next release.</p>
      </div>
      <Button tone="primary" :is-disabled="!canEdit || !dirty || brand.busy.value" :is-loading="saving" @click="save">
        <template #leading><Icon :icon="Save" :size="15" /></template>
        Save
      </Button>
    </div>

    <template v-if="form">
      <Card radius="2xl" padding="xl">
        <div class="grid gap-4 md:grid-cols-[240px_1fr]">
          <Field label="Brand name"><TextInput v-model="form.name" name="name" class="text-lg" /></Field>
          <Field label="Spelling rule"><TextAreaInput v-model="form.spellingRule" :rows="2" /></Field>
        </div>
      </Card>

      <Card radius="2xl" padding="xl">
        <h2 class="font-editorial text-2xl">Texts</h2>
        <div v-for="text in texts" :key="text.key" class="mt-4">
          <LabelText class="mb-1.5">{{ text.label }}</LabelText>
          <div class="grid gap-3 md:grid-cols-3">
            <TextInput
              v-for="l in locales"
              :key="l.code"
              v-model="form[text.key][l.code]"
              :placeholder="l.label"
              :aria-label="`${text.label} — ${l.label}`"
            />
          </div>
        </div>
        <Field label="Voice and style notes" class="mt-4">
          <TextAreaInput v-model="form.voiceNotes" :rows="3" />
        </Field>
      </Card>

      <Card radius="2xl" padding="xl">
        <div class="flex items-center justify-between">
          <h2 class="font-editorial text-2xl">Colors</h2>
          <Button
            variant="outline"
            tone="neutral"
            @click="form.palette.push({ key: 'new', name: 'New color', hex: '#888888', role: 'accent' })"
          >
            <template #leading><Icon :icon="Plus" :size="14" /></template>
            Color
          </Button>
        </div>
        <p class="mt-1 text-xs text-muted-foreground">
          Each key becomes a <code>--ocharo-{key}</code> CSS token in the apps.
        </p>
        <ul class="mt-4 grid gap-3 md:grid-cols-2">
          <li
            v-for="(color, index) in form.palette"
            :key="index"
            class="flex items-center gap-3 rounded-xl border border-border p-3"
          >
            <ColorPicker
              :model-value="color.hex"
              trigger-variant="swatch"
              v-bind="{ 'aria-label': `Pick the color of ${color.name}` }"
              @update:model-value="color.hex = $event ?? color.hex"
            />
            <div class="grid flex-1 gap-2 sm:grid-cols-3">
              <TextInput v-model="color.key" class="font-mono text-xs" aria-label="Key" />
              <TextInput v-model="color.name" aria-label="Name" />
              <ColorInput
                :model-value="color.hex"
                class="font-mono text-xs"
                :aria-label="`${color.name} color`"
                @update:model-value="color.hex = $event ?? color.hex"
              />
            </div>
            <Button
              variant="ghost"
              tone="danger"
              shape="square"
              aria-label="Delete color"
              @click="form.palette.splice(index, 1)"
            >
              <Icon :icon="Trash2" :size="14" />
            </Button>
          </li>
        </ul>
      </Card>

      <Card radius="2xl" padding="xl">
        <h2 class="font-editorial text-2xl">Fonts</h2>
        <div class="mt-4 grid gap-4 md:grid-cols-3">
          <Field label="Heading font"><TextInput v-model="form.typography.display" /></Field>
          <Field label="Body font"><TextInput v-model="form.typography.body" /></Field>
          <Field label="Note"><TextInput v-model="form.typography.notes" /></Field>
        </div>
        <p class="mt-3 text-sm">
          <span :style="{ fontFamily: form.typography.display }" class="text-2xl">{{ form.name }}</span>
          <SkeletonState v-if="brand.refreshing.value && !dirty" class="ml-3 inline-block h-5 w-48 align-middle" />
          <span v-else :style="{ fontFamily: form.typography.body }" class="ml-3 text-muted-foreground">{{
            form.descriptor.en || form.descriptor.uz
          }}</span>
        </p>
      </Card>

      <Card radius="2xl" padding="xl">
        <div class="flex items-center justify-between">
          <h2 class="font-editorial text-2xl">Official links</h2>
          <Button
            variant="outline"
            tone="neutral"
            @click="form.links.push({ key: 'link', label: '', url: 'https://' })"
          >
            <template #leading><Icon :icon="Plus" :size="14" /></template>
            Link
          </Button>
        </div>
        <p class="mt-1 text-xs text-muted-foreground">
          Keeps account names exactly as written (for example, ocharostile, Ocharo_Style). Use <code>tel:</code> for a
          phone number.
        </p>
        <ul class="mt-4 space-y-2">
          <li
            v-for="(link, index) in form.links"
            :key="index"
            class="grid gap-2 md:grid-cols-[120px_160px_180px_1fr_auto]"
          >
            <TextInput v-model="link.key" class="font-mono text-xs" aria-label="Key" />
            <TextInput v-model="link.label" aria-label="Name" />
            <TextInput
              :model-value="link.handle ?? ''"
              placeholder="handle"
              aria-label="Handle"
              @update:model-value="link.handle = $event"
            />
            <TextInput v-model="link.url" class="font-mono text-xs" aria-label="URL" />
            <Button
              variant="ghost"
              tone="danger"
              shape="square"
              aria-label="Delete link"
              @click="form.links.splice(index, 1)"
            >
              <Icon :icon="Trash2" :size="14" />
            </Button>
          </li>
        </ul>
      </Card>
    </template>
    <IdentityFormSkeleton v-else-if="brand.queryState.value === 'loading'" />
    <div v-else class="brand-query-region text-sm text-muted-foreground">
      <p>
        {{
          brand.queryState.value === 'unavailable'
            ? 'Identity data is not available right now.'
            : 'No identity data yet.'
        }}
      </p>
    </div>
  </section>
</template>
