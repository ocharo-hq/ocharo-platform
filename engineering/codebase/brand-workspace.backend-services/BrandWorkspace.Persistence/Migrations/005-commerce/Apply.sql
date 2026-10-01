CREATE SCHEMA commerce;
CREATE TABLE commerce.profiles (
 organization_id uuid NOT NULL, brand_id uuid NOT NULL, current_revision integer NOT NULL,
 public_slug text UNIQUE, active_release_id uuid, updated_at timestamptz NOT NULL,
 PRIMARY KEY(organization_id,brand_id), UNIQUE(brand_id),
 FOREIGN KEY(organization_id,brand_id) REFERENCES preparation.brands(organization_id,id)
);
CREATE TABLE commerce.revisions (
 organization_id uuid NOT NULL, brand_id uuid NOT NULL, revision integer NOT NULL,
 snapshot jsonb NOT NULL, actor text NOT NULL, created_at timestamptz NOT NULL,
 PRIMARY KEY(organization_id,brand_id,revision),
 FOREIGN KEY(organization_id,brand_id) REFERENCES commerce.profiles(organization_id,brand_id)
);
CREATE TABLE commerce.releases (
 id uuid PRIMARY KEY, organization_id uuid NOT NULL, brand_id uuid NOT NULL,
 revision integer NOT NULL, source_revision integer NOT NULL, snapshot jsonb NOT NULL,
 note text NOT NULL, actor text NOT NULL, published_at timestamptz NOT NULL,
 withdrawn_at timestamptz, withdrawn_by text, withdrawal_reason text,
 UNIQUE(organization_id,brand_id,revision),
 FOREIGN KEY(organization_id,brand_id,source_revision) REFERENCES commerce.revisions(organization_id,brand_id,revision)
);
CREATE TABLE commerce.media (
 brand_id uuid NOT NULL REFERENCES commerce.profiles(brand_id), sha256 text NOT NULL,
 content_type text NOT NULL CHECK(content_type IN ('image/png','image/jpeg','image/webp')),
 bytes bytea NOT NULL CHECK(octet_length(bytes) BETWEEN 1 AND 15728640),
 PRIMARY KEY(brand_id,sha256), CHECK(sha256 ~ '^[a-f0-9]{64}$')
);
CREATE TABLE commerce.creative_imports (
 organization_id uuid NOT NULL, id uuid NOT NULL, brand_id uuid NOT NULL,
 actor text NOT NULL, created_at timestamptz NOT NULL, snapshot jsonb NOT NULL,
 PRIMARY KEY(organization_id,id),
 FOREIGN KEY(organization_id,brand_id) REFERENCES preparation.brands(organization_id,id)
);
