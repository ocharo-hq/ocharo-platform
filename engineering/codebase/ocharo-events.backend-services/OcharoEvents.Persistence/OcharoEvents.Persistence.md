# OcharoEvents.Persistence

*Last updated: 2026-11-10*

The service's place in schema `ocharo_events` of the product database (PostgreSQL): the embedded `ocharo_events`
migration set (`Migrations/NNN-name/{Apply,Rollback}.sql`), the Data Protection key ring table
(`PostgresXmlRepository`) and `OcharoEventsDbContext`, which maps no table and serves the readiness check. Event rows live
in WoW2 Events, never here.
