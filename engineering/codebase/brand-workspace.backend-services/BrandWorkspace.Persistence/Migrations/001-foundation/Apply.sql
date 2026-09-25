CREATE SCHEMA foundation;
CREATE TABLE foundation.schema_version (id integer PRIMARY KEY CHECK (id = 1), version integer NOT NULL);
INSERT INTO foundation.schema_version (id, version) VALUES (1, 1);
CREATE TABLE foundation.worker_heartbeats (worker_id text PRIMARY KEY, last_seen_at_utc timestamptz NOT NULL);
CREATE TABLE foundation.probe_receipts (probe_id uuid PRIMARY KEY, completed_at_utc timestamptz NOT NULL);
