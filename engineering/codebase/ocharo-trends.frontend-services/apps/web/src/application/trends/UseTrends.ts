import { computed, onScopeDispose, ref, shallowRef, watch } from 'vue';
import { BffSessionExtensions, useBffAuth } from '@wow-two-beta/fe/auth';
import { ReviewStatus, type Guide, type TrendVideo, type VideoReview } from '../../domain/trends/Trends';
import { identityStrategy, trendsApi } from '../../integration/trends/TrendsApi';

export function useReviewSession() {
  const auth = useBffAuth();
  return {
    signedIn: computed(() => auth.isAuthenticated),
    person: computed(() => auth.person),
    signInHref: identityStrategy.signInUrl('/review'),
    signOut: () => auth.signOut(),
  };
}

export function useGuides() {
  const guides = shallowRef<Guide[]>([]);
  const loading = ref(true);
  const error = ref(false);
  const request = new AbortController();
  onScopeDispose(() => request.abort());

  async function load() {
    loading.value = true;
    error.value = false;
    const result = await trendsApi.listGuides(request.signal);
    if (request.signal.aborted) return;
    if (result.ok) guides.value = result.value;
    else error.value = true;
    loading.value = false;
  }

  void load();
  return { guides, loading, error, load };
}

export function useVideoReviews() {
  const auth = useBffAuth();
  const canEdit = computed(() => auth.hasPermission('trends:edit'));
  const sessionKey = computed(() =>
    auth.isPending ? undefined : auth.user ? BffSessionExtensions.identity(auth.user) : null,
  );
  const videos = shallowRef<TrendVideo[]>([]);
  const loading = ref(true);
  const error = ref('');
  const signedOut = ref(false);
  const request = new AbortController();
  onScopeDispose(() => request.abort());

  const counts = computed(
    () =>
      Object.fromEntries(
        ReviewStatus.map((status) => [status, videos.value.filter((video) => video.status === status).length]),
      ) as Record<ReviewStatus, number>,
  );

  async function load() {
    loading.value = true;
    error.value = '';
    signedOut.value = false;
    const result = await trendsApi.listVideos(request.signal);
    if (request.signal.aborted || (!result.ok && result.failure.type === 'cancelled')) return;
    if (result.ok) videos.value = result.value;
    else if (result.failure.status === 401) signedOut.value = true;
    else if (result.failure.status === 403) error.value = 'Your account does not have access to the review board.';
    else if (result.failure.status === 503)
      error.value = 'Video review is unavailable. Ocharo Identity and the review database must be connected.';
    else error.value = 'Could not load the review board.';
    loading.value = false;
  }

  async function save(id: string, input: VideoReview) {
    const result = await trendsApi.saveReview(id, input, request.signal);
    if (request.signal.aborted || (!result.ok && result.failure.type === 'cancelled')) return '';
    if (result.ok) {
      const updated = result.value;
      videos.value = videos.value.map((video) => (video.id === id ? updated : video));
      return '';
    }
    if (result.failure.status === 401) {
      signedOut.value = true;
      videos.value = [];
      return 'Sign in again to save this review.';
    }
    if (result.failure.status === 403) return 'Your account does not have permission to save reviews.';
    return 'Could not save the review. Try again.';
  }

  watch(
    sessionKey,
    (session) => {
      videos.value = [];
      if (session === undefined) return;
      void load();
    },
    { immediate: true },
  );
  return { videos, loading, error, signedOut, canEdit, counts, load, save };
}
