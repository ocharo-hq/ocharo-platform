# Backend development

*Last updated: 2026-09-26*

Follow the [shared .NET conventions](../../../../../conventions/development/backend/dotnet/dotnet-conventions.md).
Use the actual SDK contracts and verified package pins; older template prose is not an API guarantee.

Use `BrandWorkspace.BackendServices.slnx`, central package management, and the prescribed Clean
Architecture roles. Group code by domain inside each appropriate project. API and worker compose
the same reusable modules without duplicating business layers.

The target persistence provider is PostgreSQL. Use versioned migrations and one migration execution
owner; the template's SQLite `EnsureCreated` path does not satisfy this product's persistence contract.
