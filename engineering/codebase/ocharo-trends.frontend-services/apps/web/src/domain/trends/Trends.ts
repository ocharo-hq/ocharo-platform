export const ReviewStatus = ['unreviewed', 'useful', 'skipped'] as const;
export type ReviewStatus = (typeof ReviewStatus)[number];
export type VideoFormat = 'short' | 'video';
export type GuideKind = 'trend' | 'transition';

export interface VideoReview {
  status: ReviewStatus;
  note: string;
}

export interface TrendVideo extends VideoReview {
  id: string;
  title: string;
  url: string;
  creator: string;
  format: VideoFormat;
  publishedAt?: string | null;
  tags?: string[] | null;
}

export interface GuideExample {
  title: string;
  url: string;
  startSeconds?: number | null;
}

export interface Guide {
  slug: string;
  title: string;
  kind: GuideKind;
  summary: string;
  steps: string[];
  examples: GuideExample[];
}
