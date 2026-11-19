#!/usr/bin/env bash
# events-e2e-smoke.sh -- one record through SCFD -> WoW2 Events -> Ocharo's Events app, all local (E29).
#
# Builds the three services from their working trees, starts them with a local Ocharo Identity on one throwaway
# PostgreSQL, provisions Ocharo's SCFD selector subscription (`ocharo setup ... --selector`) and the one Events intake
# source bound to it (`Filter: selector:ocharo-events`), delivers one tagged fixture event, polls Events' intake and
# reads the event through Ocharo Events' feed with a signed-in test session. Then it stops every container it started
# by name, removes the network and its temporary files, and checks that nothing is left.
#
#   engineering/scripts/events-e2e-smoke.sh            exits 0 and prints the event's title, dates, city, source link
#
# Needs Docker, python3 (3.9+), openssl and uuidgen on the Mac; nothing else, and no real credential: every secret is
# generated for the run. Environment (all optional):
#   E2E_HARVEST_REPO  E2E_EVENTS_REPO   the SCFD and WoW2 Events checkouts (default: the workspace's wow-two-sdk-beta ones)
#   E2E_SKIP_BUILD=1                    reuse the ocharo-e2e-*:local images of an earlier run
#   E2E_PORT_BASE                       first of five loopback ports (default 18450: identity, SCFD, Events, Ocharo Events, PostgreSQL)
#   E2E_IDENTITY_IMAGE                  Identity's image (default: the tag Ocharo's deploy.yml pins)
#   E2E_FIXTURE                         another fixture file (default: events-e2e-fixture.json beside this script)
#   E2E_KEEP=1                          leave the containers and temp files for inspection (stop them by name afterwards)
# Documented in engineering/deployment/deployment.md § Checks.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OC="$(cd "$HERE/../.." && pwd)"
WORKBENCH="$(cd "$OC/../../../../../.." && pwd)"
HARVEST_REPO="${E2E_HARVEST_REPO:-$WORKBENCH/wow-two-sdk-beta/wow-two-sdk-beta.harvest}"
EVENTS_REPO="${E2E_EVENTS_REPO:-$WORKBENCH/wow-two-sdk-beta/wow-two-sdk-beta.events}"
DRIVER="$HERE/events-e2e-drive.py"
FIXTURE="${E2E_FIXTURE:-$HERE/events-e2e-fixture.json}"
WORKFLOW="$OC/engineering/deployment/events/ocharo-workflow.json"

P=ocharo-e2e
NET=$P-net
DB=$P-db
ID=$P-identity
SCFD=$P-scfd
EV=$P-events
OE=$P-ocharo-events
BASE="${E2E_PORT_BASE:-18450}"
PORT_ID=$BASE
PORT_SCFD=$((BASE + 1))
PORT_EV=$((BASE + 2))
PORT_OE=$((BASE + 3))
PORT_DB=$((BASE + 4))
ALL_CONTAINERS="$OE $EV $SCFD $ID $DB"

HARVEST_IMAGE=$P-harvest:local
EVENTS_IMAGE=$P-events:local
OE_IMAGE=$P-ocharo-events:local
IDENTITY_IMAGE="${E2E_IDENTITY_IMAGE:-$(sed -n 's/^ *image: *\(ghcr.io[^ ]*identity\/identity:[0-9.]*\).*/\1/p' "$OC/engineering/deployment/deploy.yml" | head -1)}"

say() { printf '\n==> %s\n' "$*"; }
die() { printf 'events-e2e: %s\n' "$*" >&2; exit 1; }
rand() { openssl rand -hex "${1:-16}"; }

for tool in docker python3 openssl uuidgen; do command -v "$tool" >/dev/null || die "$tool is not installed"; done
docker info >/dev/null 2>&1 || die "Docker is not running"
[ -n "$IDENTITY_IMAGE" ] || die "no Identity image: set E2E_IDENTITY_IMAGE"
for repo in "$HARVEST_REPO" "$EVENTS_REPO"; do [ -d "$repo/engineering/codebase" ] || die "no checkout at $repo"; done
[ -f "$WORKFLOW" ] || die "missing $WORKFLOW"

WORK="$(mktemp -d "${TMPDIR:-/tmp}/ocharo-e2e.XXXXXX")"
chmod 755 "$WORK"
mkdir -p "$WORK/tls"

# ---- cleanup: every container by name, the network, the temp files; then proof nothing is left ---------------------
cleanup() {
  rc=$?
  trap - EXIT INT TERM
  if [ "${E2E_KEEP:-0}" = 1 ]; then
    printf '\nevents-e2e: E2E_KEEP=1, left running: %s (network %s, files %s)\nStop with: docker rm -f %s; docker network rm %s\n' \
      "$ALL_CONTAINERS" "$NET" "$WORK" "$ALL_CONTAINERS" "$NET"
    exit "$rc"
  fi
  if [ "$rc" -ne 0 ]; then
    for name in $ID $SCFD $EV $OE; do
      if docker inspect "$name" >/dev/null 2>&1; then
        printf '\n---- %s (last log lines) ----\n' "$name" >&2
        docker logs --tail 40 "$name" 2>&1 | cut -c1-400 >&2 || true
      fi
    done
  fi
  say "cleanup"
  # shellcheck disable=SC2086
  docker rm -f $ALL_CONTAINERS >/dev/null 2>&1 || true
  docker ps -aq --filter "name=^${P}-job-" | xargs docker rm -f >/dev/null 2>&1 || true
  docker network rm "$NET" >/dev/null 2>&1 || true
  rm -rf "$WORK"
  left="$(docker ps -a --filter "name=^${P}-" --format '{{.Names}}' | tr '\n' ' ')"
  listening=""
  for port in $PORT_ID $PORT_SCFD $PORT_EV $PORT_OE $PORT_DB; do
    if lsof -nP -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1; then listening="$listening $port"; fi
  done
  networks="$(docker network ls --filter "name=^${NET}\$" --format '{{.Name}}')"
  if [ -n "$left$listening$networks" ]; then
    printf 'events-e2e: LEFT BEHIND: containers [%s] listening ports [%s] network [%s]\n' "$left" "$listening" "$networks" >&2
    [ "$rc" -eq 0 ] && rc=1
  else
    printf 'cleanup: no container of this run is left (docker ps -a), nothing listens on %s-%s (lsof)\n' "$PORT_ID" "$PORT_DB"
  fi
  exit "$rc"
}
trap cleanup EXIT INT TERM

# Leftovers of an aborted run use these exact names: clear them, then insist the ports are free.
# shellcheck disable=SC2086
docker rm -f $ALL_CONTAINERS >/dev/null 2>&1 || true
docker ps -aq --filter "name=^${P}-job-" | xargs docker rm -f >/dev/null 2>&1 || true
docker network rm "$NET" >/dev/null 2>&1 || true
for port in $PORT_ID $PORT_SCFD $PORT_EV $PORT_OE $PORT_DB; do
  if lsof -nP -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1; then die "port $port is taken: set E2E_PORT_BASE"; fi
done

# ---- test-only credentials and one throwaway CA -------------------------------------------------------------------
DB_PASSWORD="$(rand 16)"
ORG_ID="$(uuidgen | tr 'A-Z' 'a-z')"
OWNER_EMAIL="e2e-owner@example.test"
OWNER_PASSWORD="Smoke-$(rand 10)-pass1!"
SECRET_OE="$(rand 24)"
SECRET_EVENTS="$(rand 24)"
SECRET_DRIVER="$(rand 24)"
OPERATOR_KEY="hv_$(rand 16)"

say "TLS: a throwaway CA and one server certificate for the five names"
cat > "$WORK/tls/leaf.cnf" <<EOF
[req]
distinguished_name = dn
prompt = no
[dn]
CN = $P
[san]
subjectAltName = DNS:$ID,DNS:$SCFD,DNS:$EV,DNS:$OE,DNS:localhost,IP:127.0.0.1
extendedKeyUsage = serverAuth
basicConstraints = CA:FALSE
EOF
cat > "$WORK/tls/ca.cnf" <<EOF
[req]
distinguished_name = dn
prompt = no
x509_extensions = ca
[dn]
CN = $P-ca
[ca]
basicConstraints = critical,CA:TRUE
keyUsage = critical,keyCertSign,cRLSign
EOF
openssl req -x509 -newkey rsa:2048 -nodes -keyout "$WORK/tls/ca.key" -out "$WORK/tls/ca.pem" -days 2 -config "$WORK/tls/ca.cnf" 2>/dev/null
openssl req -newkey rsa:2048 -nodes -keyout "$WORK/tls/leaf.key" -out "$WORK/tls/leaf.csr" -config "$WORK/tls/leaf.cnf" 2>/dev/null
openssl x509 -req -in "$WORK/tls/leaf.csr" -CA "$WORK/tls/ca.pem" -CAkey "$WORK/tls/ca.key" -CAcreateserial \
  -out "$WORK/tls/leaf.pem" -days 2 -extfile "$WORK/tls/leaf.cnf" -extensions san 2>/dev/null
chmod 755 "$WORK/tls"
chmod 644 "$WORK/tls/ca.pem" "$WORK/tls/leaf.pem" "$WORK/tls/leaf.key"

URL_ID="https://$ID:8443/"
URL_SCFD="https://$SCFD:8443/"
URL_EV="https://$EV:8443/"
URL_OE="https://$OE:8443/"
CONN() { printf 'Host=%s;Port=5432;Database=%s;Username=smoke;Password=%s' "$DB" "$1" "$DB_PASSWORD"; }

cat > "$WORK/config.json" <<EOF
{
  "prefix": "$P",
  "ca": "$WORK/tls/ca.pem",
  "fixture": "$FIXTURE",
  "catalog": "ocharo-fashion",
  "orgId": "$ORG_ID",
  "owner": { "email": "$OWNER_EMAIL", "password": "$OWNER_PASSWORD" },
  "ports": { "$ID": $PORT_ID, "$SCFD": $PORT_SCFD, "$EV": $PORT_EV, "$OE": $PORT_OE },
  "clients": {
    "events": { "id": "events", "secret": "$SECRET_EVENTS" },
    "driver": { "id": "e2e-driver", "secret": "$SECRET_DRIVER" },
    "ocharo-events": { "id": "ocharo-events", "secret": "$SECRET_OE" }
  },
  "waitSeconds": 300,
  "state": {}
}
EOF
chmod 600 "$WORK/config.json"
drive() { python3 -I "$DRIVER" "$WORK/config.json" "$@"; }

# Every .NET service serves HTTPS on 8443 with the run's certificate and trusts only the run's CA.
tls_env() {
  cat <<EOF
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=https://+:8443;http://+:8080
Kestrel__Certificates__Default__Path=/e2e-tls/leaf.pem
Kestrel__Certificates__Default__KeyPath=/e2e-tls/leaf.key
SSL_CERT_FILE=/e2e-tls/ca.pem
AllowedHosts=*
Logging__LogLevel__Default=Information
EOF
}

# ---- images ---------------------------------------------------------------------------------------------------------
build() { # tag dockerfile context repo
  local log="$WORK/build-$1.log"
  docker build --build-arg APP_VERSION=0.0.0-smoke --build-arg "VCS_REF=$(git -C "$4" rev-parse HEAD 2>/dev/null || echo local)" \
    -f "$2" -t "$1" "$3" >"$log" 2>&1 || { tail -n 30 "$log" >&2; die "docker build of $1 failed"; }
  echo "built $1"
}
if [ "${E2E_SKIP_BUILD:-0}" = 1 ]; then
  say "images: reusing $HARVEST_IMAGE, $EVENTS_IMAGE and $OE_IMAGE (E2E_SKIP_BUILD=1)"
else
  say "images: building SCFD, Events and Ocharo Events from their working trees (cached layers make a rerun short)"
  build "$HARVEST_IMAGE" "$HARVEST_REPO/engineering/deployment/Dockerfile" "$HARVEST_REPO/engineering/codebase" "$HARVEST_REPO"
  build "$EVENTS_IMAGE" "$EVENTS_REPO/engineering/deployment/Dockerfile" "$EVENTS_REPO/engineering/codebase" "$EVENTS_REPO"
  build "$OE_IMAGE" "$OC/engineering/deployment/events/Dockerfile" "$OC/engineering/codebase" "$OC"
fi
docker image inspect "$IDENTITY_IMAGE" >/dev/null 2>&1 || docker pull --platform linux/amd64 "$IDENTITY_IMAGE" >/dev/null || die "cannot pull $IDENTITY_IMAGE"
echo "identity image $IDENTITY_IMAGE"

# ---- network and PostgreSQL ------------------------------------------------------------------------------------------
say "PostgreSQL ($DB, loopback $PORT_DB) with one database per service"
docker network create "$NET" >/dev/null
docker run -d --name "$DB" --hostname "$DB" --network "$NET" -p "127.0.0.1:$PORT_DB:5432" \
  -e POSTGRES_USER=smoke -e POSTGRES_PASSWORD="$DB_PASSWORD" -e POSTGRES_DB=postgres \
  --tmpfs /var/lib/postgresql postgres:18-alpine >/dev/null
for _ in $(seq 1 60); do
  docker exec "$DB" pg_isready -U smoke -d postgres >/dev/null 2>&1 && break
  sleep 1
done
docker exec "$DB" pg_isready -U smoke -d postgres >/dev/null || die "PostgreSQL did not start"
sleep 2
for database in identity harvest events ocharo_events; do
  docker exec "$DB" psql -U smoke -d postgres -qc "CREATE DATABASE $database" >/dev/null
done

job() { # tag image env-file args...   a one-off container, named, removed when it exits; stdin is $JOB_INPUT or empty
  local tag=$1 image=$2 envfile=$3 platform=()
  shift 3
  [ "$image" = "$IDENTITY_IMAGE" ] && platform=(--platform linux/amd64)
  docker run --rm -i --name "$P-job-$tag" --network "$NET" ${platform[@]+"${platform[@]}"} -v "$WORK/tls:/e2e-tls:ro" \
    --env-file "$WORK/$envfile" "$image" "$@" < "${JOB_INPUT:-/dev/null}"
}
serve() { # name image env-file port
  local platform=()
  [ "$2" = "$IDENTITY_IMAGE" ] && platform=(--platform linux/amd64)
  docker run -d --name "$1" --hostname "$1" --network "$NET" ${platform[@]+"${platform[@]}"} -p "127.0.0.1:$4:8443" \
    -v "$WORK/tls:/e2e-tls:ro" --env-file "$WORK/$3" "$2" >/dev/null
}

# ---- Ocharo's local Identity: the apps' clients, the Events and SCFD resources, the Events permissions ---------------
say "Identity: settings, migrate, start"
{
  tls_env
  cat <<EOF
DB_CONNECTION=$(CONN identity)
Identity__Server__Issuer=$URL_ID
Identity__SignUp__Mode=Open
Identity__Server__Signals__AllowPrivateNetworkTargets=true
Identity__Organizations__Roles__owner__0=events:read
Identity__Organizations__Roles__owner__1=events:track
Identity__Organizations__Roles__owner__2=events:review
Identity__Organizations__Roles__owner__3=events:admin
Identity__Organizations__Roles__admin__0=events:read
Identity__Organizations__Roles__admin__1=events:track
Identity__Organizations__Roles__admin__2=events:review
Identity__Organizations__Roles__admin__3=events:admin
Identity__Organizations__Roles__editor__0=events:read
Identity__Organizations__Roles__editor__1=events:track
Identity__Organizations__Roles__viewer__0=events:read
Identity__Server__Resources__0__Name=events
Identity__Server__Resources__0__Uri=$URL_EV
Identity__Server__Resources__0__PermissionNamespace=events
Identity__Server__Resources__0__Scopes__0=events.read
Identity__Server__Resources__0__Scopes__1=events.track
Identity__Server__Resources__0__Scopes__2=events.review
Identity__Server__Resources__0__Scopes__3=events.import
Identity__Server__Resources__0__Scopes__4=events.admin
Identity__Server__Resources__1__Name=harvest
Identity__Server__Resources__1__Uri=$URL_SCFD
Identity__Server__Resources__1__Scopes__0=harvest.tenants-manage
Identity__Server__Resources__1__Scopes__1=harvest.containers-manage
Identity__Server__Resources__1__Scopes__2=harvest.sources-manage
Identity__Server__Resources__1__Scopes__3=harvest.pipelines-read
Identity__Server__Resources__1__Scopes__4=harvest.pipelines-run
Identity__Server__Resources__1__Scopes__5=harvest.pipelines-manage
Identity__Server__Resources__1__Scopes__6=harvest.subscriptions-manage
Identity__Server__Resources__1__Scopes__7=harvest.events-read
Identity__Server__Resources__1__Scopes__8=harvest.links-submit
Identity__Server__Resources__1__Scopes__9=harvest.entries-write
Identity__Server__Resources__1__Scopes__10=harvest.posts-read
Identity__Server__Clients__0__ClientId=ocharo-events
Identity__Server__Clients__0__DisplayName=Ocharo Events
Identity__Server__Clients__0__PermissionNamespace=events
Identity__Server__Clients__0__ClientSecret=$SECRET_OE
Identity__Server__Clients__0__RedirectUris__0=${URL_OE}api/identity/callback
Identity__Server__Clients__0__PostSignOutRedirectUris__0=$URL_OE
Identity__Server__Clients__0__Grants__0=authorization_code
Identity__Server__Clients__0__Grants__1=token_exchange
Identity__Server__Clients__0__ExchangeResources__0=events
Identity__Server__Clients__0__Scopes__0=events.read
Identity__Server__Clients__0__Scopes__1=events.track
Identity__Server__Clients__0__Scopes__2=events.admin
Identity__Server__Clients__1__ClientId=events
Identity__Server__Clients__1__DisplayName=WoW2 Events
Identity__Server__Clients__1__ClientSecret=$SECRET_EVENTS
Identity__Server__Clients__1__Grants__0=client_credentials
Identity__Server__Clients__1__Resources__0=harvest
Identity__Server__Clients__1__Scopes__0=harvest.events-read
Identity__Server__Clients__2__ClientId=e2e-driver
Identity__Server__Clients__2__DisplayName=events-e2e-smoke driver
Identity__Server__Clients__2__ClientSecret=$SECRET_DRIVER
Identity__Server__Clients__2__Grants__0=client_credentials
Identity__Server__Clients__2__Resources__0=harvest
Identity__Server__Clients__2__Scopes__0=harvest.tenants-manage
Identity__Server__Clients__2__Scopes__1=harvest.containers-manage
Identity__Server__Clients__2__Scopes__2=harvest.sources-manage
EOF
} > "$WORK/identity.env"
chmod 600 "$WORK/identity.env"
job identity-migrate "$IDENTITY_IMAGE" identity.env migrate >"$WORK/identity-migrate.out" 2>&1 \
  || { tail -n 25 "$WORK/identity-migrate.out" >&2; die "Identity migrate failed"; }
serve "$ID" "$IDENTITY_IMAGE" identity.env "$PORT_ID"
drive wait identity

say "Identity: the test owner, instance admin and owner of Ocharo's organization $ORG_ID"
OWNER_ID="$(drive identity-owner)"
admin_out="$(job identity-admin "$IDENTITY_IMAGE" identity.env admin create --display-name "Smoke Owner" --email "$OWNER_EMAIL" --user-id "$OWNER_ID" 2>"$WORK/identity-admin.err")" \
  || { cat "$WORK/identity-admin.err" >&2; die "admin create failed: $admin_out"; }
echo "$admin_out" | tail -n 1
drive identity-org

# ---- SCFD: Ocharo's channels and selector subscription, then the host with service tokens ---------------------------
say "SCFD: migrate, then \`ocharo setup --selector\` on the four publishers the selector follows"
{
  tls_env
  cat <<EOF
DB_CONNECTION=$(CONN harvest)
Harvest__Operator__ApiKey=$OPERATOR_KEY
Harvest__Worker__Enabled=false
HarvestSources__Audience=internal
HarvestIntegrations__Enabled=false
EOF
} > "$WORK/scfd-job.env"
chmod 600 "$WORK/scfd-job.env"
job scfd-migrate "$HARVEST_IMAGE" scfd-job.env migrate >"$WORK/scfd-migrate.out" 2>&1 \
  || { tail -n 25 "$WORK/scfd-migrate.out" >&2; die "SCFD migrate failed"; }
job scfd-setup "$HARVEST_IMAGE" scfd-job.env ocharo setup --group afisha --group anons --group caex --group uzexpocentre --selector \
  >"$WORK/scfd-setup.out" 2>"$WORK/scfd-setup.err" \
  || { tail -n 30 "$WORK/scfd-setup.err" >&2; tail -n 3 "$WORK/scfd-setup.out" >&2; die "ocharo setup failed"; }
drive record-setup < "$WORK/scfd-setup.out"
TENANT_ID="$(drive get tenantId)"
SELECTOR_ID="$(drive get selectorSubscriptionId)"

say "SCFD: start the host; its API takes Identity's service tokens only (the Events client reads, the driver is the operator)"
{
  cat "$WORK/scfd-job.env"
  cat <<EOF
Harvest__ServiceAccess__Enabled=true
Harvest__ServiceAccess__Authority=$URL_ID
Harvest__ServiceAccess__Audience=$URL_SCFD
Harvest__ServiceAccess__ClientGrantsJson={"events":{"tenantId":"$TENANT_ID","scopes":["events:read"]},"e2e-driver":{"isOperator":true,"scopes":["tenants:manage","containers:manage","sources:manage"]}}
EOF
} > "$WORK/scfd.env"
chmod 600 "$WORK/scfd.env"
serve "$SCFD" "$HARVEST_IMAGE" scfd.env "$PORT_SCFD"
drive wait scfd

# ---- WoW2 Events: catalog, Ocharo's workflow, the one intake source, then the host ------------------------------------
say "Events: migrate, Ocharo's catalog and workflow, intake source ocharo-events -> selector:ocharo-events"
{
  tls_env
  cat <<EOF
DB_CONNECTION=$(CONN events)
Events__Auth__Audience=$URL_EV
Events__Auth__Issuers__0__Issuer=$URL_ID
Events__Auth__Issuers__0__Audiences__0=$URL_EV
Events__Digests__Enabled=false
Events__Intake__Sources__0__Name=ocharo-events
Events__Intake__Sources__0__Catalog=ocharo-fashion
Events__Intake__Sources__0__Instance=scfd
Events__Intake__Sources__0__BaseUrl=$URL_SCFD
Events__Intake__Sources__0__SubscriptionId=$SELECTOR_ID
Events__Intake__Sources__0__TokenEndpoint=${URL_ID}connect/token
Events__Intake__Sources__0__ClientId=events
Events__Intake__Sources__0__ClientSecret=$SECRET_EVENTS
Events__Intake__Sources__0__Scope=harvest.events-read
Events__Intake__Sources__0__Resource=$URL_SCFD
Events__Intake__Sources__0__PollInterval=00:05:00
Events__Intake__Sources__0__Review=auto
Events__Intake__Sources__0__Classifier=filter
Events__Intake__Sources__0__Filter=selector:ocharo-events
Events__Intake__Sources__0__DefaultCountry=UZ
Events__Intake__Sources__0__DefaultCities__0=Tashkent
Events__Intake__Sources__0__DefaultTimeZone=Asia/Tashkent
Events__Intake__Sources__0__OwnerOrg=$ORG_ID
Events__Intake__Sources__0__OwnerIssuer=$URL_ID
EOF
} > "$WORK/events.env"
chmod 600 "$WORK/events.env"
job events-migrate "$EVENTS_IMAGE" events.env migrate >"$WORK/events-migrate.out" 2>&1 \
  || { tail -n 25 "$WORK/events-migrate.out" >&2; die "Events migrate failed"; }
job events-catalog "$EVENTS_IMAGE" events.env catalog ensure ocharo-fashion --owner-org "$ORG_ID" --name "Ocharo fashion" \
  --kinds runway,trade-fair,market,exhibition,competition,festival,conference,other 2>&1 | tail -n 1
JOB_INPUT="$WORKFLOW" job events-workflow "$EVENTS_IMAGE" events.env workflow put --owner-org "$ORG_ID" --spec - 2>&1 | tail -n 1
serve "$EV" "$EVENTS_IMAGE" events.env "$PORT_EV"
drive wait events
echo "events: \`intake check\` (token, the selector subscription, tenant isolation):"
job events-check "$EVENTS_IMAGE" events.env intake check 2>&1 | tail -n 3 | cut -c1-300

# ---- Ocharo's Events app --------------------------------------------------------------------------------------------
say "Ocharo Events: migrate, start (sessions through Identity client ocharo-events, data from WoW2 Events)"
{
  tls_env
  cat <<EOF
ConnectionStrings__Database=$(CONN ocharo_events)
Identity__Client__Authority=$URL_ID
Identity__Client__ClientId=ocharo-events
Identity__Client__ClientSecret=$SECRET_OE
Identity__Client__CookieName=__Host-ocharo-events
Events__BaseUrl=$URL_EV
Events__Catalog=ocharo-fashion
EOF
} > "$WORK/oe.env"
chmod 600 "$WORK/oe.env"
job oe-migrate "$OE_IMAGE" oe.env migrate >"$WORK/oe-migrate.out" 2>&1 \
  || { tail -n 25 "$WORK/oe-migrate.out" >&2; die "Ocharo Events migrate failed"; }
serve "$OE" "$OE_IMAGE" oe.env "$PORT_OE"
drive wait ocharo-events

# ---- the record ---------------------------------------------------------------------------------------------------
say "SCFD: ingest one tagged fixture capture through the operator capture route"
drive scfd-ingest
say "Events: poll the intake once"
drive events-poll
say "Ocharo Events: read the feed with a signed-in test session"
drive ocharo-read
say "events-e2e passed: SCFD -> Events -> Ocharo Events carried one event"
