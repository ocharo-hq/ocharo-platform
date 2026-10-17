# Engineering overview

*Last updated: 2026-10-17*

## Foundation stack

| Area | Target | Implementation status |
|---|---|---|
| Backend | .NET 10, ASP.NET Core, Clean Architecture, backend beta SDK | Implemented and locally verified |
| Frontend | Vue 3, strict TypeScript, Vite, Tailwind v4, Vue beta UI SDK | Implemented and locally verified |
| Persistence | PostgreSQL, Npgsql, embedded SQL migrations through the backend SDK | Implemented and locally verified |
| Execution | Independent worker, Hangfire PostgreSQL storage, technical probes | Implemented and locally verified |
| Hosting | API serves built frontend; separate worker executable/image | Implemented and locally verified |
| Source | Existing product template, adapted to current conventions | Audited source adapted additively |

Toolchain pins: .NET SDK `10.0.300`, Node `24.11.0`, pnpm `10.33.2`. Published SDK inputs are
`WoW2.Sdk.Beta.Be` `10.0.68-beta` and `@wow-two-beta/ui-vue` `0.0.6`. Lockfiles record
resolved dependency graphs. Runtime and test proof remains in the verification record.

## Navigation

- [Architecture](architecture/architecture.md) owns system boundaries and invariants.
- [Template audit](research/template-audit.md) owns source provenance and adaptation requirements.
- [Scaffold track](research/previous-work/rough-track/r0.1/r0.1.md) owns the next implementation work.
- [Verification](research/previous-work/verification.md) distinguishes inspections from executed checks.
- [Deployment](deployment/deployment.md) and [operations](operations/operations.md) own runtime lifecycle.
