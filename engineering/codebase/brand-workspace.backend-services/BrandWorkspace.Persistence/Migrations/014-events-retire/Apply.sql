-- @contract since=v0.3.8
-- 014-events-retire: retires Desk's own events store (v0.3 Iteration 5). Desk's Events page reads WoW2 Events since
-- v0.3.5 (2026-11-10); the 68 rows local Desk held were exported, imported into Events on dev and reconciled 68 of 68,
-- and the export bundle stays outside Git. The developer approved dropping the table and its rows outright, with no
-- rollback (2026-11-10, asked to retire Desk's old Events table and code, answered verbatim "yes drop completely").
-- Drops, with every row: events.showcase_events (008) and the schema events. No other table references it; without
-- CASCADE, an unforeseen dependent fails the run instead of vanishing with it.
-- Forward-only: Rollback.sql is a no-op. Idempotent: IF EXISTS finds nothing on a second run.

DROP TABLE IF EXISTS events.showcase_events;
DROP SCHEMA IF EXISTS events;
