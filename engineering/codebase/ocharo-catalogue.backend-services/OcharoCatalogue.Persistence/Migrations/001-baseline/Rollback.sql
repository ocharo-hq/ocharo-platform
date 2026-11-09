-- 001-baseline — drops Catalogue's tables. Every product, catalogue, release and key in them is lost; run only locally
-- or by the guarded `rollback` verb.

DROP TABLE IF EXISTS {{schema}}.data_protection_keys;
DROP TABLE IF EXISTS {{schema}}.catalogue_release_files;
DROP TABLE IF EXISTS {{schema}}.catalogue_release_assets;
DROP TABLE IF EXISTS {{schema}}.catalogue_publications;
DROP TABLE IF EXISTS {{schema}}.catalogue_releases;
DROP TABLE IF EXISTS {{schema}}.catalogue_design_revisions;
DROP TABLE IF EXISTS {{schema}}.catalogue_designs;
DROP TABLE IF EXISTS {{schema}}.settings;
DROP TABLE IF EXISTS {{schema}}.media_blobs;
DROP TABLE IF EXISTS {{schema}}.collections;
DROP TABLE IF EXISTS {{schema}}.product_revisions;
DROP TABLE IF EXISTS {{schema}}.products;
