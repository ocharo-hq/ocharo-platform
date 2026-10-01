# Product capabilities

*Last updated: 2026-09-26*

## First product milestone — preparation

| Capability | Acceptance |
|---|---|
| Organization and brand workspace | Authenticated membership; Ocharo and non-apparel fixture remain isolated |
| Source/media selection | Stable source identity, immutable media revision, hash, and provenance |
| Draft editing | Optimistic revisions; conflicting saves preserve local work |
| Review and approval | Approval covers exact content/media revisions; material edits require review |
| Manual publication package | Explicit manual status, destination intent, downloadable media/copy, receipt fields |

The same workflow must work without Ocharo constants, garment fields, or a running Studio.
Missing tenant context must fail closed across records, media, exports, and mutations.

## Following milestone — automated delivery

One supported account/format receives approved, one-time scheduled publications. Persist intent,
attempts, provider references, and confirmed results. Restart, concurrent claims, lease expiry,
credential revocation, cancellation races, and crash-after-send must have verified recovery behavior.

Observations distinguish zero, unknown, unsupported, stale, and corrected values. A provider stub
proves local behavior; an authorized live probe proves only that specific provider route.

## Later capabilities

More channels, recurring campaigns, inbox workflows, merchant relationships, order evidence, and
deeper apparel operations remain in the [ordered engineering backlog](../../engineering/planning/backlog.md)
and the broader [capability map][capabilities]. They are not scaffold completion criteria.

[capabilities]: /Users/max/Projects/10x-ws/workbench/fam/mft-10x-ws/context/domains/external/work/ventures/ocharo/platform/och-platform-capabilities.md
