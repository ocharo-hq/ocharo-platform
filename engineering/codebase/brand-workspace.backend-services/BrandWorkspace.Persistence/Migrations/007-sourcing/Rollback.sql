-- Deliberately fail if supplier records or shared media exist; never discard private sourcing evidence.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM sourcing.suppliers) OR EXISTS (SELECT 1 FROM sourcing.media_contents) THEN
        RAISE EXCEPTION 'Sourcing records exist; export them before rolling back' USING ERRCODE = '55000';
    END IF;
END;
$$;
DROP SCHEMA sourcing CASCADE;
