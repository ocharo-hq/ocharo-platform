import type { IntakeFreshnessModel } from '@wow-two-beta/fe-events/contracts';

/**
 * How long the catalog's sources may go without a new record before the Events page warns (E24): the intake pulls every
 * few minutes, so a day without a record is a stalled pull more than a quiet week.
 */
export const intakeStaleAfterMs = 24 * 60 * 60 * 1000;

/** What the Events page shows of WoW2 Events' freshness read: when the sources last delivered, and whether to warn. */
export interface IntakeFreshnessView {
  /** When a source of the catalog last received records, epoch milliseconds; null before the catalog's first record. */
  readonly receivedAt: number | null;
  /** True when the last record arrived more than 24 hours before `now`. */
  readonly stale: boolean;
  /** How many of the catalog's sources failed their last poll. */
  readonly failingSources: number;
  /** True when the page shows the freshness as a warning: stale, or a source failing. */
  readonly warns: boolean;
}

/** Reads Events' freshness of the catalog at `now` (epoch milliseconds); an unreadable time reads as none. */
export function readIntakeFreshness(model: IntakeFreshnessModel, now: number): IntakeFreshnessView {
  const parsed = model.lastReceivedAtUtc ? Date.parse(model.lastReceivedAtUtc) : Number.NaN;
  const receivedAt = Number.isFinite(parsed) ? parsed : null;
  const stale = receivedAt !== null && now - receivedAt > intakeStaleAfterMs;
  const failingSources = Number.isFinite(model.failingSources) ? Math.max(0, Math.trunc(model.failingSources)) : 0;
  return { receivedAt, stale, failingSources, warns: stale || failingSources > 0 };
}
