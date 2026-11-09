-- WoW2 Files (planning/files-adoption.md #13). Each bytes column gains the Files file that holds its bytes: a row stored
-- with Files carries file_id and no bytes, a row stored before keeps its bytes until t9's retire step drops the columns.
-- 011 is reserved for the Identity cutover. Every statement is DDL, so no row trigger fires.

ALTER TABLE preparation.media ADD COLUMN file_id text CHECK (length(file_id) BETWEEN 1 AND 100);
-- Without bytes, the size a media snapshot records needs a column of its own.
ALTER TABLE preparation.media ADD COLUMN byte_length bigint CHECK (byte_length BETWEEN 1 AND 5242880);
ALTER TABLE preparation.media ALTER COLUMN bytes DROP NOT NULL;
ALTER TABLE preparation.media ADD CONSTRAINT media_bytes_or_file
    CHECK (bytes IS NOT NULL OR (file_id IS NOT NULL AND byte_length IS NOT NULL));

ALTER TABLE preparation.exports ADD COLUMN file_id text CHECK (length(file_id) BETWEEN 1 AND 100);
ALTER TABLE preparation.exports ALTER COLUMN bytes DROP NOT NULL;
ALTER TABLE preparation.exports ADD CONSTRAINT export_bytes_or_file CHECK (bytes IS NOT NULL OR file_id IS NOT NULL);

-- preparation.media and preparation.exports reject every UPDATE and DELETE (003), so a row stored before Files never takes
-- its file id in place. The boot backfill copies the id into an insert-only record keyed like the row, and every read
-- repoints through it: COALESCE(row.file_id, record.file_id).
CREATE TABLE preparation.media_files (
    organization_id uuid NOT NULL,
    media_id uuid NOT NULL,
    file_id text NOT NULL CHECK (length(file_id) BETWEEN 1 AND 100),
    PRIMARY KEY (organization_id, media_id),
    FOREIGN KEY (organization_id, media_id) REFERENCES preparation.media(organization_id, id)
);
CREATE TABLE preparation.export_files (
    organization_id uuid NOT NULL,
    export_id uuid NOT NULL,
    file_id text NOT NULL CHECK (length(file_id) BETWEEN 1 AND 100),
    PRIMARY KEY (organization_id, export_id),
    FOREIGN KEY (organization_id, export_id) REFERENCES preparation.exports(organization_id, id)
);
CREATE TRIGGER media_file_immutable BEFORE UPDATE OR DELETE ON preparation.media_files
    FOR EACH ROW EXECUTE FUNCTION preparation.reject_immutable_change();
CREATE TRIGGER export_file_immutable BEFORE UPDATE OR DELETE ON preparation.export_files
    FOR EACH ROW EXECUTE FUNCTION preparation.reject_immutable_change();

-- Commerce and sourcing rows are mutable: the backfill sets file_id in place.
ALTER TABLE commerce.media ADD COLUMN file_id text CHECK (length(file_id) BETWEEN 1 AND 100);
ALTER TABLE commerce.media ALTER COLUMN bytes DROP NOT NULL;
ALTER TABLE commerce.media ADD CONSTRAINT media_bytes_or_file CHECK (bytes IS NOT NULL OR file_id IS NOT NULL);

-- A sourcing thumbnail stored with Files is the original's 480 px variant, so new rows keep no thumbnail bytes (#10).
ALTER TABLE sourcing.media_contents ADD COLUMN file_id text CHECK (length(file_id) BETWEEN 1 AND 100);
ALTER TABLE sourcing.media_contents ALTER COLUMN bytes DROP NOT NULL;
ALTER TABLE sourcing.media_contents ADD CONSTRAINT media_contents_bytes_or_file CHECK (bytes IS NOT NULL OR file_id IS NOT NULL);
