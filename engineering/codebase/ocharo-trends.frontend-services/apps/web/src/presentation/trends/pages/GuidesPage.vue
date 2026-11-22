<script setup lang="ts">
import { computed, ref } from 'vue';
import { RouterLink } from 'vue-router';
import { ArrowUpRight, Film } from 'lucide-vue-next';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { useGuides } from '../../../application/trends/UseTrends';
import type { GuideKind } from '../../../domain/trends/Trends';

const { guides, loading, error, load } = useGuides();
const kind = ref<GuideKind | ''>('');
const query = ref('');
const filteredGuides = computed(() =>
  guides.value.filter((guide) => {
    const needle = query.value.trim().toLocaleLowerCase();
    return (
      (!kind.value || guide.kind === kind.value) &&
      `${guide.title} ${guide.summary}`.toLocaleLowerCase().includes(needle)
    );
  }),
);
</script>

<template>
  <section class="board-page">
    <header class="board-heading">
      <h1 class="sr-only">Trends</h1>
      <p>Explore video formats, camera moves and transitions with original examples.</p>
    </header>
    <p v-if="loading" role="status" class="board-state">Loading guides…</p>
    <div v-else-if="error" role="alert" class="board-state">
      <p>Could not load the guides.</p>
      <Button tone="primary" @click="load">Retry</Button>
    </div>
    <div v-else-if="!guides.length" class="board-state">
      <Film aria-hidden="true" :size="28" />
      <h2>No published guides yet</h2>
    </div>
    <template v-else>
      <div class="board-filters guides-filters">
        <label>Search guides <input v-model="query" type="search" placeholder="Search by title or technique" /></label>
        <label
          >Type
          <select v-model="kind">
            <option value="">All guides</option>
            <option value="trend">Trends</option>
            <option value="transition">Transitions</option>
          </select>
        </label>
      </div>
      <ul v-if="filteredGuides.length" class="guide-grid">
        <li v-for="guide in filteredGuides" :key="guide.slug" class="board-card">
          <span class="board-type">{{ guide.kind }}</span>
          <RouterLink :to="{ name: 'guide', params: { slug: guide.slug } }" class="guide-link">
            <h2>{{ guide.title }}</h2>
            <ArrowUpRight aria-hidden="true" :size="19" />
          </RouterLink>
          <p>{{ guide.summary }}</p>
        </li>
      </ul>
      <p v-else class="board-state">No guides match those filters.</p>
    </template>
  </section>
</template>
