CREATE TABLE {{schema}}.video_reviews (
    organization_id UUID NOT NULL,
    video_id TEXT NOT NULL,
    status TEXT NOT NULL CHECK (status IN ('unreviewed', 'useful', 'skipped')),
    note TEXT NOT NULL CHECK (length(note) <= 4000),
    reviewer_subject TEXT NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (organization_id, video_id)
);

CREATE TABLE {{schema}}.data_protection_keys (
    friendly_name TEXT PRIMARY KEY,
    xml TEXT NOT NULL
);
