-- Store approved catalogue exports beside existing content-addressed media.
ALTER TABLE commerce.media DROP CONSTRAINT media_content_type_check;
ALTER TABLE commerce.media DROP CONSTRAINT media_bytes_check;
ALTER TABLE commerce.media ADD CONSTRAINT media_content_type_check
    CHECK(content_type IN ('image/png','image/jpeg','image/webp','application/pdf'));
ALTER TABLE commerce.media ADD CONSTRAINT media_bytes_check
    CHECK(octet_length(bytes) BETWEEN 1 AND CASE WHEN content_type='application/pdf' THEN 67108864 ELSE 15728640 END);
