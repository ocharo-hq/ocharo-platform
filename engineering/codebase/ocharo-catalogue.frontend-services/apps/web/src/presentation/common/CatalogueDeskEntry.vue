<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';

/** Opens canonical catalogue authoring without replacing this retained pilot or its unsaved fields. */
defineOptions({ name: 'CatalogueDeskEntry' });
defineProps<{ deskUrl: string | null }>();
</script>

<template>
  <section class="no-print mb-6 rounded-2xl border border-border bg-card p-5" aria-label="Ocharo Desk">
    <div class="flex flex-wrap items-center justify-between gap-4">
      <div class="max-w-2xl">
        <h2 class="text-base font-semibold">Katalog dizayneri — Ocharo Desk</h2>
        <p class="mt-2 text-sm leading-6 text-muted-foreground">
          Bu saqlangan dastlabki muharrir. Katalog dizayni va nashrlarini Ocharo Desk orqali boshqaring. Yangi oyna bu
          yerdagi saqlanmagan o‘zgarishlarni yopmaydi.
        </p>
      </div>
      <Button v-if="deskUrl" as-child class="min-h-11">
        <a :href="deskUrl" target="_blank" rel="noopener noreferrer">Katalog dizaynerini ochish ↗</a>
      </Button>
      <p v-else class="text-sm text-muted-foreground">Ocharo Desk havolasi sozlanmagan.</p>
    </div>
  </section>
</template>
