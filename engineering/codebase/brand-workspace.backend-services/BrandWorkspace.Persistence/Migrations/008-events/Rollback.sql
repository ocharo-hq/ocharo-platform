-- Deliberately fail if event records exist; never discard statuses and notes edited in Desk.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM events.showcase_events) THEN
        RAISE EXCEPTION 'Event records exist; export them before rolling back' USING ERRCODE = '55000';
    END IF;
END;
$$;
DROP SCHEMA events CASCADE;
