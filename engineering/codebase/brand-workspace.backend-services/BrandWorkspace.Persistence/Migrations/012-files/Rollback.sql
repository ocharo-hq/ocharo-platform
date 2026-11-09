-- Deliberately fail once any row keeps its bytes only in WoW2 Files; NOT NULL could not hold again.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM preparation.media WHERE bytes IS NULL)
        OR EXISTS (SELECT 1 FROM preparation.exports WHERE bytes IS NULL)
        OR EXISTS (SELECT 1 FROM commerce.media WHERE bytes IS NULL)
        OR EXISTS (SELECT 1 FROM sourcing.media_contents WHERE bytes IS NULL) THEN
        RAISE EXCEPTION 'Rows stored only in WoW2 Files exist; restore their bytes before rolling back' USING ERRCODE = '55000';
    END IF;
END;
$$;
ALTER TABLE sourcing.media_contents DROP CONSTRAINT media_contents_bytes_or_file;
ALTER TABLE sourcing.media_contents ALTER COLUMN bytes SET NOT NULL;
ALTER TABLE sourcing.media_contents DROP COLUMN file_id;
ALTER TABLE commerce.media DROP CONSTRAINT media_bytes_or_file;
ALTER TABLE commerce.media ALTER COLUMN bytes SET NOT NULL;
ALTER TABLE commerce.media DROP COLUMN file_id;
DROP TABLE preparation.export_files;
DROP TABLE preparation.media_files;
ALTER TABLE preparation.exports DROP CONSTRAINT export_bytes_or_file;
ALTER TABLE preparation.exports ALTER COLUMN bytes SET NOT NULL;
ALTER TABLE preparation.exports DROP COLUMN file_id;
ALTER TABLE preparation.media DROP CONSTRAINT media_bytes_or_file;
ALTER TABLE preparation.media ALTER COLUMN bytes SET NOT NULL;
ALTER TABLE preparation.media DROP COLUMN byte_length;
ALTER TABLE preparation.media DROP COLUMN file_id;
