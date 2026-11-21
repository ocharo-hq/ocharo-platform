<script setup lang="ts">
import { computed } from 'vue';
import { RouterLink } from 'vue-router';
import { ArrowLeft, ArrowUpRight } from 'lucide-vue-next';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import type { GuideExample } from '../../../domain/trends/Trends';
import { useGuides } from '../../../application/trends/UseTrends';

const props = defineProps<{ slug: string }>();
const { guides, loading, error, load } = useGuides();
const guide = computed(() => guides.value.find(({ slug }) => slug === props.slug));

function exampleLink(example: GuideExample) {
  const link = new URL(example.url);
  if (example.startSeconds != null && (link.hostname === 'www.youtube.com' || link.hostname === 'youtu.be')) {
    const short = link.pathname.match(/^\/shorts\/([a-zA-Z0-9_-]{11})$/);
    if (short?.[1]) {
      link.pathname = '/watch';
      link.searchParams.set('v', short[1]);
    }
    link.searchParams.set('t', `${example.startSeconds}s`);
  }
  return link.href;
}

function timeLabel(seconds: number) {
  return `${Math.floor(seconds / 60)}:${String(Math.floor(seconds % 60)).padStart(2, '0')}`;
}
</script>

<template>
  <article class="board-page guide-detail">
    <RouterLink to="/" class="board-link"><ArrowLeft aria-hidden="true" :size="16" /> All guides</RouterLink>
    <p v-if="loading" role="status" class="board-state">Loading guide…</p>
    <div v-else-if="error" role="alert" class="board-state">
      <p>Could not load this guide.</p>
      <Button tone="primary" @click="load">Retry</Button>
    </div>
    <h1 v-else-if="!guide" class="board-state">Guide unavailable</h1>
    <template v-else>
      <header class="board-heading">
        <span class="board-type">{{ guide.kind }}</span>
        <h1>{{ guide.title }}</h1>
        <p>{{ guide.summary }}</p>
      </header>
      <section v-if="guide.steps.length" class="board-card">
        <h2>How it moves</h2>
        <ol class="guide-steps">
          <li v-for="(step, index) in guide.steps" :key="index">{{ step }}</li>
        </ol>
      </section>
      <section v-if="guide.examples.length" class="board-card">
        <h2>Examples</h2>
        <ul class="example-list">
          <li v-for="(example, index) in guide.examples" :key="index">
            <a :href="exampleLink(example)" class="board-link" target="_blank" rel="noopener noreferrer">
              {{ example.title }}
              <span v-if="example.startSeconds != null">at {{ timeLabel(example.startSeconds) }}</span>
              <ArrowUpRight aria-hidden="true" :size="16" />
            </a>
          </li>
        </ul>
      </section>
    </template>
  </article>
</template>
