<script setup lang="ts">
import { computed, nextTick, ref } from 'vue';
import { ArrowUpRight } from 'lucide-vue-next';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import type { TrendVideo, VideoReview } from '../../domain/trends/Trends';

const props = defineProps<{
  video: TrendVideo;
  canEdit: boolean;
  save: (id: string, review: VideoReview) => Promise<string>;
}>();
const status = ref(props.video.status);
const note = ref(props.video.note);
const saving = ref(false);
const error = ref('');
const saved = ref(false);
const changed = computed(() => status.value !== props.video.status || note.value !== props.video.note);
const byline = computed(() =>
  [
    props.video.creator,
    props.video.format === 'short' ? 'Short' : 'Video',
    props.video.publishedAt
      ? new Date(props.video.publishedAt).toLocaleDateString(undefined, { dateStyle: 'medium' })
      : null,
  ]
    .filter(Boolean)
    .join(' · '),
);

async function submit() {
  if (saving.value || !changed.value) return;
  saving.value = true;
  saved.value = false;
  try {
    error.value = await props.save(props.video.id, { status: status.value, note: note.value });
    if (error.value) return;
    await nextTick();
    status.value = props.video.status;
    note.value = props.video.note;
    saved.value = true;
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <li class="board-card video-card">
    <div class="video-details">
      <p class="video-meta">{{ byline }}</p>
      <h2>
        <a :href="video.url" class="board-link" target="_blank" rel="noopener noreferrer">
          {{ video.title }} <ArrowUpRight aria-hidden="true" :size="16" />
        </a>
      </h2>
      <ul v-if="video.tags?.length" class="video-tags" aria-label="Tags">
        <li v-for="tag in video.tags" :key="tag">{{ tag }}</li>
      </ul>
    </div>
    <form v-if="canEdit" class="video-form" @submit.prevent="submit" @input="saved = false">
      <label
        >Status
        <select v-model="status" :disabled="saving" :aria-label="`Status for ${video.title}`">
          <option value="unreviewed">Unreviewed</option>
          <option value="useful">Useful</option>
          <option value="skipped">Skipped</option>
        </select></label
      >
      <label
        >Note
        <textarea v-model="note" :disabled="saving" :aria-label="`Note for ${video.title}`" rows="2" maxlength="4000" />
      </label>
      <Button type="submit" :is-disabled="saving || !changed" tone="primary">{{ saving ? 'Saving…' : 'Save' }}</Button>
      <p v-if="error" role="alert" class="form-message">{{ error }}</p>
      <p v-else-if="saved" role="status" class="form-message">Review saved.</p>
    </form>
    <div v-else class="video-details">
      <p class="board-type">{{ video.status }}</p>
      <p v-if="video.note">{{ video.note }}</p>
    </div>
  </li>
</template>
