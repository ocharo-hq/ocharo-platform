-- 001-baseline — drops the key ring. Every session cookie it encrypted stops reading, so everyone signs in again; run
-- only locally or by the guarded `rollback` verb.

DROP TABLE IF EXISTS {{schema}}.data_protection_keys;
