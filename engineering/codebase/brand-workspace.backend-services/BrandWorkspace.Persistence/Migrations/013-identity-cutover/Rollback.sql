-- Reverse 013-identity-cutover: forward-only, so a no-op. The dropped sessions, SSO grants, Telegram links, memberships
-- and EF identity rows cannot be recreated, and no Desk release since the Identity cutover reads them. Rolling back only
-- returns 013 to pending; applying it again finds nothing to drop.

SELECT 1;
