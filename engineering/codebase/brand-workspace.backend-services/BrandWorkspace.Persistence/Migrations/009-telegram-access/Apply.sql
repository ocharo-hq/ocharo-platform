CREATE TABLE access.telegram_links (
    user_id text PRIMARY KEY REFERENCES access.users(id),
    telegram_id bigint NOT NULL UNIQUE CHECK (telegram_id > 0),
    phone text NOT NULL UNIQUE CHECK (phone ~ '^\+[1-9][0-9]{6,14}$'),
    linked_at timestamptz NOT NULL
);
CREATE TABLE access.telegram_link_requests (
    token_hash text PRIMARY KEY,
    user_id text NOT NULL REFERENCES access.users(id),
    session_id uuid NOT NULL REFERENCES access.sessions(id),
    expires_at timestamptz NOT NULL,
    telegram_id bigint,
    consumed boolean NOT NULL DEFAULT false
);
CREATE INDEX telegram_link_requests_user ON access.telegram_link_requests(user_id);
CREATE UNIQUE INDEX telegram_link_requests_chat ON access.telegram_link_requests(telegram_id) WHERE telegram_id IS NOT NULL AND NOT consumed;
CREATE TABLE access.telegram_codes (
    id uuid PRIMARY KEY,
    subject text NOT NULL,
    scope text NOT NULL,
    protected_code text NOT NULL,
    created_at timestamptz NOT NULL,
    expires_at timestamptz NOT NULL,
    attempts integer NOT NULL DEFAULT 0,
    consumed boolean NOT NULL DEFAULT false
);
CREATE INDEX telegram_codes_subject ON access.telegram_codes(subject,scope,created_at DESC);
