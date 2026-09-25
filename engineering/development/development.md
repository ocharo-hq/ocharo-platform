# Development

*Last updated: 2026-09-26*

Use shared wow-two conventions; repository documents carry only product-specific choices.
The initial bootstrap is one coordinated lane. Parallel business subsystems need an explicit vector
ownership map before work divides; a frontend/backend split is not a business vector.

The active session may touch this product and the backend/UI SDKs through explicit disjoint lanes.
Studio remains independently owned. Shared package and solution changes have one coordinator.

Backend rules: [backend guidelines](backend-guidelines.md).
Frontend rules: [frontend guidelines](frontend-guidelines.md).
Execution: [scaffold track](../planning/rough-track/r0.1/r0.1.md).
