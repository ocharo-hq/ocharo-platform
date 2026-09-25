# Deployment contract

*Last updated: 2026-09-26*

No deployment artifact or environment exists yet. The scaffold target has one API image serving
the built Vue frontend and one worker image, coordinated through a local Compose definition.
PostgreSQL and media storage require explicit persistent volumes or external stores.

Keep deployment definitions here with build context `engineering/codebase/`. Do not introduce a
second frontend origin by default. Follow the [single-host convention](../../../../../conventions/deployment/hosting/single-host-serving.md).

Give schema migration one owner and coordinate API/worker schema compatibility. Health checks,
graceful shutdown, recovery, and backup restoration are explicit verification concerns. Provider
access and public hosting are later environment decisions; no production deployment is authorized
by the documentation baseline.
