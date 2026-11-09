-- 001-baseline — Catalogue's store in schema `catalogue` of the product database (`ocharo_{env}`, shared with Desk,
-- Identity and Studio): products and their immutable revisions, collections, media, settings, named catalogues and
-- their revisions, releases with their artifacts and publications, and the Data Protection key ring. It replaces the
-- SQLite store's EnsureCreated tables and the designer tables the boot used to add, column for column; the one-time
-- `import-sqlite` verb copies the retired rows in, ids, revisions, hashes and instants unchanged.
--
-- Catalogue stores no Identity organization or user id: the host binds one organization (`Catalogue:OrganizationId`),
-- so no key reaches into Identity's tables. Document columns stay `text`, byte for byte as saved: the release
-- fingerprint hashes the product and settings text, and `jsonb` would rewrite it.

CREATE TABLE IF NOT EXISTS {{schema}}.products (
    id           varchar(64) NOT NULL,
    garment_type text        NOT NULL,
    status       text        NOT NULL,
    sort_order   integer     NOT NULL,
    revision     integer     NOT NULL,
    created_at   timestamptz NOT NULL,
    updated_at   timestamptz NOT NULL,
    document     text        NOT NULL,

    CONSTRAINT pk_products PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_products_status_sort_order ON {{schema}}.products (status, sort_order);
CREATE INDEX IF NOT EXISTS ix_products_garment_type ON {{schema}}.products (garment_type);

-- One immutable snapshot per saved product revision.
CREATE TABLE IF NOT EXISTS {{schema}}.product_revisions (
    id         uuid        NOT NULL,
    product_id varchar(64) NOT NULL,
    revision   integer     NOT NULL,
    saved_at   timestamptz NOT NULL,
    summary    text        NOT NULL,
    document   text        NOT NULL,

    CONSTRAINT pk_product_revisions PRIMARY KEY (id),
    CONSTRAINT uq_product_revisions_product_id_revision UNIQUE (product_id, revision),
    CONSTRAINT fk_product_revisions_product FOREIGN KEY (product_id)
        REFERENCES {{schema}}.products (id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS {{schema}}.collections (
    id         text        NOT NULL,
    sort_order integer     NOT NULL,
    revision   integer     NOT NULL,
    updated_at timestamptz NOT NULL,
    document   text        NOT NULL,

    CONSTRAINT pk_collections PRIMARY KEY (id)
);

-- Content-addressed product images: bytes stored before WoW2 Files stay on the data volume under `blob_path`;
-- `file_id` names the Files copy once stored there.
CREATE TABLE IF NOT EXISTS {{schema}}.media_blobs (
    sha256       varchar(64) NOT NULL,
    content_type text        NOT NULL,
    byte_length  bigint      NOT NULL,
    file_id      text,
    blob_path    text        NOT NULL,
    preview_path text,
    width        integer,
    height       integer,
    stored_at    timestamptz NOT NULL,

    CONSTRAINT pk_media_blobs PRIMARY KEY (sha256)
);

CREATE TABLE IF NOT EXISTS {{schema}}.settings (
    id       text    NOT NULL,
    revision integer NOT NULL,
    document text    NOT NULL,

    CONSTRAINT pk_settings PRIMARY KEY (id)
);

-- Named catalogues: a slug is unique per language among the catalogues not archived.
CREATE TABLE IF NOT EXISTS {{schema}}.catalogue_designs (
    id         uuid        NOT NULL,
    title      text        NOT NULL,
    slug       text        NOT NULL,
    locale     text        NOT NULL,
    revision   integer     NOT NULL,
    archived   boolean     NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    document   text        NOT NULL,

    CONSTRAINT pk_catalogue_designs PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_catalogue_designs_slug_locale
    ON {{schema}}.catalogue_designs (slug, locale) WHERE NOT archived;

CREATE TABLE IF NOT EXISTS {{schema}}.catalogue_design_revisions (
    id        uuid        NOT NULL,
    design_id uuid        NOT NULL,
    revision  integer     NOT NULL,
    title     text        NOT NULL,
    saved_at  timestamptz NOT NULL,
    document  text        NOT NULL,

    CONSTRAINT pk_catalogue_design_revisions PRIMARY KEY (id),
    CONSTRAINT uq_catalogue_design_revisions_design_id_revision UNIQUE (design_id, revision),
    CONSTRAINT fk_catalogue_design_revisions_design FOREIGN KEY (design_id)
        REFERENCES {{schema}}.catalogue_designs (id) ON DELETE RESTRICT
);

-- Prepared releases: immutable once prepared; publishing stamps `published_at`, withdrawing `withdrawn_at`.
CREATE TABLE IF NOT EXISTS {{schema}}.catalogue_releases (
    id               uuid        NOT NULL,
    design_id        uuid        NOT NULL,
    design_revision  integer     NOT NULL,
    slug             text        NOT NULL,
    locale           text        NOT NULL,
    created_at       timestamptz NOT NULL,
    published_at     timestamptz,
    withdrawn_at     timestamptz,
    fingerprint      varchar(64) NOT NULL,
    product_ids_json text        NOT NULL,
    document         text        NOT NULL,

    CONSTRAINT pk_catalogue_releases PRIMARY KEY (id),
    CONSTRAINT fk_catalogue_releases_design FOREIGN KEY (design_id)
        REFERENCES {{schema}}.catalogue_designs (id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_catalogue_releases_design_id_created_at
    ON {{schema}}.catalogue_releases (design_id, created_at);

-- The release each `{slug}:{locale}` serves to the public reader.
CREATE TABLE IF NOT EXISTS {{schema}}.catalogue_publications (
    key        text NOT NULL,
    design_id  uuid NOT NULL,
    release_id uuid NOT NULL,

    CONSTRAINT pk_catalogue_publications PRIMARY KEY (key),
    CONSTRAINT fk_catalogue_publications_design FOREIGN KEY (design_id)
        REFERENCES {{schema}}.catalogue_designs (id) ON DELETE RESTRICT,
    CONSTRAINT fk_catalogue_publications_release FOREIGN KEY (release_id)
        REFERENCES {{schema}}.catalogue_releases (id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_catalogue_publications_design_id ON {{schema}}.catalogue_publications (design_id);
CREATE INDEX IF NOT EXISTS ix_catalogue_publications_release_id ON {{schema}}.catalogue_publications (release_id);

-- Release artifacts (PDFs, page PNGs) prepared before WoW2 Files, by SHA-256; read until migrated to Files.
CREATE TABLE IF NOT EXISTS {{schema}}.catalogue_release_assets (
    sha256       varchar(64) NOT NULL,
    content_type text        NOT NULL,
    bytes        bytea       NOT NULL,

    CONSTRAINT pk_catalogue_release_assets PRIMARY KEY (sha256)
);

-- Where each release artifact lives in WoW2 Files, by SHA-256.
CREATE TABLE IF NOT EXISTS {{schema}}.catalogue_release_files (
    sha256       varchar(64) NOT NULL,
    content_type text        NOT NULL,
    file_id      text        NOT NULL,

    CONSTRAINT pk_catalogue_release_files PRIMARY KEY (sha256)
);

-- The ring that encrypts sign-in cookies and the antiforgery token, shared by every Catalogue replica.
CREATE TABLE IF NOT EXISTS {{schema}}.data_protection_keys (
    id            bigint GENERATED ALWAYS AS IDENTITY,
    friendly_name text        NOT NULL,
    xml           text        NOT NULL,
    created_at    timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT pk_data_protection_keys PRIMARY KEY (id),
    CONSTRAINT uq_data_protection_keys_friendly_name UNIQUE (friendly_name)
);
