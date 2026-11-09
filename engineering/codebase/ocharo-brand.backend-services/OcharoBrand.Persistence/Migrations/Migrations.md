# Migrations

*Last updated: 2026-11-09*

The `brand` migration set, bespoke SQL embedded in the assembly: one `NNN-name/` folder per migration with `Apply.sql`
and `Rollback.sql`, `{{schema}}` standing for `brand`. Applied migrations never change; a new one gets the next number.
`001-baseline` creates every table the SQLite store held and the key ring table.
