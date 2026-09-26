# Architecture

*Last updated: 2026-09-26*

## Product and runtime boundaries

One platform repository owns the API/frontend, integration worker, and product modules. They share
a versioned release contract and initially a PostgreSQL database. Studio is a separate creative product.
There is no separate repository or microservice for each calendar, approval, or observation feature.

The diagram describes the product target. The current preparation increment wires the API/SPA,
PostgreSQL, operator authentication, organization membership, immutable images and draft revisions,
review/approval, and manual packages. The worker still performs only foundation diagnostics. Studio
imports and provider connections remain later work. Diagnostics grant no access to business records.

```mermaid
flowchart LR
    Browser[Authorized brand workspace] --> API[API and Vue frontend]
    API --> DB[(PostgreSQL)]
    API --> Media[(Immutable media)]
    Worker[Integration worker] --> DB
    Worker --> Media
    Worker --> Provider[Supported provider]
    Studio[Independent Studio] -. optional exports .-> Browser
    SDK[Shared wow-two libraries] --> API
    SDK --> Worker
```

## Initial module ownership

| Module | Owned state and behavior |
|---|---|
| Organizations and brands | Membership, roles, brand configuration, authorization context |
| Source references and media | Imported facts, immutable revisions, provenance, storage policy |
| Content and approvals | Draft revisions, destination variants, review, approval/revocation |
| Connections | Provider account authority, capabilities, credential references, lifecycle |
| Publications | Scheduled intent, attempts, receipts, cancellation, reconciliation |
| Observations | Source-preserving metrics, availability/freshness, optional enquiry evidence |

Infrastructure primitives and reusable brand behavior are separate concerns. Existing SDK defects
are fixed in their owner. New reusable behavior is proven through the product, then completed and
adopted through the shared-library cycle. Product modules must not depend on Ocharo constants.

## Foundation composition

The API exposes public system status, liveness, readiness, and the built Vue shell. Authenticated
preparation APIs resolve current organization membership. Unknown API routes return JSON errors.
The worker is a separate executable; its normal host consumes durable
PostgreSQL jobs. A client-only diagnostic command submits harmless probes without starting a worker.

Schema changes have one explicit owner: the API executable's `--migrate` command, packaged as the
one-shot Compose migration service. Ordinary API and worker startup does not create or upgrade
schema. The selected shared SDK supports embedded SQL migrations with PostgreSQL locking, so the
foundation does not carry the template's SQLite initialization path. Framework Identity uses EF stores;
its generated DDL is versioned in `002-access` and applied by that same SQL migration owner.

The [preparation contract](preparation-contract.md) records the current security and concurrency
boundaries. Original image and ZIP bytes use a bounded PostgreSQL storage adapter. Session cookies
require HTTPS and durable Data Protection keys; unsafe requests require a session-bound antiforgery
token. Per-draft row locks serialize saves, review transitions, and new exports.

Worker timestamps are diagnostic observations, not proof that a publication succeeded. Durable
probe execution establishes the host/queue boundary; it does not establish exactly-once external
effects, publication scheduling, cancellation, or provider reconciliation.

## Invariants

- Authenticate the human and resolve organization membership before ordinary reads or writes.
- Fail closed on missing or mismatched tenant context, including workers and object storage.
- Preserve canonical product/media identities and immutable approved content revisions.
- Commit delivery intent and the outbox event together; jobs carry record IDs/revisions.
- Use short claims; do not retain database locks through provider calls.
- Reconcile an uncertain external effect before attempting another potentially duplicate send.
- Distinguish provider acceptance, processing, confirmed publication, and manual handoff.
- Keep secrets out of job arguments, browser bundles, exports, logs, and version control.
- Retain native metric definitions and freshness; missing observations never silently become zero.
- Let one owner coordinate migrations, solution files, dependency pins, and runtime composition.

## Studio and source records

Studio JSON is imported unchanged, with platform association in a sidecar record. No database sharing
or direct cross-repository project reference is required. Existing source product records and media
remain canonical until a deliberate ownership migration.

The detailed [build contract][design] remains the analysis source. Its framework examples are subject
to the current shared Vue frontend standard, recorded in this repository's template audit.

[design]: /Users/max/Projects/10x-ws/workbench/fam/mft-10x-ws/context/domains/external/work/ventures/ocharo/platform/och-platform-content-publishing-build.md
