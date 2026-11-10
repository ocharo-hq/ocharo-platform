-- Reverse 014-events-retire: forward-only, so a no-op. The dropped events and their tracking live in WoW2 Events, which
-- Desk reads since v0.3.5; no Desk release from v0.3.8 on reads Desk's own table. Rolling back only returns 014 to
-- pending; applying it again finds nothing to drop.

SELECT 1;
