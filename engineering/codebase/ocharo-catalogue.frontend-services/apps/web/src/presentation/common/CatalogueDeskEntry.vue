<script setup lang="ts">
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
      <a v-if="deskUrl" :href="deskUrl" target="_blank" rel="noopener noreferrer" class="btn-primary min-h-11"
        >Katalog dizaynerini ochish ↗</a
      >
      <p v-else class="text-sm text-muted-foreground">Ocharo Desk havolasi sozlanmagan.</p>
    </div>
  </section>
</template>
