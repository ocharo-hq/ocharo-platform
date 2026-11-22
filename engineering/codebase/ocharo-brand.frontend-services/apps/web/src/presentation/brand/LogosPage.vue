<script setup lang="ts">
import { ref } from 'vue';
import { Check, Upload } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/fe/foundation/icons';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Badge, Card } from '@wow-two-beta/fe/presentation/display';
import { TextInput } from '@wow-two-beta/fe/presentation/forms';
import { useBrand } from '../../application/brand';
import { useIdentitySession } from '../../application/common/UseIdentitySession';
import { slotLabels, slots } from '../../domain/brand/BrandModels';
import LogoSwatch from './LogoSwatch.vue';

/** Every logo slot: its current (draft) file, all uploaded versions, and uploads with a note. */
defineOptions({ name: 'LogosPage' });
const brand = useBrand();
const { canEdit } = useIdentitySession();
const notes = ref<Record<string, string>>({});
const inputs = ref<Record<string, HTMLInputElement | null>>({});
async function onFile(slot: string, event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0];
  if (!file) return;
  const version = await brand.upload(slot, file, notes.value[slot]?.trim() || null);
  if (version) notes.value[slot] = '';
  const input = inputs.value[slot];
  if (input) input.value = '';
}
function size(bytes: number) {
  return bytes > 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.round(bytes / 1024)} KB`;
}
</script>

<template>
  <section class="space-y-6">
    <div>
      <h1 class="sr-only">Logos</h1>
      <p class="max-w-3xl text-sm text-muted-foreground">
        Upload a new file for each slot and press “Make current”. The change stays in the draft; when “Publish” is
        pressed on the Overview page, every app gets the new logo. PNG, JPEG and WebP only.
      </p>
    </div>
    <Card
      v-for="slot in slots"
      :key="slot"
      v-bind="{ role: 'region', 'aria-labelledby': `slot-${slot}` }"
      radius="2xl"
      padding="xl"
    >
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h2 :id="`slot-${slot}`" class="font-editorial text-2xl">{{ slotLabels[slot]!.label }}</h2>
          <p class="text-sm text-muted-foreground">
            {{ slotLabels[slot]!.hint }} · <code class="text-xs">{{ brand.urls.logo(slot) }}</code>
          </p>
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <TextInput
            :model-value="notes[slot] ?? ''"
            class="w-56"
            placeholder="Note (optional)"
            :aria-label="`${slotLabels[slot]!.label} note`"
            @update:model-value="notes[slot] = $event"
          />
          <Button
            variant="outline"
            tone="neutral"
            :is-disabled="!canEdit || brand.busy.value"
            @click="inputs[slot]?.click()"
          >
            <template #leading><Icon :icon="Upload" :size="14" /></template>
            Upload
          </Button>
          <input
            :ref="(el) => (inputs[slot] = el as HTMLInputElement | null)"
            type="file"
            accept="image/png,image/jpeg,image/webp"
            class="hidden"
            @change="onFile(slot, $event)"
          />
        </div>
      </div>
      <div v-if="brand.currentVersion(slot)" class="mt-4">
        <LogoSwatch
          :src="brand.urls.media(brand.currentVersion(slot)!.sha256, brand.currentVersion(slot)!.hasPreview)"
          :alt="slotLabels[slot]!.label"
        />
      </div>
      <p v-else class="mt-4 rounded-xl border border-dashed border-border p-4 text-sm text-muted-foreground">
        No file is chosen for this slot{{
          slot === 'on-light' || slot === 'social'
            ? ' — the master logo is used'
            : slot === 'favicon'
              ? ' — the profile picture or the on-light logo is used'
              : ''
        }}.
      </p>
      <ul v-if="brand.versionsFor(slot).length" class="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        <li
          v-for="v in brand.versionsFor(slot)"
          :key="v.id"
          :class="[
            'flex gap-3 rounded-xl border p-3',
            brand.draft.value?.draft.currentLogos[slot] === v.id
              ? 'border-primary bg-primary-soft/30'
              : 'border-border',
          ]"
        >
          <img
            :src="brand.urls.media(v.sha256, v.hasPreview)"
            alt=""
            class="h-16 w-20 shrink-0 rounded bg-artwork-paper object-contain p-1"
            loading="lazy"
          />
          <div class="min-w-0 flex-1 text-xs">
            <p class="truncate font-semibold">{{ v.fileName }}</p>
            <p class="text-muted-foreground">{{ v.width }}×{{ v.height }} · {{ size(v.byteLength) }}</p>
            <p v-if="v.note" class="mt-1 line-clamp-2 text-muted-foreground">{{ v.note }}</p>
            <div class="mt-2 flex flex-wrap items-center gap-1">
              <Badge :variant="v.status === 'approved' ? 'success' : 'neutral'" size="sm">
                {{ v.status === 'approved' ? 'approved' : 'draft' }}
              </Badge>
              <Button
                v-if="brand.draft.value?.draft.currentLogos[slot] !== v.id"
                tone="primary"
                size="xs"
                :is-disabled="!canEdit || brand.busy.value"
                @click="brand.setCurrent(slot, v.id)"
              >
                Make current
              </Button>
              <Badge v-else variant="brand" size="sm">
                <template #leading><Icon :icon="Check" :size="11" /></template>
                current
              </Badge>
              <Button
                tone="neutral"
                size="xs"
                :is-disabled="!canEdit || brand.busy.value"
                @click="brand.setStatus(v.id, v.status === 'approved' ? 'draft' : 'approved')"
              >
                {{ v.status === 'approved' ? 'Remove approval' : 'Approve' }}
              </Button>
            </div>
          </div>
        </li>
      </ul>
    </Card>
  </section>
</template>
