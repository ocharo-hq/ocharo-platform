-- @contract since=v0.2.7
-- 013-identity-cutover: retires Desk's own sign-in storage (convention sweep D-31). Every Ocharo app signs in through
-- Ocharo's WoW2 Identity since 2026-11-05; Desk keeps only its projection of Identity's people (access.users) and
-- organizations (access.organizations), whose rows the authored records' foreign keys need.
-- Drops, with every row: access.sessions (002), the sso_* tables (010), the telegram_* tables (009), ASP.NET Core
-- Identity's six identity_* tables (002) and access.memberships (002). The developer approved dropping the data
-- outright, with no archive (2026-11-09, asked "dropping or archiving Desk's old sign-in tables (deletes data)",
-- answered verbatim "completely fine").
-- Numbered 013, not the 011 reserved for it: deployed databases already hold 012-files, and the migrator refuses a
-- pending ordinal below the applied level outside a local run. 011 stays a gap.
-- Each table drops after every table that references it, without CASCADE, so an unforeseen dependent fails the run
-- instead of vanishing with it; no kept table references a dropped one. A DROP TABLE takes its own indexes and
-- constraints, its foreign keys into access.users and access.organizations included.
-- Forward-only: Rollback.sql is a no-op. Idempotent: IF EXISTS finds nothing on a second run.

-- 010: SSO codes and grants reference sessions, users and organizations.
DROP TABLE IF EXISTS access.sso_grants;
DROP TABLE IF EXISTS access.sso_codes;

-- 009: link requests reference sessions and users; links reference users; codes reference nothing.
DROP TABLE IF EXISTS access.telegram_link_requests;
DROP TABLE IF EXISTS access.telegram_links;
DROP TABLE IF EXISTS access.telegram_codes;

-- 002: sessions and memberships, now that nothing references sessions.
DROP TABLE IF EXISTS access.sessions;
DROP TABLE IF EXISTS access.memberships;

-- 002: the EF identity store; the user tables reference users and roles, the role claims reference roles.
DROP TABLE IF EXISTS access.identity_user_tokens;
DROP TABLE IF EXISTS access.identity_user_roles;
DROP TABLE IF EXISTS access.identity_user_logins;
DROP TABLE IF EXISTS access.identity_user_claims;
DROP TABLE IF EXISTS access.identity_role_claims;
DROP TABLE IF EXISTS access.identity_roles;
