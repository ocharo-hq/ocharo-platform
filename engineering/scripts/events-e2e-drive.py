#!/usr/bin/env python3
"""HTTP steps of events-e2e-smoke.sh: Ocharo's local Identity, SCFD, WoW2 Events and the Ocharo Events app.

Python 3.9+ standard library only. The shell script owns the containers and passes one JSON config file; every step
here talks HTTPS to a container by its name (`https://ocharo-e2e-identity:8443/`), which this client routes to the
loopback port that container publishes, checking the certificate against the run's own CA. A name is the origin an
app registers with Identity and the issuer a token carries, so none of them differs from a deployed one.

    events-e2e-drive.py CONFIG wait NAME             wait until https://NAME:8443/health answers 200
    events-e2e-drive.py CONFIG get KEY               print a value of the config's `state`
    events-e2e-drive.py CONFIG record-setup          read `ocharo setup`'s result line from stdin into the state
    events-e2e-drive.py CONFIG identity-owner        sign the test owner up at Identity; print the person's id
    events-e2e-drive.py CONFIG identity-org          sign in, create Ocharo's organization with the preset id
    events-e2e-drive.py CONFIG scfd-ingest           put the fixture capture in SCFD; read it back from the selector
    events-e2e-drive.py CONFIG events-poll           poll Events' intake once with a service token
    events-e2e-drive.py CONFIG ocharo-read           sign in to Ocharo Events, read the feed, assert the event
"""
from __future__ import annotations

import base64
import datetime
import hashlib
import http.client
import json
import os
import socket
import ssl
import sys
import time
import urllib.parse
import uuid
from pathlib import Path


class Failure(Exception):
    """A step that did not hold; the script prints it and fails."""


class Client:
    """A browser-like HTTPS client that routes container names to loopback ports and keeps cookies by origin."""

    def __init__(self, config: dict) -> None:
        self.routes = {f"{name}:8443": port for name, port in config["ports"].items()}
        self.context = ssl.create_default_context(cafile=config["ca"])
        self.cookies: dict[str, dict[str, str]] = {}

    def request(self, method: str, url: str, *, json_body=None, form=None, headers=None, follow=0):
        """Send one request; returns (status, response headers, body bytes). `follow` redirects are walked by hand."""
        for _ in range(follow + 1):
            parts = urllib.parse.urlsplit(url)
            origin = f"{parts.hostname}:{parts.port or 443}"
            if origin not in self.routes:
                raise Failure(f"{url} names no container of this run ({origin})")
            sent = {"Host": origin, "Accept": "application/json", **(headers or {})}
            body = None
            if json_body is not None:
                body = json.dumps(json_body).encode()
                sent["Content-Type"] = "application/json"
            elif form is not None:
                body = urllib.parse.urlencode(form).encode()
                sent["Content-Type"] = "application/x-www-form-urlencoded"
            jar = self.cookies.setdefault(origin, {})
            if jar:
                sent["Cookie"] = "; ".join(f"{key}={value}" for key, value in jar.items())
            connection = _Connection(parts.hostname, self.routes[origin], self.context)
            try:
                path = parts.path + (f"?{parts.query}" if parts.query else "")
                connection.request(method, path or "/", body=body, headers=sent)
                response = connection.getresponse()
                data = response.read()
            finally:
                connection.close()
            for line in response.headers.get_all("Set-Cookie") or []:
                pair = line.split(";", 1)[0]
                key, _, value = pair.partition("=")
                if "max-age=0" in line.lower() or value == "":
                    jar.pop(key.strip(), None)
                else:
                    jar[key.strip()] = value.strip()
            location = response.headers.get("Location")
            if response.status in (301, 302, 303, 307, 308) and location and follow:
                url = urllib.parse.urljoin(url, location)
                method, json_body, form = "GET", None, None
                follow -= 1
                continue
            return response.status, response.headers, data
        raise Failure(f"too many redirects from {url}")


class _Connection(http.client.HTTPSConnection):
    def __init__(self, name: str, port: int, context: ssl.SSLContext) -> None:
        super().__init__("127.0.0.1", port, context=context, timeout=60)
        self._name = name

    def connect(self) -> None:
        sock = socket.create_connection((self.host, self.port), self.timeout)
        self.sock = self._context.wrap_socket(sock, server_hostname=self._name)


def load(path: str) -> dict:
    return json.loads(Path(path).read_text())


def save(path: str, config: dict) -> None:
    Path(path).write_text(json.dumps(config, indent=2))


def url(config: dict, name: str, path: str = "") -> str:
    return f"https://{config['prefix']}-{name}:8443/{path.lstrip('/')}"


def parse(body: bytes):
    try:
        return json.loads(body) if body else None
    except json.JSONDecodeError:
        return body.decode(errors="replace")[:400]


def data_of(status: int, body: bytes, what: str, ok=(200, 201)):
    parsed = parse(body)
    if status not in ok:
        raise Failure(f"{what}: HTTP {status} {json.dumps(parsed)[:600]}")
    return parsed["data"] if isinstance(parsed, dict) and "data" in parsed else parsed


def token(client: Client, config: dict, client_id: str, secret: str, resource: str, scopes: list[str]) -> str:
    """A client-credentials token from Ocharo's Identity for exactly one resource."""
    status, _, body = client.request(
        "POST", url(config, "identity", "connect/token"),
        form={"grant_type": "client_credentials", "client_id": client_id, "client_secret": secret,
              "resource": resource, "scope": " ".join(scopes)})
    parsed = parse(body)
    if status != 200 or not isinstance(parsed, dict) or "access_token" not in parsed:
        raise Failure(f"token for {client_id} -> {resource}: HTTP {status} {json.dumps(parsed)[:400]}")
    return parsed["access_token"]


def bearer(value: str) -> dict:
    return {"Authorization": f"Bearer {value}"}


# ---- commands ------------------------------------------------------------------------------------------------------

def cmd_wait(config: dict, name: str) -> None:
    client = Client(config)
    deadline = time.time() + float(config.get("waitSeconds", 240))
    last = "no answer"
    while time.time() < deadline:
        try:
            status, _, body = client.request("GET", url(config, name, "health"))
            if status == 200:
                print(f"{name}: healthy")
                return
            last = f"HTTP {status} {body[:120]!r}"
        except (OSError, ssl.SSLError, http.client.HTTPException) as error:
            last = f"{type(error).__name__}: {error}"
        time.sleep(2)
    raise Failure(f"{name} was not healthy in time ({last})")


def cmd_get(config: dict, key: str) -> None:
    value = config["state"].get(key)
    if value is None:
        raise Failure(f"state has no {key}")
    print(value)


def cmd_record_setup(path: str, config: dict) -> None:
    """Take the last JSON line of `ocharo setup` from stdin: its tenant, container and selector subscription."""
    lines = [line for line in sys.stdin.read().splitlines() if line.strip().startswith("{")]
    if not lines:
        raise Failure("ocharo setup printed no result line")
    result = json.loads(lines[-1])
    for key in ("tenantId", "containerId", "selectorSubscriptionId", "selectorVersion"):
        if result.get(key) in (None, ""):
            raise Failure(f"ocharo setup's result has no {key}: {lines[-1][:400]}")
        config["state"][key] = result[key]
    save(path, config)
    print(f"tenant {result['tenantId']}  selector subscription {result['selectorSubscriptionId']} v{result['selectorVersion']}")


def cmd_identity_owner(path: str, config: dict) -> None:
    """Sign the test owner up (sign-up is open on this local instance); `admin create --user-id` then names them."""
    client = Client(config)
    owner = config["owner"]
    status, _, body = client.request(
        "POST", url(config, "identity", "api/account/sign-up"),
        json_body={"email": owner["email"], "password": owner["password"]})
    person = data_of(status, body, "sign-up")
    config["state"]["ownerId"] = person["userId"]
    save(path, config)
    print(person["userId"])


def sign_in_identity(client: Client, config: dict) -> None:
    owner = config["owner"]
    status, _, body = client.request(
        "POST", url(config, "identity", "api/account/sign-in?useCookies=true"),
        json_body={"identifier": owner["email"], "password": owner["password"]})
    if status != 204:
        raise Failure(f"owner sign-in at Identity: HTTP {status} {json.dumps(parse(body))[:400]}")


def cmd_identity_org(path: str, config: dict) -> None:
    """Create Ocharo's organization with the preset id as the instance admin, who becomes its owner."""
    client = Client(config)
    sign_in_identity(client, config)
    status, _, body = client.request(
        "POST", url(config, "identity", "api/console/organizations"),
        json_body={"name": "Ocharo", "slug": "ocharo", "id": config["orgId"]})
    organization = data_of(status, body, "create organization")
    print(f"organization {organization.get('id', config['orgId'])} created; the owner holds it")


def operator_token(client: Client, config: dict) -> str:
    drive = config["clients"]["driver"]
    return token(client, config, drive["id"], drive["secret"], url(config, "scfd"),
                 ["harvest.tenants-manage", "harvest.containers-manage", "harvest.sources-manage"])


def events_client_token(client: Client, config: dict) -> str:
    events = config["clients"]["events"]
    return token(client, config, events["id"], events["secret"], url(config, "scfd"), ["harvest.events-read"])


def fixture(config: dict) -> dict:
    """The fixture with its dates set: a start 45 days ahead at 19:00 Tashkent time, and the next day as its end."""
    text = Path(config["fixture"]).read_text()
    start = (datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=5))) + datetime.timedelta(days=45)).date()
    text = text.replace("@START@", f"{start.isoformat()}T19:00:00+05:00").replace("@END@", (start + datetime.timedelta(days=1)).isoformat())
    return json.loads(text)


def cmd_scfd_ingest(path: str, config: dict) -> None:
    client = Client(config)
    spec = fixture(config)
    operator = bearer(operator_token(client, config))
    tenant, container = config["state"]["tenantId"], config["state"]["containerId"]
    subscription = config["state"]["selectorSubscriptionId"]

    # The subscription is the selector `ocharo setup --selector` made: the check the Events source makes first.
    status, _, body = client.request("GET", url(config, "scfd", f"api/subscriptions/{subscription}"), headers=bearer(events_client_token(client, config)))
    shown = data_of(status, body, "read the selector subscription")
    if shown.get("kind") != "selector" or shown.get("name") != "ocharo-events" or shown.get("state") != "active":
        raise Failure(f"subscription {subscription} is not Ocharo's active selector: {json.dumps(shown)[:400]}")
    print(f"scfd: subscription {shown['name']} kind={shown['kind']} v{shown['selectorVersion']} state={shown['state']}")

    status, _, body = client.request("GET", url(config, "scfd", f"api/sources?containerId={container}"), headers=operator)
    sources = data_of(status, body, "list the container's sources")
    wanted = spec["source"]
    source = next((item for item in sources if item.get("handle") == wanted["handle"] and str(item.get("family", "")).lower() == wanted["family"]), None)
    if source is None:
        handles = ", ".join(f"{item.get('family')}/{item.get('handle')}" for item in sources)
        raise Failure(f"no source {wanted['family']}/{wanted['handle']} in the Ocharo container (has: {handles})")

    capture = dict(spec["capture"])
    capture["capturedAtUtc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    capture["body"] = base64.b64encode(spec["capture"]["contentProjection"].encode()).decode()
    status, _, body = client.request(
        "POST", url(config, "scfd", "api/posts/captures"), headers=operator,
        json_body={"containerId": container, "sourceId": source["id"], "capture": capture, "envelope": spec["envelope"]})
    saved = data_of(status, body, "operator capture")
    post = saved["post"]["id"]
    print(f"scfd: capture stored as post {post} in source {source['handle']}")
    config["state"]["postId"] = post
    save(path, config)

    # What the Events source pulls: the selector's verdict on the tagged post.
    deadline = time.time() + 60
    events: list = []
    while time.time() < deadline:
        status, _, body = client.request("GET", url(config, "scfd", f"api/subscriptions/{subscription}/events"), headers=bearer(events_client_token(client, config)))
        page = data_of(status, body, "pull the selector subscription")
        events = [item for item in page["data"] if item.get("postId") == post]
        if events:
            break
        time.sleep(1)
    if not events:
        raise Failure("the selector delivered nothing for the fixture post: it is probably pending or refused "
                      f"(the tags {[tag['tag'] for tag in spec['envelope']['tags']]} must reach the selector's clauses)")
    first = events[0]
    verdict = (first.get("match") or {}).get("verdict")
    if first.get("type") != "post.new" or verdict != "match":
        raise Failure(f"expected post.new with a matching verdict, got {first.get('type')} / {verdict}: {json.dumps(first.get('match'))[:300]}")
    print(f"scfd: selector delivered {first['type']} verdict={verdict} clauses={(first['match'] or {}).get('clauses')}")


def person_token(client: Client, config: dict, resource: str, scopes: list[str]) -> str:
    """The signed-in owner's token for one resource, the way the Ocharo Events app gets it for a person: the code flow
    with PKCE as client `ocharo-events`, then a token exchange (RFC 8693) for the resource."""
    sign_in_identity(client, config)
    app = config["clients"]["ocharo-events"]
    verifier = base64.urlsafe_b64encode(os.urandom(32)).rstrip(b"=").decode()
    challenge = base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).rstrip(b"=").decode()
    redirect = url(config, "ocharo-events", "api/identity/callback")
    query = urllib.parse.urlencode({
        "client_id": app["id"], "redirect_uri": redirect, "response_type": "code", "scope": "openid profile email offline_access",
        "state": "e2e", "nonce": "e2e", "code_challenge": challenge, "code_challenge_method": "S256"})
    status, headers, body = client.request("GET", url(config, "identity", "connect/authorize") + "?" + query)
    code = urllib.parse.parse_qs(urllib.parse.urlsplit(headers.get("Location", "")).query).get("code", [None])[0]
    if status != 302 or not code:
        raise Failure(f"authorize as {app['id']}: HTTP {status} {headers.get('Location', '')[:200]} {json.dumps(parse(body))[:300]}")
    status, _, body = client.request("POST", url(config, "identity", "connect/token"), form={
        "grant_type": "authorization_code", "code": code, "redirect_uri": redirect, "code_verifier": verifier,
        "client_id": app["id"], "client_secret": app["secret"]})
    tokens = parse(body)
    if status != 200 or "access_token" not in tokens:
        raise Failure(f"code redemption: HTTP {status} {json.dumps(tokens)[:300]}")
    status, _, body = client.request("POST", url(config, "identity", "connect/token"), form={
        "grant_type": "urn:ietf:params:oauth:grant-type:token-exchange", "subject_token": tokens["access_token"],
        "subject_token_type": "urn:ietf:params:oauth:token-type:access_token", "resource": resource, "scope": " ".join(scopes),
        "client_id": app["id"], "client_secret": app["secret"]})
    exchanged = parse(body)
    if status != 200 or "access_token" not in exchanged:
        raise Failure(f"token exchange for {resource}: HTTP {status} {json.dumps(exchanged)[:300]}")
    return exchanged["access_token"]


def cmd_events_poll(path: str, config: dict) -> None:
    """Poll WoW2 Events' intake once, as the Ocharo owner: `POST api/events/v1/intake/poll` covers only the sources whose
    catalogs the caller's organization owns, so a service token (no organization) polls none."""
    client = Client(config)
    access = person_token(client, config, url(config, "events"), ["events.admin"])
    status, _, body = client.request("POST", url(config, "events", "api/events/v1/intake/poll"), headers=bearer(access))
    report = data_of(status, body, "intake poll")
    sources = report.get("sources", [])
    if not sources:
        raise Failure(f"intake poll reported no source for Ocharo's organization: {json.dumps(report)[:400]}")
    for source in sources:
        print(f"events: intake poll source={source['name']} catalog={source['catalog']} received={source['received']} "
              f"quarantined={source['quarantined']} failures={source['consecutiveFailures']} lastError={source.get('lastError')}")
    failing = [source for source in sources if source.get("lastError") or source["consecutiveFailures"]]
    if failing:
        raise Failure(f"intake source failed: {json.dumps(failing)[:500]}")
    if sum(source["received"] for source in sources) < 1:
        raise Failure(f"the poll stored no record: {json.dumps(report)[:400]}")
    print(f"events: {report.get('candidates')} candidate(s) waiting, {report.get('pendingChanges')} pending change(s)")


def sign_in_ocharo(client: Client, config: dict) -> dict:
    """Walk the code flow a browser walks: the app's sign-in route, Identity's authorize endpoint, the app's callback."""
    sign_in_identity(client, config)
    app = url(config, "ocharo-events")
    status, headers, body = client.request("GET", app + "api/identity/sign-in?returnUrl=%2F")
    if status != 302:
        raise Failure(f"the app's sign-in route answered HTTP {status}, not a redirect: {json.dumps(parse(body))[:300]}")
    authorize = headers["Location"]
    if "/connect/authorize" not in authorize:
        raise Failure(f"the sign-in redirect does not go to Identity's authorize endpoint: {authorize[:200]}")
    status, headers, body = client.request("GET", authorize)
    callback = headers.get("Location", "")
    if status != 302 or "/api/identity/callback" not in callback:
        raise Failure(f"Identity did not send the browser back with a code (HTTP {status}): {callback[:200]} {json.dumps(parse(body))[:300]}")
    status, headers, body = client.request("GET", callback)
    if status != 302:
        raise Failure(f"the app's callback answered HTTP {status}: {json.dumps(parse(body))[:400]}")
    status, _, body = client.request("GET", app + "api/identity/session")
    session = data_of(status, body, "the app's session route")
    return session


def cmd_ocharo_read(path: str, config: dict) -> None:
    client = Client(config)
    spec = fixture(config)
    session = sign_in_ocharo(client, config)
    org, catalog = config["orgId"], config["catalog"]
    print(f"ocharo-events: signed in; session {sorted(session.keys())}")
    base = url(config, "ocharo-events", f"api/organizations/{org}/events-api/api/events/v1/")

    # The feed of the organization's tracked catalog: the event that SCFD delivered and Events published.
    found = None
    deadline = time.time() + 30
    while time.time() < deadline and found is None:
        status, _, body = client.request("GET", base + f"catalogs/{catalog}/tracking?limit=50")
        page = data_of(status, body, "the Events page's feed")
        items = page["items"]
        found = next((item for item in items if spec["envelope"]["title"] in json.dumps(item["event"]["facts"]["title"])), None)
        if found is None:
            time.sleep(1)
    if found is None:
        raise Failure(f"the feed does not hold {spec['envelope']['title']!r}; it held {json.dumps(page)[:600]}")

    status, _, body = client.request("GET", base + "workflow")
    workflow = data_of(status, body, "the workflow")
    facts, tracking = found["event"]["facts"], found.get("tracking")
    title = next(iter(facts["title"].values()))
    place = facts.get("place") or {}
    city = place.get("city")
    starts = (facts.get("starts") or {}).get("value", "")
    sources = found["event"].get("sources", [])
    link = next((source.get("url") for source in sources if source.get("url")), None) or facts.get("website")
    stage = (tracking or {}).get("stage") or workflow.get("defaultStage")
    review = found["event"].get("review")

    expected_start = next(date["value"] for date in spec["envelope"]["dates"] if date["role"] == "start")
    checks = [
        ("title", title, lambda got: got == spec["envelope"]["title"], spec["envelope"]["title"]),
        ("start date", starts[:10], lambda got: got == expected_start[:10], expected_start[:10]),
        ("city", city, lambda got: (got or "").lower() == "tashkent", "Tashkent"),
        ("source link", link, lambda got: got == spec["capture"]["url"], spec["capture"]["url"]),
        ("status", stage, lambda got: got == "candidate" and tracking is None, "candidate (untracked, the workflow's default stage)"),
        ("review", review, lambda got: got == "published", "published (Events' automatic review of the selector's delivery)"),
    ]
    failed = [f"{name}: got {got!r}, wanted {want}" for name, got, holds, want in checks if not holds(got)]
    print("")
    print("  ocharo events feed, event delivered through SCFD -> Events -> Ocharo")
    print(f"  title        {title}")
    print(f"  starts       {starts}  (ends {(facts.get('ends') or {}).get('value')})")
    print(f"  city         {city}  (country {place.get('country')}, venue {place.get('venue')})")
    print(f"  source link  {link}")
    print(f"  status       {stage}  (stage of {'tracking' if tracking else 'the workflow default, untracked'}; review {review})")
    print("")
    if failed:
        raise Failure("assertions failed: " + "; ".join(failed))
    print("events-e2e: title, start date, city, source link and status match the fixture")


COMMANDS = {
    "identity-owner": cmd_identity_owner,
    "identity-org": cmd_identity_org,
    "record-setup": cmd_record_setup,
    "scfd-ingest": cmd_scfd_ingest,
    "events-poll": cmd_events_poll,
    "ocharo-read": cmd_ocharo_read,
}


def main(argv: list[str]) -> int:
    if len(argv) < 3:
        print(__doc__)
        return 2
    path, command, rest = argv[1], argv[2], argv[3:]
    config = load(path)
    try:
        if command == "wait":
            cmd_wait(config, rest[0])
        elif command == "get":
            cmd_get(config, rest[0])
        elif command in COMMANDS:
            COMMANDS[command](path, config)
        else:
            print(f"unknown command {command}\n{__doc__}")
            return 2
    except Failure as failure:
        print(f"events-e2e: {command} failed: {failure}", file=sys.stderr)
        return 1
    except (OSError, ssl.SSLError, http.client.HTTPException) as error:
        print(f"events-e2e: {command} failed: {type(error).__name__}: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
