-- Deliberately fail validation if PDF editions exist; never discard stored user artifacts.
ALTER TABLE commerce.media DROP CONSTRAINT media_content_type_check;
ALTER TABLE commerce.media DROP CONSTRAINT media_bytes_check;
ALTER TABLE commerce.media ADD CONSTRAINT media_content_type_check
    CHECK(content_type IN ('image/png','image/jpeg','image/webp'));
ALTER TABLE commerce.media ADD CONSTRAINT media_bytes_check
    CHECK(octet_length(bytes) BETWEEN 1 AND 15728640);
