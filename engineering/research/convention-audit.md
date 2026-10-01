# Platform convention audit

*Reviewed: 2026-10-01*

## Changes

One canonical backlog and v0.1 track now cover Desk, Commerce and private image processing. Adopted Brand/Catalogue component names are explicit compatibility exceptions in CLAUDE.md. Previous source-pilot planning and receipts are preserved under research; operational rules moved to development.

Desk, Brand and Catalogue APIs expose immutable assembly identity. Their frontend headers include About. The jobs worker writes its build identity to stderr, preserving diagnostic JSON stdout. Product migration stamps remove only informational `+commit` metadata; prerelease identities remain intact.

The descriptor represents Desk, jobs and the private Python worker. All named sites are private. Desk and jobs read the deployer's optional appsettings.Local.json before permitted environment overrides; Python accepts an equivalent ImageWorker section. Worker readiness includes truthful version and optional commit; model readiness semantics are unchanged. Python health uses its installed urllib probe.

## Executed checks

- Platform backend: 37 unit and 92 integration tests passed after correcting migration stamping.
- Desk frontend: typecheck, 187 tests and production build passed.
- Retained Brand pilot: 5 backend and 4 frontend tests passed; frontend typecheck passed.
- Retained Catalogue pilot: 18 backend and 12 frontend tests passed; frontend typecheck passed.
- Private image worker: 17 tests passed, including mounted settings and build identity.
- Full informational version initially overflowed the SDK migration varchar(20); regression cases now strip only build metadata, preserving long prereleases exactly.

## Open integration gaps

- The shared SDK migration history still limits product versions to 20 characters. A permitted long prerelease needs a published SDK widening before deployment; truncation is prohibited.
- Shared publisher adoption needs Git LFS checkout and source-revision build input. Existing publishing behavior is retained; the new descriptors are prepared inputs, not proof of CI execution.
- Legacy frontend VITE origin values remain build-time configuration. Hosted runtime configuration and donor absorption require their own reviewed work.
- Jobs build identity is logged; no persisted per-worker build inventory exists in Desk About.
- Frontend bundle size advisory remains. Real hosted backup/restore and access acceptance remain open.

## Boundaries

- Product v0.1 remains in progress; all developer verification checkboxes remain open.
- Product package versions are 0.0.0 placeholders. Local apps/services report dev; CI accepts APP_VERSION.
- Full assembly informational identity remains available at `/api/system/version`; About loads it on demand.
- No Git index, commit, remote, deployment target, real user data or existing runtime changed in this lane.
- Preserved pre-consolidation notes under research are evidence, not active plans or current permissions.
- The proposed shared publisher v0.1.0 does not hydrate Git LFS assets and does not inject VCS_REF.
- The proposed shared CI migration was withdrawn. Existing Platform/Studio release triggers, jobs and permissions were retained. A narrow follow-up adds validated APP_VERSION/VCS_REF build inputs to both existing workflows and Git LFS checkout to Platform (Studio already hydrates LFS). Ten valid/invalid tag cases and unchanged trigger/job/permission structure were checked locally. Assets has no active publisher. No remote workflow ran.
- Container version smoke proves local packaging only; no product release, hosted acceptance or human approval is claimed.
