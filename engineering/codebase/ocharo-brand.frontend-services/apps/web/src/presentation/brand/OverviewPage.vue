<script setup lang="ts">
import { ref } from 'vue';
import { RouterLink } from 'vue-router';
import { Send } from 'lucide-vue-next';
import { Icon } from '@wow-two-beta/fe/foundation/icons';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Badge, Card } from '@wow-two-beta/fe/presentation/display';
import { TextInput } from '@wow-two-beta/fe/presentation/forms';
import { useBrand } from '../../application/brand';
import { useIdentitySession } from '../../application/common/UseIdentitySession';
import { slotLabels, slots } from '../../domain/brand/BrandModels';
import { formatWhen } from '../../domain/brand/UzDates';
import LogoSwatch from './LogoSwatch.vue';

/** Where the brand stands: the published kit apps read, what is waiting to publish, and which apps follow along. */
defineOptions({ name: 'OverviewPage' });
const brand = useBrand();
const { canApprove } = useIdentitySession();
const note = ref('');
function when(value: string) {
  return formatWhen(value);
}
async function publish() {
  await brand.publish(note.value.trim() || null);
  note.value = '';
}
</script>

<template>
  <section class="space-y-6">
    <div>
      <h1 class="font-editorial text-4xl tracking-tight">Brend markazi</h1>
      <p class="mt-2 max-w-3xl text-sm text-muted-foreground">
        Logotip, ranglar va havolalar shu yerda bir marta o‘zgaradi. Nashr qilinganda Katalog, Marketing, Blog va boshqa
        ilovalar yangi brendni barqaror manzillar orqali oladi. Chop etilgan va eksport qilingan nusxalar o‘zgarmaydi.
      </p>
    </div>

    <Card v-bind="{ role: 'region', 'aria-labelledby': 'pending-heading' }" radius="2xl" padding="xl">
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h2 id="pending-heading" class="font-editorial text-2xl">Nashrga tayyor o‘zgarishlar</h2>
          <p class="mt-1 text-sm text-muted-foreground">
            Joriy nashr:
            {{ brand.kit.value ? `${brand.kit.value.release} · ${when(brand.kit.value.publishedAt)}` : 'hali yo‘q' }}
          </p>
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <TextInput v-model="note" class="w-64" placeholder="Nashr izohi (ixtiyoriy)" aria-label="Nashr izohi" />
          <Button
            tone="primary"
            :is-disabled="
              !canApprove ||
              brand.busy.value ||
              !brand.draft.value ||
              (brand.draft.value.latestRelease !== undefined && brand.pending.value.length === 0)
            "
            @click="publish"
          >
            <template #leading><Icon :icon="Send" :size="15" /></template>
            Nashr qilish
          </Button>
        </div>
      </div>
      <ul v-if="brand.pending.value.length" class="mt-4 flex flex-wrap gap-2">
        <li v-for="change in brand.pending.value" :key="change">
          <Badge variant="warning" size="sm">{{ change }}</Badge>
        </li>
      </ul>
      <p v-else class="mt-4 text-sm text-success">Hammasi nashr qilingan — ilovalar eng so‘nggi brendni ko‘rmoqda.</p>
    </Card>

    <Card v-if="brand.kit.value?.appearance" radius="2xl" padding="xl" class="border-primary/40 bg-primary-soft/40">
      <p class="text-xs font-semibold uppercase tracking-[0.2em] text-primary">Faol tadbir ko‘rinishi</p>
      <p class="mt-1 font-editorial text-2xl">{{ brand.kit.value.appearance.name }}</p>
      <p class="text-sm text-muted-foreground">
        {{ when(brand.kit.value.appearance.startsAt) }} — {{ when(brand.kit.value.appearance.endsAt) }}
      </p>
    </Card>

    <Card
      v-if="brand.kit.value"
      v-bind="{ role: 'region', 'aria-labelledby': 'live-heading' }"
      radius="2xl"
      padding="xl"
    >
      <div class="flex items-center justify-between">
        <h2 id="live-heading" class="font-editorial text-2xl">Ilovalar hozir ko‘rayotgan logotiplar</h2>
        <Button as-child variant="outline" tone="neutral">
          <RouterLink to="/logos">Logotiplarni boshqarish</RouterLink>
        </Button>
      </div>
      <div class="mt-4 grid gap-5 lg:grid-cols-2">
        <div v-for="slot in slots.filter((s) => brand.kit.value!.logos[s])" :key="slot">
          <p class="mb-2 text-sm font-semibold">
            {{ slotLabels[slot]!.label }}
            <Badge v-if="brand.kit.value.logos[slot]!.fromAppearance" variant="brand" size="sm" class="ml-1">
              tadbir
            </Badge>
          </p>
          <LogoSwatch :src="`${brand.urls.logo(slot)}?preview=true`" :alt="slotLabels[slot]!.label" />
        </div>
      </div>
      <div class="mt-5 flex flex-wrap gap-2">
        <span
          v-for="color in brand.kit.value.palette"
          :key="color.key"
          class="flex items-center gap-2 rounded-full border border-border px-3 py-1 text-xs"
        >
          <span class="h-4 w-4 rounded-full border border-border" :style="{ background: color.hex }"></span
          >{{ color.name }} · {{ color.hex }}
        </span>
      </div>
    </Card>

    <Card v-bind="{ role: 'region', 'aria-labelledby': 'consumers-heading' }" radius="2xl" padding="xl">
      <h2 id="consumers-heading" class="font-editorial text-2xl">Brendni o‘qiyotgan ilovalar</h2>
      <p v-if="!brand.consumers.value.length" class="mt-2 text-sm text-muted-foreground">
        Hali hech bir ilova o‘zini tanitmagan. Ilovalar <code>X-Ocharo-App</code> sarlavhasi yoki
        <code>?app=</code> bilan so‘raydi.
      </p>
      <ul class="mt-3 grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
        <li v-for="c in brand.consumers.value" :key="c.app" class="rounded-xl border border-border p-3 text-sm">
          <p class="font-semibold">{{ c.app }}</p>
          <p class="text-xs text-muted-foreground">{{ when(c.lastSeenAt) }} · nashr {{ c.lastRelease }}</p>
          <Badge :variant="c.current ? 'success' : 'warning'" size="sm" class="mt-1">
            {{ c.current ? 'eng so‘nggi' : 'eski nashr' }}
          </Badge>
        </li>
      </ul>
    </Card>

    <Card
      v-if="brand.releases.value.length"
      v-bind="{ role: 'region', 'aria-labelledby': 'releases-heading' }"
      radius="2xl"
      padding="xl"
    >
      <h2 id="releases-heading" class="font-editorial text-2xl">Nashrlar tarixi</h2>
      <ol class="mt-3 space-y-2 text-sm">
        <li v-for="r in brand.releases.value" :key="r.version" class="rounded-lg border border-border px-3 py-2">
          <p class="font-semibold">{{ r.version }}-nashr · {{ when(r.publishedAt) }}</p>
          <p class="text-xs text-muted-foreground">{{ r.note }}</p>
          <p class="mt-1 text-xs">{{ r.changes.join(' · ') }}</p>
        </li>
      </ol>
    </Card>
  </section>
</template>
