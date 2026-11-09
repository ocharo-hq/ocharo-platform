# OcharoCatalogue.Persistence

*Last updated: 2026-11-09*

The PostgreSQL store in schema `catalogue` of the product database (`ocharo_{env}`): the `catalogue` migration set
(`Migrations/001-baseline`, bespoke SQL, advisory lock `8278001`), the `OcharoCatalogueDbContext` that maps it, the
repositories, the Data Protection key ring table (`PostgresXmlRepository`) and the one-time import of the retired SQLite
store (`Import/`). Repository writes run in serializable transactions; a save another one overtook maps to the busy
conflict (`PostgresBusyExceptionMappingRule`).
