# Engineering overview

*Last updated: 2026-09-26*

## Target stack

| Area | Target | Baseline status |
|---|---|---|
| Backend | .NET 10, ASP.NET Core, EF Core, Clean Architecture, backend beta SDK | Planned |
| Frontend | Vue 3, strict TypeScript, Vite, Tailwind v4, Vue beta UI SDK | Planned |
| Persistence | PostgreSQL with versioned migrations | Planned |
| Execution | Independent worker using durable shared-library primitives | Planned |
| Hosting | API serves built frontend; separate worker executable/image | Planned |
| Source | Existing product template, adapted to current conventions | Audited; not imported |

Installed toolchain inspection found .NET SDK `10.0.300` and Node `v24.11.0`; this is not a
project build or dependency verification. Package pins will be verified during scaffolding.

## Navigation

- [Architecture](architecture/architecture.md) owns system boundaries and invariants.
- [Template audit](research/template-audit.md) owns source provenance and adaptation requirements.
- [Scaffold track](planning/rough-track/r0.1/r0.1.md) owns the next implementation work.
- [Verification](planning/verification.md) distinguishes inspections from executed checks.
- [Deployment](deployment/deployment.md) and [operations](operations/operations.md) own runtime lifecycle.
