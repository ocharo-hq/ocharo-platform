CREATE SCHEMA events;

-- Every event where Ocharo could show its products; Desk records facts and never applies or pays.
CREATE TABLE events.showcase_events (
    organization_id uuid NOT NULL REFERENCES access.organizations(id),
    id uuid NOT NULL,
    import_key text CHECK (import_key ~ '^[a-z0-9][a-z0-9-]{0,63}$'),
    title text NOT NULL CHECK (length(btrim(title)) BETWEEN 1 AND 200),
    organizer text CHECK (length(btrim(organizer)) BETWEEN 1 AND 160),
    scope text NOT NULL CHECK (scope IN ('local', 'regional', 'international')),
    format text NOT NULL CHECK (format IN (
        'runway', 'trade-fair', 'market', 'exhibition', 'competition', 'festival', 'conference', 'other')),
    country text CHECK (country ~ '^[A-Z]{2}$'),
    city text CHECK (length(btrim(city)) BETWEEN 1 AND 120),
    venue text CHECK (length(btrim(venue)) BETWEEN 1 AND 200),
    starts_on date CHECK (starts_on BETWEEN DATE '2000-01-01' AND DATE '2100-12-31'),
    ends_on date CHECK (ends_on BETWEEN DATE '2000-01-01' AND DATE '2100-12-31'),
    date_precision text NOT NULL CHECK (date_precision IN ('day', 'month', 'estimated', 'unknown')),
    application_deadline date CHECK (application_deadline BETWEEN DATE '2000-01-01' AND DATE '2100-12-31'),
    participation text NOT NULL CHECK (length(participation) <= 2000),
    cost text CHECK (length(btrim(cost)) BETWEEN 1 AND 500),
    website text CHECK (length(website) BETWEEN 1 AND 2048),
    source_url text CHECK (length(source_url) BETWEEN 1 AND 2048),
    status text NOT NULL CHECK (status IN (
        'candidate', 'shortlisted', 'applying', 'applied', 'confirmed', 'attended', 'skipped', 'rejected')),
    priority text NOT NULL CHECK (priority IN ('high', 'normal', 'low')),
    notes text NOT NULL CHECK (length(notes) <= 8000),
    revision integer NOT NULL CHECK (revision > 0),
    created_by text NOT NULL REFERENCES access.users(id),
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (organization_id, id),
    UNIQUE (organization_id, import_key),
    CHECK (ends_on IS NULL OR (starts_on IS NOT NULL AND ends_on >= starts_on)),
    -- Unknown precision means no dates; every other precision needs a start date.
    CHECK ((date_precision = 'unknown') = (starts_on IS NULL)),
    -- Month and estimated precision store the 1st of the announced or inferred month.
    CHECK (date_precision NOT IN ('month', 'estimated') OR extract(day FROM starts_on) = 1)
);

CREATE INDEX showcase_events_by_start
    ON events.showcase_events(organization_id, starts_on NULLS LAST, lower(title), id);
CREATE INDEX showcase_events_by_deadline
    ON events.showcase_events(organization_id, application_deadline) WHERE application_deadline IS NOT NULL;
