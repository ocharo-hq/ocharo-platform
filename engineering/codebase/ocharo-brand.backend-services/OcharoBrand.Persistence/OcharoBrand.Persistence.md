# OcharoBrand.Persistence

*Last updated: 2026-11-09*

Brand's store in schema `brand` of the product database (PostgreSQL): the `OcharoBrandDbContext` mapped onto the tables
the embedded `brand` migration set creates (`Migrations/NNN-name/{Apply,Rollback}.sql`), the brand repository, the
Data Protection key ring table (`PostgresXmlRepository`) and `Import/`, the one-time verified copy of the retired SQLite
store behind the host's `import-sqlite` verb.
