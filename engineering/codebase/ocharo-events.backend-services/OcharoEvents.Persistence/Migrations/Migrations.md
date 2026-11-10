# Migrations

*Last updated: 2026-11-10*

The `ocharo_events` migration set, bespoke SQL embedded in the assembly: one `NNN-name/` folder per migration with
`Apply.sql` and `Rollback.sql`, `{{schema}}` standing for `ocharo_events`. Applied migrations never change; a new one gets
the next number. `001-baseline` creates the Data Protection key ring table, the set's only table.
