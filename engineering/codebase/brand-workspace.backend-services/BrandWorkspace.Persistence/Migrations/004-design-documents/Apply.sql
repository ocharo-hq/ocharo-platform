CREATE SCHEMA design_documents;

CREATE TABLE design_documents.documents (
    organization_id uuid NOT NULL REFERENCES access.organizations(id),
    id uuid NOT NULL,
    kind text NOT NULL CHECK (kind IN ('catalogue', 'business-card')),
    current_revision integer NOT NULL CHECK (current_revision > 0),
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (organization_id, id)
);

CREATE TABLE design_documents.revisions (
    organization_id uuid NOT NULL,
    document_id uuid NOT NULL,
    revision integer NOT NULL CHECK (revision > 0),
    title text NOT NULL CHECK (length(btrim(title)) BETWEEN 1 AND 160),
    snapshot json NOT NULL CHECK (
        json_typeof(snapshot) = 'object' AND octet_length(snapshot::text) <= 1048576),
    archived boolean NOT NULL,
    saved_by text NOT NULL REFERENCES access.users(id),
    saved_at timestamptz NOT NULL,
    PRIMARY KEY (organization_id, document_id, revision),
    FOREIGN KEY (organization_id, document_id)
        REFERENCES design_documents.documents(organization_id, id)
);

ALTER TABLE design_documents.documents ADD CONSTRAINT document_current_revision_fk
    FOREIGN KEY (organization_id, id, current_revision)
    REFERENCES design_documents.revisions(organization_id, document_id, revision)
    DEFERRABLE INITIALLY DEFERRED;

CREATE FUNCTION design_documents.reject_immutable_change() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'Design document revisions are immutable' USING ERRCODE = '55000';
END;
$$;

CREATE TRIGGER document_revision_immutable BEFORE UPDATE OR DELETE ON design_documents.revisions
    FOR EACH ROW EXECUTE FUNCTION design_documents.reject_immutable_change();

CREATE INDEX documents_by_kind_updated
    ON design_documents.documents(organization_id, kind, updated_at DESC, id);
CREATE INDEX documents_by_updated
    ON design_documents.documents(organization_id, updated_at DESC, id);
