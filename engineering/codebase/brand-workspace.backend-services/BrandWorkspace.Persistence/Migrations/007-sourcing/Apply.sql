CREATE SCHEMA sourcing;

CREATE TABLE sourcing.suppliers (
    organization_id uuid NOT NULL REFERENCES access.organizations(id),
    id uuid NOT NULL,
    import_key text CHECK (import_key ~ '^[a-z0-9][a-z0-9-]{0,63}$'),
    name text NOT NULL CHECK (length(btrim(name)) BETWEEN 1 AND 160),
    stage text NOT NULL CHECK (stage IN ('lead', 'contacted', 'replied', 'quoted', 'qualified', 'dropped')),
    country text CHECK (country ~ '^[A-Z]{2}$'),
    city text CHECK (length(btrim(city)) BETWEEN 1 AND 120),
    website text CHECK (length(website) BETWEEN 1 AND 2048),
    notes text NOT NULL CHECK (length(notes) <= 8000),
    revision integer NOT NULL CHECK (revision > 0),
    created_by text NOT NULL REFERENCES access.users(id),
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (organization_id, id),
    UNIQUE (organization_id, import_key)
);

CREATE TABLE sourcing.people (
    organization_id uuid NOT NULL,
    id uuid NOT NULL,
    supplier_id uuid NOT NULL,
    name text NOT NULL CHECK (length(btrim(name)) BETWEEN 1 AND 160),
    role text CHECK (length(btrim(role)) BETWEEN 1 AND 120),
    notes text NOT NULL CHECK (length(notes) <= 4000),
    revision integer NOT NULL CHECK (revision > 0),
    created_by text NOT NULL REFERENCES access.users(id),
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (organization_id, id),
    UNIQUE (organization_id, supplier_id, id),
    FOREIGN KEY (organization_id, supplier_id) REFERENCES sourcing.suppliers(organization_id, id)
);

CREATE TABLE sourcing.contact_routes (
    organization_id uuid NOT NULL,
    id uuid NOT NULL,
    supplier_id uuid NOT NULL,
    person_id uuid,
    channel text NOT NULL CHECK (channel IN (
        'whatsapp', 'email', 'phone', 'form', 'website', 'instagram', 'facebook', 'telegram', 'marketplace', 'other')),
    destination text NOT NULL CHECK (length(btrim(destination)) BETWEEN 1 AND 512),
    label text CHECK (length(btrim(label)) BETWEEN 1 AND 120),
    source_url text CHECK (length(source_url) BETWEEN 1 AND 2048),
    verified boolean NOT NULL,
    is_primary boolean NOT NULL,
    notes text NOT NULL CHECK (length(notes) <= 4000),
    revision integer NOT NULL CHECK (revision > 0),
    created_by text NOT NULL REFERENCES access.users(id),
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (organization_id, id),
    UNIQUE (organization_id, supplier_id, channel, destination),
    FOREIGN KEY (organization_id, supplier_id) REFERENCES sourcing.suppliers(organization_id, id),
    -- A route may only name a person of its own supplier; deleting the person keeps the route.
    FOREIGN KEY (organization_id, supplier_id, person_id)
        REFERENCES sourcing.people(organization_id, supplier_id, id) ON DELETE SET NULL (person_id)
);

CREATE UNIQUE INDEX contact_routes_one_primary
    ON sourcing.contact_routes(organization_id, supplier_id) WHERE is_primary;

-- Original bytes are content-addressed per organization and kept apart from list queries.
CREATE TABLE sourcing.media_contents (
    organization_id uuid NOT NULL REFERENCES access.organizations(id),
    sha256 text NOT NULL CHECK (sha256 ~ '^[a-f0-9]{64}$'),
    bytes bytea NOT NULL CHECK (octet_length(bytes) BETWEEN 1 AND 67108864),
    thumbnail bytea CHECK (octet_length(thumbnail) BETWEEN 1 AND 1048576),
    PRIMARY KEY (organization_id, sha256)
);

CREATE TABLE sourcing.media (
    organization_id uuid NOT NULL,
    id uuid NOT NULL,
    supplier_id uuid,
    sha256 text NOT NULL,
    content_type text NOT NULL CHECK (content_type IN (
        'image/jpeg', 'image/png', 'image/webp', 'application/pdf', 'video/mp4')),
    byte_size integer NOT NULL CHECK (byte_size BETWEEN 1 AND 67108864),
    width integer CHECK (width BETWEEN 1 AND 8192),
    height integer CHECK (height BETWEEN 1 AND 8192),
    has_thumbnail boolean NOT NULL,
    capture text NOT NULL CHECK (capture IN ('original', 'screenshot', 'derived')),
    original_name text NOT NULL CHECK (length(btrim(original_name)) BETWEEN 1 AND 255),
    caption text NOT NULL CHECK (length(caption) <= 2000),
    source_label text CHECK (length(btrim(source_label)) BETWEEN 1 AND 200),
    source_message_id text CHECK (length(btrim(source_message_id)) BETWEEN 1 AND 200),
    received_at timestamptz,
    taken_at timestamptz,
    has_location boolean NOT NULL,
    perceptual_hash bigint,
    revision integer NOT NULL CHECK (revision > 0),
    created_by text NOT NULL REFERENCES access.users(id),
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (organization_id, id),
    UNIQUE (organization_id, sha256),
    FOREIGN KEY (organization_id, sha256) REFERENCES sourcing.media_contents(organization_id, sha256),
    FOREIGN KEY (organization_id, supplier_id) REFERENCES sourcing.suppliers(organization_id, id)
);

CREATE INDEX suppliers_by_name ON sourcing.suppliers(organization_id, lower(name), id);
CREATE INDEX people_by_supplier ON sourcing.people(organization_id, supplier_id, lower(name));
CREATE INDEX media_by_supplier
    ON sourcing.media(organization_id, supplier_id, received_at DESC NULLS LAST, created_at DESC, id);
CREATE INDEX media_with_perceptual_hash
    ON sourcing.media(organization_id) INCLUDE (perceptual_hash) WHERE perceptual_hash IS NOT NULL;
