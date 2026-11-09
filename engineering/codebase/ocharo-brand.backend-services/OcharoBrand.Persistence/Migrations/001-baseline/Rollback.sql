-- 001-baseline — drops Brand's tables. Every draft, release, logo, media row and key in them is lost; run only locally or
-- by the guarded `rollback` verb.

DROP TABLE IF EXISTS {{schema}}.data_protection_keys;
DROP TABLE IF EXISTS {{schema}}.consumers;
DROP TABLE IF EXISTS {{schema}}.media_blobs;
DROP TABLE IF EXISTS {{schema}}.appearances;
DROP TABLE IF EXISTS {{schema}}.releases;
DROP TABLE IF EXISTS {{schema}}.logo_versions;
DROP TABLE IF EXISTS {{schema}}.draft_revisions;
DROP TABLE IF EXISTS {{schema}}.drafts;
