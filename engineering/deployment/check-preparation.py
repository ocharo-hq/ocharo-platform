#!/usr/bin/env python3
"""Exercise the preparation contract against an explicitly local HTTPS deployment.

Creates synthetic organizations and content using a previously provisioned example.test operator.
Keeps those records for inspection. Password comes from the terminal or standard input, never argv.
"""

import argparse
import getpass
import hashlib
import http.cookiejar
import io
import json
import ssl
import struct
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid
import zipfile
import zlib


class Client:
    def __init__(self, origin, ca_file):
        self.origin = origin.rstrip("/")
        self.csrf = None
        self.set_cookie_headers = []
        self.cookies = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(self.cookies),
            urllib.request.HTTPSHandler(context=ssl.create_default_context(cafile=ca_file)),
        )

    def request(self, path, method="GET", body=None, expected=(200,), raw=False, content_type=None):
        if not path.startswith("/") or path.startswith("//"):
            raise AssertionError("The API returned a nonlocal download URL.")
        headers = {"Accept": "application/json"}
        if method != "GET" and self.csrf:
            headers["X-CSRF-TOKEN"] = self.csrf
        if body is not None:
            if isinstance(body, bytes):
                headers["Content-Type"] = content_type
            else:
                body = json.dumps(body).encode()
                headers["Content-Type"] = "application/json"
        request = urllib.request.Request(self.origin + path, data=body, headers=headers, method=method)
        try:
            response = self.opener.open(request, timeout=30)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            self.set_cookie_headers = response.headers.get_all("Set-Cookie", [])
            payload = response.read()
            if response.status not in expected:
                raise AssertionError(f"{method} {path}: expected {expected}, received {response.status}")
            if "no-store" not in response.headers.get("Cache-Control", ""):
                raise AssertionError(f"{method} {path}: private response lacks no-store")
            if raw or not payload:
                return payload
            return json.loads(payload)

    def session(self):
        session = self.request("/api/auth/session")["data"]
        self.csrf = session["csrfToken"]
        return session


def png_bytes():
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))

    return (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">IIBBBBB", 1, 1, 8, 2, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(b"\x00\x70\x35\x66"))
        + chunk(b"IEND", b"")
    )


def upload(client, organization_path):
    boundary = "BrandWorkspaceCheck" + uuid.uuid4().hex
    image = png_bytes()
    data = (
        f'--{boundary}\r\nContent-Disposition: form-data; name="provenance"\r\n\r\n'
        f'Synthetic local verification image; no product claim.\r\n--{boundary}\r\n'
        'Content-Disposition: form-data; name="file"; filename="verification.png"\r\n'
        'Content-Type: image/png\r\n\r\n'
    ).encode() + image + f"\r\n--{boundary}--\r\n".encode()
    asset = client.request(
        organization_path + "/media", "POST", data, (200, 201),
        content_type=f"multipart/form-data; boundary={boundary}",
    )["data"]
    assert asset["sha256"].lower() == hashlib.sha256(image).hexdigest()
    assert asset["byteLength"] == len(image)
    actual = client.request(organization_path + f'/media/{asset["id"]}/content', raw=True)
    assert actual == image
    return asset, image


def run(args, password):
    client = Client(args.origin, args.ca_file)
    client.request("/api/organizations", expected=(401,))
    anonymous = client.session()
    assert anonymous["authenticated"] is False
    assert anonymous["setupRequired"] is False, "Provision the verification operator first."
    client.csrf = None
    client.request("/api/auth/login", "POST", {}, (400, 403))
    client.csrf = anonymous["csrfToken"]
    client.request("/api/auth/login", "POST", {"email": args.email, "password": password})
    session_headers = [header.lower() for header in client.set_cookie_headers
                       if header.startswith("__Host-brand-workspace-session=")]
    assert len(session_headers) == 1
    assert all(flag in session_headers[0] for flag in ("; secure", "; httponly", "; samesite=", "; path=/"))
    session = client.session()
    assert session["authenticated"] and session["sessionId"]
    assert session["user"]["email"].lower() == args.email.lower()
    assert any(cookie.name == "__Host-brand-workspace-session" and cookie.secure for cookie in client.cookies)

    suffix = uuid.uuid4().hex[:12]
    organizations = [
        client.request("/api/organizations", "POST", {
            "name": f"Verification {label} {suffix}", "slug": f"verification-{label}-{suffix}",
        }, (200, 201))["data"]
        for label in ("a", "b")
    ]
    first, second = [f'/api/organizations/{organization["id"]}' for organization in organizations]
    brand = client.request(first + "/brands", "POST", {
        "name": "Synthetic verification brand", "timeZone": "Asia/Tashkent",
    }, (200, 201))["data"]
    asset, image = upload(client, first)
    client.request(second + f'/media/{asset["id"]}/content', expected=(404,))
    draft_input = {
        "brandId": brand["id"], "title": "Synthetic verification draft",
        "caption": "Original verification caption.", "language": "en", "destination": "manual",
        "sourceNotes": "Local automated verification. No publication requested.", "mediaIds": [asset["id"]],
    }
    draft = client.request(first + "/drafts", "POST", draft_input, (200, 201))["data"]
    draft_path = first + f'/drafts/{draft["id"]}'
    client.request(second + f'/drafts/{draft["id"]}', expected=(404,))
    draft_input["caption"] = "Approved verification caption."
    updated = client.request(draft_path, "PUT", {**draft_input, "expectedRevision": draft["revision"]})["data"]
    client.request(draft_path, "PUT", {**draft_input, "expectedRevision": draft["revision"]}, (409,))
    revision = {"expectedRevision": updated["revision"]}
    requested = client.request(draft_path + "/request-review", "POST", revision)["data"]
    assert requested["status"] == "in-review"
    approved = client.request(draft_path + "/approve", "POST", revision)["data"]
    assert approved["status"] == "approved"
    export_request = {**revision, "idempotencyKey": str(uuid.uuid4())}
    package = client.request(draft_path + "/exports", "POST", export_request, (200, 201))["data"]
    repeated = client.request(draft_path + "/exports", "POST", export_request, (200, 201))["data"]
    assert repeated["id"] == package["id"]
    assert package["publicationStatus"] == "unverified"
    archive_bytes = client.request(package["downloadUrl"], raw=True)
    with zipfile.ZipFile(io.BytesIO(archive_bytes)) as archive:
        names = archive.namelist()
        assert all(not name.startswith("/") and ".." not in name.split("/") for name in names)
        assert archive.read("caption.txt").decode("utf-8-sig") == draft_input["caption"]
        manifest_names = [name for name in names if name.endswith("manifest.json")]
        assert len(manifest_names) == 1
        json.loads(archive.read(manifest_names[0]))
        image_names = [name for name in names if name.endswith(".png")]
        assert len(image_names) == 1 and archive.read(image_names[0]) == image
    client.request(second + f'/exports/{package["id"]}/download', expected=(404,))
    client.request(draft_path + "/revoke-approval", "POST", {**revision, "reason": "Verification revocation"})
    client.request(draft_path + "/exports", "POST", {**revision, "idempotencyKey": str(uuid.uuid4())}, (409,))
    assert client.request(package["downloadUrl"], raw=True) == archive_bytes
    client.request("/api/auth/logout", "POST", {}, (204,))
    client.request("/api/organizations", expected=(401,))
    assert client.session()["authenticated"] is False
    print(json.dumps({
        "result": "passed", "organizationIds": [item["id"] for item in organizations],
        "draftId": draft["id"], "exportId": package["id"],
        "checks": ["HTTPS session", "CSRF", "organization isolation", "immutable image bytes",
                   "stale save", "review and approval", "idempotent ZIP", "revocation", "logout"],
    }, indent=2))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--origin", default="https://localhost:8264")
    parser.add_argument("--email", default="smoke@example.test")
    parser.add_argument("--ca-file", help="Explicit local test CA; certificate verification remains enabled.")
    args = parser.parse_args()
    origin = urllib.parse.urlsplit(args.origin)
    if origin.scheme != "https" or origin.hostname not in ("localhost", "127.0.0.1", "::1"):
        parser.error("The verification script targets local HTTPS only.")
    if origin.username or origin.password or origin.path not in ("", "/") or origin.query or origin.fragment:
        parser.error("Use a plain HTTPS origin without credentials, path, query, or fragment.")
    if not args.email.lower().endswith("@example.test"):
        parser.error("Use an explicitly provisioned example.test verification operator.")
    password = getpass.getpass("Verification operator password: ") if sys.stdin.isatty() else sys.stdin.readline().rstrip("\r\n")
    if not password:
        parser.error("A verification operator password is required on stdin.")
    run(args, password)


if __name__ == "__main__":
    main()
