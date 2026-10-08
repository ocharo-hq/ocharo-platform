"""Tests for build_shawl_import_bundle.py on a small synthetic fixture.

Every name, number, address and URL below is invented (example.test, +9100000000xx).
Run from this folder: python3 -I -m unittest
"""
from __future__ import annotations

import contextlib
import hashlib
import importlib.util
import io
import json
import os
import struct
import subprocess
import sys
import tempfile
import unittest
import zlib
from pathlib import Path
from unittest import mock

HERE = Path(__file__).resolve().parent
SCRIPT = HERE / "build_shawl_import_bundle.py"
_spec = importlib.util.spec_from_file_location("build_shawl_import_bundle", SCRIPT)
bib = importlib.util.module_from_spec(_spec)
sys.modules[_spec.name] = bib
_spec.loader.exec_module(bib)

LEDGER = "och-fixture-outreach.json"
CEILING = 7.77


# --------------------------------------------------------------------------- byte factories

def png_bytes(rgb: tuple[int, int, int], width: int = 1, height: int = 1) -> bytes:
    def chunk(kind: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)
    row = b"\x00" + bytes(rgb) * width
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(row * height)) + chunk(b"IEND", b""))


def jpeg_bytes(salt: str, width: int = 4, height: int = 3) -> bytes:
    comment = salt.encode()
    return (b"\xff\xd8" + b"\xff\xfe" + struct.pack(">H", len(comment) + 2) + comment
            + b"\xff\xc0" + struct.pack(">HBHHB", 11, 8, height, width, 1) + b"\x01\x11\x00" + b"\xff\xd9")


def mp4_bytes() -> bytes:
    return b"\x00\x00\x00\x18ftypisom\x00\x00\x02\x00isomiso2" + b"\x00" * 32


def pdf_bytes() -> bytes:
    return b"%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF\n"


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


# --------------------------------------------------------------------------- fixture

class Fixture:
    """tmp/mft/context/domains/x/research is the research folder; tmp/mft plays the repo root."""

    def __init__(self, root: Path) -> None:
        self.root = root
        self.repo = root / "mft"
        self.research = self.repo / "context" / "domains" / "x" / "research"
        self.outreach = self.research / "assets" / Path(LEDGER).stem
        self.responses = self.outreach / "responses"
        self.viewer = root / "viewer"
        self.bytes: dict[str, bytes] = {}
        self.build()

    def write(self, rel: str, data: bytes) -> Path:
        path = self.responses / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        self.bytes[rel] = data
        return path

    def write_json(self, rel: str, data) -> Path:
        return self.write(rel, json.dumps(data, indent=1).encode())

    def build(self) -> None:
        self.responses.mkdir(parents=True)
        reference = jpeg_bytes("our-reference-a")
        (self.outreach / "och-shawl-choice-a.jpg").write_bytes(reference)
        self.write_json("och-monitor-heartbeat.json", {"run": 1})

        # alpha: WhatsApp v1 manifest (paths relative to responses/), gallery (relative to manifest)
        a1 = self.write("alpha/och-alpha-received-01.png", png_bytes((10, 20, 30)))
        self.write("alpha/och-alpha-received-02.png", png_bytes((11, 21, 31)))
        self.write("alpha/och-alpha-received-03.png", png_bytes((12, 22, 32)))  # unmanifested
        self.write_json("alpha/och-alpha-media-manifest.json", {
            "captured_at": "2026-09-29T12:40:00Z",
            "photos": [
                {"index": 1, "file": "alpha/och-alpha-received-01.png", "sha256": sha(a1.read_bytes()),
                 "bytes": a1.stat().st_size, "viewer": "- generic: Today at 4:52 PM\n"},
                {"index": 2, "file": "alpha/och-alpha-received-02.png", "sha256": "0" * 64,
                 "viewer": "- generic: Today at 5:07 PM\n"},
            ],
        })
        self.write("alpha/whatsapp/preview-images/och-alpha-preview-001.png", png_bytes((40, 50, 60)))
        self.write("alpha/whatsapp/preview-images/och-alpha-preview-003.jpg", reference)
        self.write("alpha/whatsapp/preview-images/och-alpha-preview-004.png", png_bytes((1, 1, 1), width=9000))
        self.write_json("alpha/whatsapp/och-alpha-gallery-2026-09-30.json", {
            "observed_on": "2026-09-30",
            "items": [
                {"sequence_newest_first": 1, "sender": "Alpha", "time": "Yesterday at 8:22 PM",
                 "file": "preview-images/och-alpha-preview-001.png",
                 "sha256": sha(self.bytes["alpha/whatsapp/preview-images/och-alpha-preview-001.png"])},
                {"sequence_newest_first": 2, "sender": "You", "time": "Yesterday at 4:53 PM",
                 "skip_reason": "User reference"},
                {"sequence_newest_first": 3, "sender": "Alpha", "time": "Yesterday at 4:00 PM",
                 "file": "preview-images/och-alpha-preview-003.jpg"},
                {"sequence_newest_first": 4, "sender": "Alpha", "time": "Yesterday at 3:00 PM",
                 "file": "preview-images/och-alpha-preview-004.png"},
            ],
        })
        self.write("alpha/whatsapp/och-alpha-approved-reply-2026-10-02.png", jpeg_bytes("proof"))

        # beta: email originals (absolute paths), attachments (research-relative), PDF-derived (repo-relative)
        self.write_json("beta/email/m1/message.json", {
            "gmail_message_id": "m1", "date_header": "Tue, 29 Sep 2026 19:26:49 +0530",
            "subject": "Item 7 price list", "direction": "incoming"})
        photo = self.write("beta/email/m1/originals/photo-1.jpg", jpeg_bytes("beta-photo"))
        video = self.write("beta/email/m1/originals/clip.mp4", mp4_bytes())
        dup = self.write("beta/email/m1/originals/dup.png", self.bytes["alpha/och-alpha-received-01.png"])
        gif = self.write("beta/email/m1/originals/anim.gif", b"GIF89a" + b"\x00" * 20)
        files = [{"message_id": "m1", "filename": p.name, "local_path": str(p), "sha256": sha(p.read_bytes()),
                  "bytes": p.stat().st_size, "url": "https://files.example.test/" + p.name}
                 for p in (photo, video, dup, gif)]
        self.write_json("beta/email/originals-manifest-2026-09-30.json", {"files": files})
        self.write_json("beta/email/originals-manifest-batch-0.json", {"files": files[:1]})
        self.write_json("beta/email/m2/message.json", {
            "id": "m2", "internal_date": "1791187569000",
            "payload": {"headers": [{"name": "Subject", "value": "Catalogue"}]}})
        pdf = self.write("beta/email/m2/originals/cat.pdf", pdf_bytes())
        self.write_json("beta/email/m2/original-attachments-manifest.json", {
            "message_id": "m2",
            "attachments": [{"filename": "cat.pdf", "sha256": sha(pdf.read_bytes()),
                             "path": f"assets/{self.outreach.name}/responses/beta/email/m2/originals/cat.pdf"}]})
        derived = self.write("beta/email/m2/catalogue-images/catalogue-photo-01.jpg", jpeg_bytes("derived"))
        repo_rel = derived.relative_to(self.repo).as_posix()
        self.write_json("beta/email/m2/catalogue-images/manifest.json", {
            "source_message_id": "m2", "original_pdf": "originals/cat.pdf",
            "images": [{"file": repo_rel, "pages": [2], "sha256": sha(derived.read_bytes())}]})
        self.write("beta/email/m2/renders/page-01.png", png_bytes((90, 90, 90)))

        # a response folder with no ledger contact
        self.write("orphan/whatsapp/och-orphan-transcript.json", b"{}")

        contacts = [
            {
                "id": "alpha", "name": "Alpha Weaving / Alphabrand", "channel": "whatsapp",
                "destination": "+910000000001", "source": "https://alpha.example.test/contact",
                "alternates": ["+91 00000 00002", "sales@alpha.example.test", "https://www.instagram.com/alpha.example/",
                               "platform call +910000000003", "https://alpha.example.test/enquiry.html",
                               "+910000000001"],
                "status": "sent", "sent_at": "2026-09-29T11:48:00Z",
                "events": [{"type": "reply", "channel": "whatsapp", "received_at": "2026-09-29T11:55:00Z"},
                           {"type": "response-audit", "summary": f"internal ceiling {CEILING} compared"}],
                "quote": {"amount": 120, "currency": "INR", "unit": "stole", "minimum_order": 25,
                          "minimum_order_unit": "pieces", "received_at": "2026-09-29T11:53:00Z",
                          "amount_usd": 1.25, "internal_note": f"do not disclose: target {CEILING}"},
                "pricing_assessment": f"Below the {CEILING} ceiling",
                "profile_identity": {"display_name": "Test Person"},
                "address_book": {"application": "WhatsApp", "phone": "+910000000001"},
                "alternate_details": [{"value": "+910000000004", "channel": "whatsapp",
                                       "verification": "Inbound supplier enquiry reply at 10:00 Tashkent."}],
                "excluded_routes": [{"value": "https://www.instagram.com/wrong.example/", "reason": "other business"}],
                "received_media": {"photos_received": 2, "saved_format": "WhatsApp media-viewer screenshots"},
            },
            {
                "id": "beta", "name": "Beta Exports", "channel": "email", "destination": "beta@example.test",
                "source": "https://directory.example.test/listing?id=7",
                "alternates": ["old@beta.example.test", "https://www.facebook.com/beta.example/"],
                "status": "sent", "sent_at": "2026-09-29T11:44:00Z",
                "events": [
                    {"type": "delivery-failed", "destination": "old@beta.example.test"},
                    {"type": "reply", "channel": "email", "from": "beta@example.test",
                     "contact_name": "Mr. Jane Example", "contact_phone": "+910000000011"},
                    {"type": "reply", "channel": "email", "contact_name": "Beta Exports Pvt Ltd"},
                ],
                "quote": {"amount": None, "currency": None, "minimum_order": None, "goods_price_received": False},
                "profile_identity": {"display_name": "Beta Exports"},
                "contact_source_verification": {"published_contact_person": "JANE EXAMPLE",
                                                "published_phone": "+910000000011"},
                "new_contact_routes": [{"value": "+910000000011", "purpose": "Supplier requested WhatsApp contact",
                                        "whatsapp_account_verified": True}],
                "unverified_contacts": [{"value": "x@other.example.test", "channel": "email"}],
            },
            {"id": "gamma", "name": "Gamma Looms", "channel": "form", "destination": "https://gamma.example.test/enquiry",
             "source": "https://gamma.example.test/enquiry", "alternates": [], "sent_at": None,
             "status": "contact request sent; photos pending", "events": []},
            {"id": "delta", "name": "Delta Mills", "channel": "email", "destination": "delta@example.test",
             "source": "https://cdn.example.test/brochure.pdf", "alternates": [], "sent_at": None,
             "status": "form stalled; send unconfirmed", "events": []},
            {"id": "epsilon", "name": "Epsilon Stoles", "channel": "email", "destination": "eps@example.test",
             "source": "https://www.amazon.in/dp/item-1", "alternates": [], "status": "sent",
             "sent_at": "2026-09-29T11:44:00Z",
             "events": [{"type": "reply", "channel": "email",
                         "offers": [{"item": "E-1", "description": "Wool stole", "dimensions_cm": [70, 200],
                                     "price_usd": 9}]}]},
            {"id": "zeta", "name": "Zeta Shop", "channel": "shared-whatsapp", "destination": "+910000000001",
             "source": "https://zeta.example.test/", "alternates": [], "sent_at": None,
             "status": "covered by shared alpha enquiry", "events": []},
        ]
        ledger = {"contacts": contacts,
                  "sourcing_requirements": {"internal_maximum_usd_per_piece": CEILING,
                                            "disclose_price_ceiling_to_suppliers": False}}
        (self.research / LEDGER).write_text(json.dumps(ledger, indent=1))

        self.viewer.mkdir()
        (self.viewer / "suppliers.ts").write_text(
            "export const s = [\n"
            "  {\n    id: 'alpha-weaving',\n    name: 'Alpha Weaving',\n    city: 'Testville',\n"
            "    contacts: [{ label: 'x', value: '+91 00000 00001', kind: 'whatsapp' }],\n  },\n"
            "  {\n    id: 'zeta-shop',\n    name: 'Zeta Shop',\n    city: 'Othertown',\n"
            "    contacts: [{ label: 'x', value: '+91 00000 00001', kind: 'whatsapp' }],\n  },\n];\n")

    def tree_state(self) -> dict[str, tuple[int, int]]:
        return {str(p.relative_to(self.research)): (p.stat().st_size, p.stat().st_mtime_ns)
                for p in sorted(self.research.rglob("*")) if p.is_file()}


class BundleTestCase(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls._tmp = tempfile.TemporaryDirectory()
        cls.fx = Fixture(Path(cls._tmp.name))
        cls.before = cls.fx.tree_state()
        cls.out = Path(cls._tmp.name) / "out"
        cls.bundle, cls.report, cls.errors = bib.build(cls.fx.research, cls.out, LEDGER, cls.fx.viewer)
        cls.suppliers = {s["key"]: s for s in cls.bundle["suppliers"]}

    @classmethod
    def tearDownClass(cls) -> None:
        cls._tmp.cleanup()

    def media_named(self, name: str) -> dict | None:
        return next((m for m in self.bundle["media"] if m["originalName"] == name), None)

    def excluded_paths(self, reason_fragment: str) -> list[str]:
        return [Path(e.get("path", "")).name for e in self.report["exclusions"] if reason_fragment in e["reason"]]


class BundleShape(BundleTestCase):
    def test_bundle_validates(self):
        self.assertEqual(self.errors, [])
        self.assertEqual(bib.validate_bundle(self.bundle), [])
        self.assertEqual(self.bundle["format"], "ocharo.sourcing-import.v1")

    def test_snapshot_is_the_only_ledger_read_and_is_recorded(self):
        copy = self.out / "ledger-snapshot.json"
        self.assertEqual(copy.read_bytes(), (self.fx.research / LEDGER).read_bytes())
        self.assertIn(sha(copy.read_bytes()), self.bundle["source"])
        self.assertIn(str(copy.resolve()), self.bundle["source"])

    def test_research_folder_untouched(self):
        self.assertEqual(self.fx.tree_state(), self.before)

    def test_suppliers_sorted_and_country(self):
        keys = [s["key"] for s in self.bundle["suppliers"]]
        self.assertEqual(keys, sorted(keys))
        self.assertTrue(all(s["country"] == "IN" for s in self.bundle["suppliers"]))

    def test_unmatched_folder_reported_not_invented(self):
        self.assertEqual(self.report["unmatchedFolders"], ["orphan"])
        self.assertNotIn("orphan", self.suppliers)

    def test_deterministic_output(self):
        with tempfile.TemporaryDirectory() as other:
            again, _report, _errors = bib.build(self.fx.research, Path(other), LEDGER, self.fx.viewer)
        self.assertEqual(again["suppliers"], self.bundle["suppliers"])
        self.assertEqual(again["media"], self.bundle["media"])


class Stage(BundleTestCase):
    def test_stage_derivation(self):
        stages = {k: s["stage"] for k, s in self.suppliers.items()}
        self.assertEqual(stages, {"alpha": "quoted", "beta": "replied", "gamma": "contacted",
                                  "delta": "lead", "epsilon": "quoted", "zeta": "lead"})

    def test_never_qualified(self):
        self.assertEqual(bib.derive_stage({"quote": {"amount": 5}, "status": "qualified"})[0], "quoted")
        self.assertEqual(bib.derive_stage({"events": [{"type": "supplier-price-quote"}]})[0], "quoted")
        self.assertEqual(bib.derive_stage({"events": [{"type": "supplier-question"}]})[0], "replied")
        self.assertEqual(bib.derive_stage({"events": [{"type": "authorized-reply-sent"}],
                                           "sent_at": "2026-09-29T00:00:00Z"})[0], "contacted")
        self.assertEqual(bib.derive_stage({"status": "not sent"})[0], "lead")


class Notes(BundleTestCase):
    def test_ceiling_never_leaks(self):
        text = json.dumps(self.bundle)
        self.assertNotIn(str(CEILING), text)
        for supplier in self.bundle["suppliers"]:
            self.assertLessEqual(len(supplier["notes"]), 1000)

    def test_quote_wording(self):
        trial = bib.quote_sentences({"amount_min": 2.5, "amount_max": 5, "currency_symbol": "$", "minimum_order": 10,
                                     "minimum_order_kind": "accepted quality-trial quantity",
                                     "minimum_order_history": [{"minimum_order": 140}]})
        self.assertEqual(trial, ["Quoted $2.5–5 per piece; currency symbol as written, unconfirmed.",
                                 "Trial quantity accepted 10 pieces (standard minimum stated: 140)."])
        self.assertEqual(bib.quote_sentences({"minimum_order": 50, "minimum_order_scope": "per design"}),
                         ["Minimum order 50 pieces per design.", "No price quoted yet."])

    def test_notes_are_factual_summary(self):
        alpha = self.suppliers["alpha"]["notes"]
        self.assertIn("Quoted INR 120 per stole on 2026-09-29.", alpha)
        self.assertIn("Minimum order 25 pieces.", alpha)
        self.assertIn("Initial channel: WhatsApp", alpha)
        self.assertRegex(alpha, r"Imported from ledger snapshot \d{4}-\d{2}-\d{2}\.$")
        self.assertNotIn("1.25", alpha)  # our USD conversion is not a supplier quote
        self.assertIn("USD 9", self.suppliers["epsilon"]["notes"])
        self.assertIn("No price quoted yet.", self.suppliers["beta"]["notes"])
        self.assertIn("send not confirmed", self.suppliers["delta"]["notes"])


class People(BundleTestCase):
    def test_people_extraction(self):
        self.assertEqual([p["name"] for p in self.suppliers["alpha"]["people"]], ["Test Person"])
        beta = self.suppliers["beta"]["people"]
        self.assertEqual([(p["key"], p["name"].casefold()) for p in beta], [("jane-example", "jane example")])
        self.assertEqual(self.suppliers["gamma"]["people"], [])

    def test_name_filter(self):
        supplier = "Alpha Weaving"
        self.assertEqual(bib.clean_person_name("Mr. Jane Example", supplier), "Jane Example")
        for company in ("Beta Exports Pvt Ltd", "Alpha Weaving", "Omegabrand", "Kappa Fabric Testcity",
                        "Lambda International", None, "", "Name 2"):
            self.assertIsNone(bib.clean_person_name(company, supplier), company)


class Routes(BundleTestCase):
    def routes(self, key):
        return {(r["channel"], r["destination"]): r for r in self.suppliers[key]["routes"]}

    def test_classification_and_dedupe(self):
        routes = self.routes("alpha")
        self.assertEqual(set(routes), {
            ("whatsapp", "+910000000001"), ("phone", "+910000000002"), ("email", "sales@alpha.example.test"),
            ("instagram", "https://www.instagram.com/alpha.example/"), ("phone", "+910000000003"),
            ("form", "https://alpha.example.test/enquiry.html"), ("whatsapp", "+910000000004")})
        platform = routes[("phone", "+910000000003")]
        self.assertEqual(platform["label"], "platform-routed call")
        self.assertFalse(platform["verified"])

    def test_primary_verified_person(self):
        routes = self.routes("alpha")
        primary = routes[("whatsapp", "+910000000001")]
        self.assertTrue(primary["isPrimary"])
        self.assertTrue(primary["verified"])
        self.assertEqual(primary["personKey"], "test-person")
        self.assertIn("zeta", primary["notes"])
        self.assertTrue(routes[("whatsapp", "+910000000004")]["verified"])
        self.assertFalse(routes[("phone", "+910000000002")]["verified"])
        for supplier in self.bundle["suppliers"]:
            self.assertLessEqual(sum(r["isPrimary"] for r in supplier["routes"]), 1, supplier["key"])

    def test_excluded_and_unverified_routes_skipped(self):
        destinations = [r["destination"] for s in self.bundle["suppliers"] for r in s["routes"]]
        self.assertNotIn("https://www.instagram.com/wrong.example/", destinations)
        self.assertNotIn("x@other.example.test", destinations)

    def test_supplier_given_whatsapp_and_bounce(self):
        routes = self.routes("beta")
        whatsapp = routes[("whatsapp", "+910000000011")]
        self.assertTrue(whatsapp["verified"])
        self.assertEqual(whatsapp["personKey"], "jane-example")
        self.assertTrue(routes[("email", "beta@example.test")]["verified"])
        bounced = routes[("email", "old@beta.example.test")]
        self.assertFalse(bounced["verified"])
        self.assertIn("Delivery failed", bounced["notes"])
        self.assertIn(("facebook", "https://www.facebook.com/beta.example/"), routes)

    def test_shared_whatsapp(self):
        routes = self.routes("zeta")
        primary = routes[("whatsapp", "+910000000001")]
        self.assertTrue(primary["isPrimary"])
        self.assertFalse(primary["verified"])
        self.assertIn("Shared route", primary["notes"])

    def test_classify_value(self):
        self.assertEqual(bib.classify_value("platform call +91 0000-000003")[:2], ("phone", "+910000000003"))
        self.assertEqual(bib.classify_value("Sales@Example.test")[:2], ("email", "sales@example.test"))
        self.assertEqual(bib.classify_value("https://m.facebook.com/x")[0], "facebook")
        self.assertEqual(bib.classify_value("https://shop.example.test/")[0], "website")
        self.assertEqual(bib.classify_value("https://shop.example.test/contact-us")[0], "form")
        self.assertIsNone(bib.classify_value("call after 5"))


class Website(BundleTestCase):
    def test_website_and_city(self):
        self.assertEqual(self.suppliers["alpha"]["website"], "https://alpha.example.test/contact")
        self.assertEqual(self.suppliers["beta"]["website"], "https://directory.example.test/listing?id=7")
        self.assertIsNone(self.suppliers["delta"]["website"])    # a PDF is not a site
        self.assertIsNone(self.suppliers["epsilon"]["website"])  # retailer product page
        self.assertEqual(self.suppliers["alpha"]["city"], "Testville")
        self.assertEqual(self.suppliers["zeta"]["city"], "Othertown")  # same phone; name decides
        self.assertIsNone(self.suppliers["beta"]["city"])


class Media(BundleTestCase):
    def test_capture_and_receipt(self):
        first = self.media_named("och-alpha-received-01.png")
        self.assertEqual((first["capture"], first["receivedAt"]), ("screenshot", "2026-09-29T11:52:00Z"))
        self.assertEqual(first["sourceLabel"], "WhatsApp screenshot 2026-09-29")
        photo = self.media_named("photo-1.jpg")
        self.assertEqual((photo["capture"], photo["receivedAt"], photo["sourceMessageId"]),
                         ("original", "2026-09-29T13:56:49Z", "m1"))
        self.assertEqual(photo["caption"], "Email subject: Item 7 price list")
        self.assertEqual(self.media_named("clip.mp4")["capture"], "original")
        pdf = self.media_named("cat.pdf")
        self.assertEqual((pdf["capture"], pdf["receivedAt"]), ("original", "2026-10-05T08:06:09Z"))
        derived = self.media_named("catalogue-photo-01.jpg")
        self.assertEqual((derived["capture"], derived["sourceMessageId"], derived["caption"]),
                         ("derived", "m2", "PDF page 2"))
        self.assertEqual(derived["sourceLabel"], "Derived from PDF cat.pdf")

    def test_relative_times_stay_null(self):
        gallery = self.media_named("och-alpha-preview-001.png")
        self.assertIsNone(gallery["receivedAt"])
        self.assertIn("Yesterday at 8:22 PM", gallery["sourceLabel"])
        self.assertIsNone(bib.resolve_relative_time("Yesterday at 8:22 PM", None, None))

    def test_gallery_skip_reason_excluded(self):
        skipped = [e for e in self.report["exclusions"] if "gallery" in e["reason"]]
        self.assertEqual(len(skipped), 1)
        self.assertEqual(skipped[0]["supplier"], "alpha")

    def test_path_base_normalization(self):
        self.assertTrue({"absolute", "responses", "manifest-dir", "research", "ancestor"} <= set(self.report["pathBases"]))
        for item in self.bundle["media"]:
            self.assertTrue(Path(item["path"]).is_absolute())
            self.assertEqual(sha(Path(item["path"]).read_bytes()), item["sha256"])

    def test_sha_mismatch_warns_and_uses_computed(self):
        warnings = [w for w in self.report["warnings"] if w["code"] == "sha256-mismatch"]
        self.assertEqual(len(warnings), 1)
        item = self.media_named("och-alpha-received-02.png")
        self.assertEqual(item["sha256"], sha(self.fx.bytes["alpha/och-alpha-received-02.png"]))

    def test_unmanifested_received_file_included(self):
        item = self.media_named("och-alpha-received-03.png")
        self.assertIsNotNone(item)
        self.assertIn("unmanifested", item["sourceLabel"])

    def test_exclusions(self):
        self.assertIn("och-alpha-approved-reply-2026-10-02.png", self.excluded_paths("approved-reply"))
        self.assertIn("page-01.png", self.excluded_paths("render"))
        self.assertIn("och-alpha-preview-003.jpg", self.excluded_paths("outreach reference"))
        self.assertIn("och-alpha-preview-004.png", self.excluded_paths("dimensions"))
        self.assertIn("anim.gif", self.excluded_paths("unsupported content type"))
        self.assertIsNone(self.media_named("och-alpha-approved-reply-2026-10-02.png"))

    def test_cross_supplier_duplicate(self):
        self.assertIsNone(self.media_named("dup.png"))
        warning = [w for w in self.report["warnings"] if w["code"] == "cross-supplier-duplicate"]
        self.assertEqual([w["supplier"] for w in warning], ["beta"])
        self.assertEqual(self.report["suppliers"]["beta"]["manifestRelistings"], 1)

    def test_media_order_and_uniqueness(self):
        media = self.bundle["media"]
        keys = [(m["supplierKey"], m["receivedAt"] is None, m["receivedAt"] or "", m["originalName"]) for m in media]
        self.assertEqual(keys, sorted(keys))
        self.assertEqual(len({m["sha256"] for m in media}), len(media))

    def test_size_limit(self):
        with tempfile.TemporaryDirectory() as tmp, \
                mock.patch.dict(bib.SIZE_LIMITS, {"application/pdf": 10}):
            fx = Fixture(Path(tmp))
            bundle, report, _ = bib.build(fx.research, Path(tmp) / "out", LEDGER)
        self.assertNotIn("cat.pdf", [m["originalName"] for m in bundle["media"]])
        self.assertTrue(any("limit" in e["reason"] and e.get("path", "").endswith("cat.pdf")
                            for e in report["exclusions"]))


class Validation(unittest.TestCase):
    def test_validator_rejects_contract_violations(self):
        bad = {"format": "x", "source": "", "suppliers": [
            {"key": "a", "name": "", "stage": "qualified", "country": "in", "city": None, "website": "ftp://x",
             "notes": "", "people": [{"key": "Bad Key", "name": "Xa Yb", "role": None, "notes": ""}], "routes": [
                 {"channel": "pager", "destination": "1", "label": None, "sourceUrl": None, "verified": True,
                  "isPrimary": True, "personKey": "ghost", "notes": ""},
                 {"channel": "email", "destination": "a@example.test", "label": None, "sourceUrl": None,
                  "verified": False, "isPrimary": True, "personKey": None, "notes": ""}]}],
            "media": [{"supplierKey": "b", "path": "rel.png", "sha256": "XYZ", "capture": "photo",
                       "originalName": "rel.png", "caption": "", "sourceLabel": None, "sourceMessageId": None,
                       "receivedAt": "yesterday"}]}
        errors = "\n".join(bib.validate_bundle(bad))
        for fragment in ("format", "source", ".name", "person[Bad Key].key", ".stage", ".country", ".website", ".channel",
                         "personKey", "more than one primary", "supplierKey", ".path", ".sha256", ".capture",
                         ".receivedAt"):
            self.assertIn(fragment, errors)


class Cli(unittest.TestCase):
    def test_cli_isolated_mode(self):
        with tempfile.TemporaryDirectory() as tmp:
            fx = Fixture(Path(tmp))
            out = Path(tmp) / "cli-out"
            result = subprocess.run(
                [sys.executable, "-I", str(SCRIPT), "--research", str(fx.research), "--out", str(out),
                 "--ledger", LEDGER], capture_output=True, text=True, env={"PATH": os.environ.get("PATH", "")})
            self.assertEqual(result.returncode, 0, result.stderr)
            for name in ("ledger-snapshot.json", "sourcing-bundle.json", "report.json"):
                self.assertTrue((out / name).is_file(), name)
            bundle = json.loads((out / "sourcing-bundle.json").read_text())
            self.assertEqual(bib.validate_bundle(bundle), [])

    def test_cli_refuses_output_inside_responses(self):
        with tempfile.TemporaryDirectory() as tmp:
            fx = Fixture(Path(tmp))
            with contextlib.redirect_stderr(io.StringIO()):
                code = bib.main(["--research", str(fx.research), "--out", str(fx.responses / "x"), "--ledger", LEDGER])
            self.assertEqual(code, 2)
            self.assertFalse((fx.responses / "x").exists())


if __name__ == "__main__":
    unittest.main()
