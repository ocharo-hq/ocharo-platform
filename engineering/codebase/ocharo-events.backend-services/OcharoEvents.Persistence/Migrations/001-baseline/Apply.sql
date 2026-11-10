-- 001-baseline — the Events service's Data Protection key ring, in schema `ocharo_events` of the product database.
--
-- The service keeps no event rows: WoW2 Events holds the catalog and each organization's tracking (events-app.md #2).
-- The key ring encrypts the BFF session cookies, whose sessions live in the be sdk's cache set (schema
-- `ocharo_events_cache`), so a restart or deploy keeps everyone signed in.

CREATE TABLE IF NOT EXISTS {{schema}}.data_protection_keys (
    id            BIGINT GENERATED ALWAYS AS IDENTITY,
    friendly_name TEXT        NOT NULL,
    xml           TEXT        NOT NULL,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT pk_data_protection_keys PRIMARY KEY (id),
    CONSTRAINT uq_data_protection_keys_friendly_name UNIQUE (friendly_name)
);
