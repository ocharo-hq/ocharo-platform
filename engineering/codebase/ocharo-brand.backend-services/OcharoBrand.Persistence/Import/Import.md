# Import

*Last updated: 2026-11-09*

The one-time copy of the retired SQLite store, its file key ring and a check of its local blobs: `LegacySqliteImport`
copies every row PostgreSQL lacks, verifies each table by row count and per-row SHA-256 digests, and retires the file;
`LegacySqliteSettings` says where the store sits; `LegacySqliteImportReport` is the `import-result` line.
