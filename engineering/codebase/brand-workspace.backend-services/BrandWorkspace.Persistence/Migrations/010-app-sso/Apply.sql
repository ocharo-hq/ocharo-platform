CREATE TABLE IF NOT EXISTS access.sso_codes (
 code_hash text PRIMARY KEY,client_id text NOT NULL,redirect_uri text NOT NULL,challenge text NOT NULL,
 user_id text NOT NULL REFERENCES access.users(id) ON DELETE CASCADE,
 session_id uuid NOT NULL REFERENCES access.sessions(id) ON DELETE CASCADE,
 organization_id uuid NOT NULL REFERENCES access.organizations(id) ON DELETE CASCADE,
 expires_at timestamptz NOT NULL,session_expires_at timestamptz NOT NULL,consumed_at timestamptz NULL
);
CREATE INDEX IF NOT EXISTS ix_sso_codes_expires ON access.sso_codes(expires_at);
CREATE TABLE IF NOT EXISTS access.sso_grants (
 token_hash text PRIMARY KEY,client_id text NOT NULL,
 user_id text NOT NULL REFERENCES access.users(id) ON DELETE CASCADE,
 session_id uuid NOT NULL REFERENCES access.sessions(id) ON DELETE CASCADE,
 organization_id uuid NOT NULL REFERENCES access.organizations(id) ON DELETE CASCADE,
 expires_at timestamptz NOT NULL,revoked_at timestamptz NULL
);
CREATE INDEX IF NOT EXISTS ix_sso_grants_session ON access.sso_grants(session_id);
CREATE INDEX IF NOT EXISTS ix_sso_grants_expires ON access.sso_grants(expires_at);
