<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { useReviewSession, useVideoReviews } from '../../../application/trends/UseTrends';
import type { ReviewStatus, VideoFormat } from '../../../domain/trends/Trends';
import ReviewVideoCard from '../ReviewVideoCard.vue';

const { signInHref } = useReviewSession();
const { videos, loading, error, signedOut, canEdit, counts, load, save } = useVideoReviews();
const query = ref('');
const status = ref<ReviewStatus | ''>('');
const format = ref<VideoFormat | ''>('');
const page = ref(1);
const pageSize = 24;
const filtered = computed(() => {
  const needle = query.value.trim().toLocaleLowerCase();
  return videos.value.filter(
    (video) =>
      (!status.value || video.status === status.value) &&
      (!format.value || video.format === format.value) &&
      [video.title, video.creator, video.note, ...(video.tags ?? [])].some((text) =>
        text.toLocaleLowerCase().includes(needle),
      ),
  );
});
const lastPage = computed(() => Math.max(1, Math.ceil(filtered.value.length / pageSize)));
const shown = computed(() => filtered.value.slice((page.value - 1) * pageSize, page.value * pageSize));
watch([query, status, format], () => {
  page.value = 1;
});
watch(lastPage, (value) => {
  page.value = Math.min(page.value, value);
});
</script>

<template>
  <section class="board-page">
    <header class="board-heading">
      <h1>Review</h1>
      <p>Review original videos and keep the references that can be reused.</p>
    </header>
    <p v-if="loading" role="status" class="board-state">Loading the review board…</p>
    <div v-else-if="signedOut" class="board-state">
      <h2>Sign in to review videos</h2>
      <a :href="signInHref" class="board-link board-sign-in">Sign in</a>
    </div>
    <div v-else-if="error" role="alert" class="board-state">
      <p>{{ error }}</p>
      <Button tone="primary" @click="load">Retry</Button>
    </div>
    <template v-else>
      <dl class="board-counts">
        <div>
          <dt>Indexed</dt>
          <dd>{{ videos.length }}</dd>
        </div>
        <div>
          <dt>Unreviewed</dt>
          <dd>{{ counts.unreviewed }}</dd>
        </div>
        <div>
          <dt>Useful</dt>
          <dd>{{ counts.useful }} <small>/ 1,000</small></dd>
        </div>
        <div>
          <dt>Skipped</dt>
          <dd>{{ counts.skipped }}</dd>
        </div>
      </dl>
      <div class="board-filters">
        <label
          >Search videos <input v-model="query" type="search" placeholder="Search by title, creator, tag or note"
        /></label>
        <label
          >Status
          <select v-model="status">
            <option value="">All statuses</option>
            <option value="unreviewed">Unreviewed</option>
            <option value="useful">Useful</option>
            <option value="skipped">Skipped</option>
          </select>
        </label>
        <label
          >Format
          <select v-model="format">
            <option value="">All formats</option>
            <option value="short">Shorts</option>
            <option value="video">Regular videos</option>
          </select>
        </label>
      </div>
      <p role="status" class="board-results">{{ filtered.length }} {{ filtered.length === 1 ? 'video' : 'videos' }}</p>
      <ul v-if="shown.length" class="video-list">
        <ReviewVideoCard v-for="video in shown" :key="video.id" :video="video" :can-edit="canEdit" :save="save" />
      </ul>
      <p v-else class="board-state">No videos match those filters.</p>
      <nav v-if="lastPage > 1" aria-label="Review pages" class="board-paging">
        <Button :is-disabled="page === 1" @click="page -= 1">Previous</Button>
        <span>Page {{ page }} of {{ lastPage }}</span>
        <Button :is-disabled="page === lastPage" @click="page += 1">Next</Button>
      </nav>
    </template>
  </section>
</template>
