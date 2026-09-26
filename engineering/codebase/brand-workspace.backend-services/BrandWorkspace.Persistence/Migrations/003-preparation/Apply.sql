CREATE SCHEMA preparation;

CREATE TABLE preparation.brands (
    organization_id uuid NOT NULL REFERENCES access.organizations(id),
    id uuid NOT NULL,
    name text NOT NULL CHECK (length(name) BETWEEN 1 AND 120),
    time_zone text NOT NULL CHECK (length(time_zone) BETWEEN 1 AND 100),
    PRIMARY KEY (organization_id, id)
);

CREATE TABLE preparation.media (
    organization_id uuid NOT NULL REFERENCES access.organizations(id),
    id uuid NOT NULL,
    file_name text NOT NULL CHECK (length(file_name) BETWEEN 1 AND 200),
    content_type text NOT NULL CHECK (content_type IN ('image/png', 'image/jpeg')),
    bytes bytea NOT NULL CHECK (octet_length(bytes) BETWEEN 1 AND 5242880),
    sha256 text NOT NULL CHECK (sha256 ~ '^[a-f0-9]{64}$'),
    provenance text NOT NULL CHECK (length(provenance) BETWEEN 1 AND 2000),
    created_by text NOT NULL REFERENCES access.users(id),
    created_at timestamptz NOT NULL,
    PRIMARY KEY (organization_id, id)
);

CREATE TABLE preparation.drafts (
    organization_id uuid NOT NULL REFERENCES access.organizations(id),
    id uuid NOT NULL,
    brand_id uuid NOT NULL,
    current_revision integer NOT NULL CHECK (current_revision > 0),
    requested_review_revision integer NULL,
    active_approval_id uuid NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (organization_id, id),
    FOREIGN KEY (organization_id, brand_id) REFERENCES preparation.brands(organization_id, id),
    CHECK (requested_review_revision IS NULL OR requested_review_revision = current_revision)
);

CREATE TABLE preparation.draft_revisions (
    organization_id uuid NOT NULL,
    draft_id uuid NOT NULL,
    brand_id uuid NOT NULL,
    revision integer NOT NULL CHECK (revision > 0),
    snapshot jsonb NOT NULL,
    created_by text NOT NULL REFERENCES access.users(id),
    created_at timestamptz NOT NULL,
    PRIMARY KEY (organization_id, draft_id, revision),
    FOREIGN KEY (organization_id, draft_id) REFERENCES preparation.drafts(organization_id, id),
    FOREIGN KEY (organization_id, brand_id) REFERENCES preparation.brands(organization_id, id)
);
ALTER TABLE preparation.drafts ADD CONSTRAINT draft_current_revision_fk
    FOREIGN KEY (organization_id, id, current_revision)
    REFERENCES preparation.draft_revisions(organization_id, draft_id, revision)
    DEFERRABLE INITIALLY DEFERRED;

CREATE TABLE preparation.draft_media (
    organization_id uuid NOT NULL,
    draft_id uuid NOT NULL,
    revision integer NOT NULL,
    position integer NOT NULL CHECK (position BETWEEN 0 AND 9),
    media_id uuid NOT NULL,
    PRIMARY KEY (organization_id, draft_id, revision, position),
    UNIQUE (organization_id, draft_id, revision, media_id),
    FOREIGN KEY (organization_id, draft_id, revision)
        REFERENCES preparation.draft_revisions(organization_id, draft_id, revision),
    FOREIGN KEY (organization_id, media_id) REFERENCES preparation.media(organization_id, id)
);

CREATE TABLE preparation.approvals (
    organization_id uuid NOT NULL,
    id uuid NOT NULL,
    draft_id uuid NOT NULL,
    revision integer NOT NULL,
    approved_by text NOT NULL REFERENCES access.users(id),
    approved_at timestamptz NOT NULL,
    revoked_by text NULL REFERENCES access.users(id),
    revoked_at timestamptz NULL,
    reason text NULL,
    PRIMARY KEY (organization_id, id),
    UNIQUE (organization_id, draft_id, revision, id),
    FOREIGN KEY (organization_id, draft_id, revision)
        REFERENCES preparation.draft_revisions(organization_id, draft_id, revision),
    CHECK ((revoked_at IS NULL AND revoked_by IS NULL AND reason IS NULL)
        OR (revoked_at IS NOT NULL AND revoked_by IS NOT NULL AND length(reason) BETWEEN 1 AND 1000))
);
CREATE UNIQUE INDEX one_active_approval_per_revision
    ON preparation.approvals (organization_id, draft_id, revision) WHERE revoked_at IS NULL;
ALTER TABLE preparation.drafts ADD CONSTRAINT draft_active_approval_fk
    FOREIGN KEY (organization_id, id, current_revision, active_approval_id)
    REFERENCES preparation.approvals(organization_id, draft_id, revision, id)
    DEFERRABLE INITIALLY DEFERRED;

CREATE TABLE preparation.exports (
    organization_id uuid NOT NULL,
    id uuid NOT NULL,
    draft_id uuid NOT NULL,
    revision integer NOT NULL,
    approval_id uuid NOT NULL,
    idempotency_key uuid NOT NULL,
    request_hash text NOT NULL CHECK (request_hash ~ '^[a-f0-9]{64}$'),
    bytes bytea NOT NULL CHECK (octet_length(bytes) BETWEEN 1 AND 28311552),
    sha256 text NOT NULL CHECK (sha256 ~ '^[a-f0-9]{64}$'),
    created_by text NOT NULL REFERENCES access.users(id),
    created_at timestamptz NOT NULL,
    PRIMARY KEY (organization_id, id),
    UNIQUE (organization_id, idempotency_key),
    FOREIGN KEY (organization_id, draft_id, revision, approval_id)
        REFERENCES preparation.approvals(organization_id, draft_id, revision, id)
);

CREATE FUNCTION preparation.reject_immutable_change() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'Preparation snapshots are immutable' USING ERRCODE = '55000';
END;
$$;
CREATE TRIGGER media_immutable BEFORE UPDATE OR DELETE ON preparation.media
    FOR EACH ROW EXECUTE FUNCTION preparation.reject_immutable_change();
CREATE TRIGGER revision_immutable BEFORE UPDATE OR DELETE ON preparation.draft_revisions
    FOR EACH ROW EXECUTE FUNCTION preparation.reject_immutable_change();
CREATE TRIGGER draft_media_immutable BEFORE UPDATE OR DELETE ON preparation.draft_media
    FOR EACH ROW EXECUTE FUNCTION preparation.reject_immutable_change();
CREATE TRIGGER export_immutable BEFORE UPDATE OR DELETE ON preparation.exports
    FOR EACH ROW EXECUTE FUNCTION preparation.reject_immutable_change();

CREATE INDEX drafts_by_brand ON preparation.drafts(organization_id, brand_id, updated_at DESC);
CREATE INDEX media_by_created ON preparation.media(organization_id, created_at DESC);
