# Import

*Last updated: 2026-11-09*

The one-time copy of the retired SQLite store and its file key ring into schema `catalogue`, run by the host's
`import-sqlite [--dry-run]` verb: `LegacySqliteTables` lists every table and column in both stores,
`LegacySqliteImport` copies, verifies each row by a SHA-256 over its canonical content and retires the file, and
`LegacySqliteImportReport` is the `import-result` line (counts and digests only). Operator steps:
`engineering/deployment/catalogue/catalogue.md` § PostgreSQL move.
