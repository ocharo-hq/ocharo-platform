import { BffSessionExtensions, createAuthBridge, createBffStrategy } from '@wow-two-beta/fe/auth';
import { ApiFailureFactory, createApiClient, type ApiDecoder } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import {
  ReviewStatus,
  type Guide,
  type GuideExample,
  type TrendVideo,
  type VideoReview,
} from '../../domain/trends/Trends';

export const identityStrategy = createBffStrategy();
export const identityBridge = createAuthBridge({ getIdentity: BffSessionExtensions.identity });
const api = createApiClient({ retry: false });
const review = createApiClient({
  retry: false,
  scope: identityBridge.scope,
  onUnauthorized: identityBridge.onUnauthorized,
  antiforgery: identityStrategy.antiforgery,
});

function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function isHttpsUrl(value: unknown): value is string {
  if (typeof value !== 'string' || !URL.canParse(value)) return false;
  const url = new URL(value);
  return url.protocol === 'https:' && !url.username && !url.password;
}

function isStringList(value: unknown): value is string[] {
  return Array.isArray(value) && value.every((item) => typeof item === 'string');
}

function isVideo(value: unknown): value is TrendVideo {
  return (
    isRecord(value) &&
    typeof value.id === 'string' &&
    typeof value.title === 'string' &&
    typeof value.creator === 'string' &&
    isHttpsUrl(value.url) &&
    typeof value.note === 'string' &&
    (value.format === 'short' || value.format === 'video') &&
    ReviewStatus.some((status) => status === value.status) &&
    (value.publishedAt == null || typeof value.publishedAt === 'string') &&
    (value.tags == null || isStringList(value.tags))
  );
}

function isExample(value: unknown): value is GuideExample {
  return (
    isRecord(value) &&
    typeof value.title === 'string' &&
    isHttpsUrl(value.url) &&
    (value.startSeconds == null || (typeof value.startSeconds === 'number' && value.startSeconds >= 0))
  );
}

function isGuide(value: unknown): value is Guide {
  return (
    isRecord(value) &&
    typeof value.slug === 'string' &&
    typeof value.title === 'string' &&
    typeof value.summary === 'string' &&
    (value.kind === 'trend' || value.kind === 'transition') &&
    isStringList(value.steps) &&
    Array.isArray(value.examples) &&
    value.examples.every(isExample)
  );
}

function decode<T>(isValid: (value: unknown) => value is T): ApiDecoder<T> {
  return (value) =>
    isValid(value) ? ResultExtensions.ok(value) : ResultExtensions.fail(ApiFailureFactory.create('protocol'));
}

function list<T>(check: (value: unknown) => value is T): ApiDecoder<T[]> {
  return decode((value): value is T[] => Array.isArray(value) && value.every(check));
}

export const trendsApi = {
  listGuides: (signal: AbortSignal) =>
    api.get('/api/trends/public/guides', { signal, unwrap: false, decode: list(isGuide) }),
  listVideos: (signal: AbortSignal) =>
    review.get('/api/trends/review/videos', { signal, unwrap: false, decode: list(isVideo) }),
  saveReview: (id: string, body: VideoReview, signal: AbortSignal) =>
    review.put(`/api/trends/review/videos/${encodeURIComponent(id)}`, {
      body,
      signal,
      unwrap: false,
      decode: decode(isVideo),
    }),
};
