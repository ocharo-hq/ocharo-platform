#!/usr/bin/env bash
set -euo pipefail

origin="${1:-http://127.0.0.1:8268}"
origin="${origin%/}"
check_tmp="$(mktemp -d "${TMPDIR:-/tmp}/brand-workspace-check.XXXXXX")"
trap 'rm -rf "$check_tmp"' EXIT

curl --fail --silent --show-error --max-time 10 "$origin/health/live" >"$check_tmp/live.json"
curl --fail --silent --show-error --max-time 10 "$origin/health/ready" >"$check_tmp/ready.json"
curl --fail --silent --show-error --max-time 10 \
  "$origin/api/system/status" >"$check_tmp/status.json"

for route in / /scaffold-deep-link; do
  curl --fail --silent --show-error --max-time 10 \
    "$origin$route" >"$check_tmp/spa.html"
  grep -q 'id="app"' "$check_tmp/spa.html"
done

index=0
for route in /api /api/scaffold-missing /api/unknown.json; do
  for method in GET POST DELETE; do
    missing_status="$(curl --silent --show-error --max-time 10 --request "$method" \
      --output "$check_tmp/missing-$index.json" --write-out '%{http_code}' \
      "$origin$route")"
    if test "$missing_status" != 404; then
      printf 'Expected HTTP 404 for %s %s; received %s.\n' "$method" "$route" "$missing_status" >&2
      exit 1
    fi
    index=$((index + 1))
  done
done

python3 - "$check_tmp" <<'PY'
import datetime
import json
import pathlib
import sys

directory = pathlib.Path(sys.argv[1])
def read(name):
    return json.loads((directory / name).read_text())

if read("live.json") != {"status": "alive"}:
    raise SystemExit("Liveness must report status=alive.")
if read("ready.json") != {"status": "ready"}:
    raise SystemExit("Readiness must report status=ready.")

status = read("status.json")
if not isinstance(status, dict) or set(status) != {"service", "stage", "persistence", "worker"}:
    raise SystemExit("System status does not match the foundation contract.")
if (status["service"], status["stage"], status["persistence"]) != (
    "brand-workspace", "foundation", "PostgreSQL"
):
    raise SystemExit("System status must identify brand-workspace/foundation/PostgreSQL.")

worker = status["worker"]
if not isinstance(worker, dict) or set(worker) != {"state", "lastSeenAtUtc"}:
    raise SystemExit("Worker status does not match the heartbeat contract.")
if worker["state"] != "healthy" or not isinstance(worker["lastSeenAtUtc"], str):
    raise SystemExit("The full-stack check requires a healthy worker heartbeat with its timestamp.")
observed = datetime.datetime.fromisoformat(worker["lastSeenAtUtc"].replace("Z", "+00:00"))
if observed.tzinfo is None or observed.utcoffset() != datetime.timedelta(0):
    raise SystemExit("Worker heartbeat timestamp must specify UTC.")

for path in directory.glob("missing-*.json"):
    missing = json.loads(path.read_text())
    if not isinstance(missing, dict) or missing.get("status") != 404 or missing.get("title") != "Not Found":
        raise SystemExit(f"{path.name}: unknown API routes must return a JSON Not Found problem.")
PY

printf '%s\n' "Passed: health contracts, system/worker heartbeat, SPA root/deep link, API JSON 404 across methods."
