/** Describes the foundation service's reported worker heartbeat, not publishing readiness. */
export interface SystemStatus {
  readonly service: 'brand-workspace';
  readonly stage: 'foundation';
  readonly persistence: 'PostgreSQL';
  readonly worker: {
    readonly state: 'waiting' | 'healthy' | 'stale';
    readonly lastSeenAtUtc: string | null;
  };
}
