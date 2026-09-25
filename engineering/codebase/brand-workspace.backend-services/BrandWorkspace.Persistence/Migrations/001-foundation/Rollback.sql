-- The application CLI never enables rollback; explicit guarded recovery only.
DROP TABLE foundation.probe_receipts;
DROP TABLE foundation.worker_heartbeats;
DROP TABLE foundation.schema_version;
DROP SCHEMA foundation;
