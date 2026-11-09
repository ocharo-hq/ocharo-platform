-- 001-baseline — Brand's store and Data Protection key ring, in schema `brand` of the product database.
--
-- Every table the retired SQLite store held, column for column, so `import-sqlite` copies each row with its id,
-- revision, SHA-256 and instant unchanged: the single-row draft, its revision snapshots, logo versions, releases,
-- scheduled appearances, media (each WoW2 Files `file_id`, or the local blob of a file stored before Files) and the apps
-- that read the public kit. Documents stay text, byte for byte as Brand wrote them. Brand binds one organization by
-- setting (`Brand:OrganizationId`) and stores no organization or account id, so no key reaches Identity's tables.

CREATE TABLE IF NOT EXISTS {{schema}}.drafts (
    id         TEXT        NOT NULL,
    revision   INTEGER     NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    json       TEXT        NOT NULL,

    CONSTRAINT pk_drafts PRIMARY KEY (id),
    CONSTRAINT ck_drafts_revision CHECK (revision >= 0)
);

CREATE TABLE IF NOT EXISTS {{schema}}.draft_revisions (
    id       UUID        NOT NULL,
    revision INTEGER     NOT NULL,
    saved_at TIMESTAMPTZ NOT NULL,
    summary  TEXT        NOT NULL,
    json     TEXT        NOT NULL,

    CONSTRAINT pk_draft_revisions PRIMARY KEY (id),
    CONSTRAINT uq_draft_revisions_revision UNIQUE (revision)
);

CREATE TABLE IF NOT EXISTS {{schema}}.logo_versions (
    id          TEXT        NOT NULL,
    slot        TEXT        NOT NULL,
    sha256      TEXT        NOT NULL,
    uploaded_at TIMESTAMPTZ NOT NULL,
    json        TEXT        NOT NULL,

    CONSTRAINT pk_logo_versions PRIMARY KEY (id),
    CONSTRAINT uq_logo_versions_slot_sha256 UNIQUE (slot, sha256)
);

CREATE TABLE IF NOT EXISTS {{schema}}.releases (
    version      INTEGER     NOT NULL,
    published_at TIMESTAMPTZ NOT NULL,
    json         TEXT        NOT NULL,

    CONSTRAINT pk_releases PRIMARY KEY (version),
    CONSTRAINT ck_releases_version CHECK (version >= 1)
);

CREATE TABLE IF NOT EXISTS {{schema}}.appearances (
    id        TEXT        NOT NULL,
    starts_at TIMESTAMPTZ NOT NULL,
    ends_at   TIMESTAMPTZ NOT NULL,
    status    TEXT        NOT NULL,
    json      TEXT        NOT NULL,

    CONSTRAINT pk_appearances PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_appearances_status_starts_at ON {{schema}}.appearances (status, starts_at);

CREATE TABLE IF NOT EXISTS {{schema}}.media_blobs (
    sha256       TEXT        NOT NULL,
    content_type TEXT        NOT NULL,
    byte_length  BIGINT      NOT NULL,
    file_id      TEXT,
    blob_path    TEXT        NOT NULL,
    preview_path TEXT,
    width        INTEGER,
    height       INTEGER,
    stored_at    TIMESTAMPTZ NOT NULL,

    CONSTRAINT pk_media_blobs PRIMARY KEY (sha256),
    CONSTRAINT ck_media_blobs_byte_length CHECK (byte_length >= 0)
);

CREATE TABLE IF NOT EXISTS {{schema}}.consumers (
    app          TEXT        NOT NULL,
    last_seen_at TIMESTAMPTZ NOT NULL,
    last_release INTEGER     NOT NULL,
    last_path    TEXT        NOT NULL,

    CONSTRAINT pk_consumers PRIMARY KEY (app)
);

-- The ring that encrypts sign-in cookies and the antiforgery token, shared by every Brand replica.
CREATE TABLE IF NOT EXISTS {{schema}}.data_protection_keys (
    id            BIGINT GENERATED ALWAYS AS IDENTITY,
    friendly_name TEXT        NOT NULL,
    xml           TEXT        NOT NULL,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT pk_data_protection_keys PRIMARY KEY (id),
    CONSTRAINT uq_data_protection_keys_friendly_name UNIQUE (friendly_name)
);
