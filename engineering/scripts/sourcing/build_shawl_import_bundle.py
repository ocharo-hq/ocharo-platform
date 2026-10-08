#!/usr/bin/env python3
"""Build a Desk Sourcing import bundle from the private shawl-sourcing ledger.

Reads the outreach ledger (through a snapshot copy) and the supplier response
folders, and writes three files into ``--out``:

* ``ledger-snapshot.json`` - byte copy of the ledger; the only ledger bytes read
* ``sourcing-bundle.json`` - ``ocharo.sourcing-import.v1`` bundle for the Desk CLI
* ``report.json``          - counts, warnings and exclusions with reasons

The research folder is never modified. Standard library only; runs under
``python3 -I``. Contract: engineering/architecture/sourcing-contract.md.
"""
from __future__ import annotations

import argparse
import datetime as dt
import email.utils
import hashlib
import json
import re
import shutil
import struct
import sys
import time
from collections import Counter, OrderedDict
from pathlib import Path
from urllib.parse import urlparse

FORMAT = "ocharo.sourcing-import.v1"
TASHKENT = dt.timezone(dt.timedelta(hours=5))
DEFAULT_LEDGER = "och-shawl-outreach-2026-09.json"

STAGES = ("lead", "contacted", "replied", "quoted", "qualified", "dropped")
CHANNELS = ("whatsapp", "email", "phone", "form", "website", "instagram", "facebook",
            "telegram", "marketplace", "other")
CAPTURES = ("original", "screenshot", "derived")

MAX_NAME = 160
MAX_SHORT = 120          # role, label, city
MAX_DESTINATION = 512
MAX_URL = 2048
MAX_SUPPLIER_NOTES = 8000
MAX_NOTES = 4000         # person, route
MAX_CAPTION = 2000
MAX_SOURCE_LABEL = 200
MAX_ORIGINAL_NAME = 255
NOTES_TARGET = 1000      # supplier notes are kept short on purpose

MIB = 1024 * 1024
SIZE_LIMITS = {
    "image/jpeg": 20 * MIB,
    "image/png": 20 * MIB,
    "image/webp": 20 * MIB,
    "application/pdf": 25 * MIB,
    "video/mp4": 64 * MIB,
}
MAX_IMAGE_SIDE = 8192
MAX_IMAGE_PIXELS = 50_000_000
MEDIA_EXTENSIONS = {".png", ".jpg", ".jpeg", ".webp", ".gif", ".heic", ".heif",
                    ".pdf", ".mp4", ".mov", ".m4v"}

# Ledger event types that record a message *from* the supplier. Audits, drafts
# and our own sends are deliberately absent; any "supplier-*" type also counts.
INBOUND_EVENT_TYPES = {
    "reply", "reply-preview", "reply-notification", "received-media-archived",
    "incoming-follow-up-reviewed",
}
INITIAL_CHANNEL_WORDS = {
    "whatsapp": "WhatsApp", "shared-whatsapp": "WhatsApp (shared route)",
    "email": "email", "form": "web form",
}
HONORIFICS = {"mr", "mrs", "ms", "miss", "dr", "shri", "sh", "smt", "sri", "md"}
COMPANY_WORDS = {
    "and", "brand", "co", "collection", "collections", "company", "corp", "corporation",
    "creations", "emporium", "enterprise", "enterprises", "export", "exporters", "exports",
    "fabric", "fabrics", "fashion", "fashions", "group", "handicraft", "handicrafts",
    "house", "inc", "india", "industries", "industry", "international", "llp", "ltd",
    "limited", "mill", "mills", "overseas", "private", "pvt", "scarf", "scarves", "shawl",
    "shawls", "shop", "sons", "store", "stoles", "textile", "textiles", "trader", "traders",
    "trading", "weaves", "woollen",
}
SOCIAL_HOSTS = {
    "instagram.com": "instagram", "facebook.com": "facebook", "fb.com": "facebook",
    "fb.me": "facebook", "wa.me": "whatsapp", "whatsapp.com": "whatsapp",
    "t.me": "telegram", "telegram.me": "telegram",
}
MARKETPLACE_HOSTS = {"indiamart.com", "tradeindia.com", "alibaba.com", "exportersindia.com"}
NOT_A_SITE_HOSTS = {
    "jiomart.com", "amazon.in", "amazon.com", "flipkart.com", "myntra.com", "meesho.com",
    "etsy.com", "ebay.com", "imimg.com", "google.com", "googleusercontent.com",
    "youtube.com", "linkedin.com", "twitter.com", "x.com",
} | set(SOCIAL_HOSTS)
DOCUMENT_SUFFIXES = (".pdf", ".jpg", ".jpeg", ".png", ".webp", ".gif", ".doc", ".docx",
                     ".xls", ".xlsx", ".zip")

EMAIL_RE = re.compile(r"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$")
EMAIL_IN_TEXT_RE = re.compile(r"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")
PHONE_RE = re.compile(r"^\+?[\d\s\-().]{8,24}$")
PLATFORM_RE = re.compile(r"^\s*platform[- ]call\s+(.+)$", re.IGNORECASE)
SENT_STATUS_RE = re.compile(r"(?<!not )(?<!un)\bsent\b", re.IGNORECASE)
RELATIVE_TIME_RE = re.compile(r"\b(Today|Yesterday)\s+at\s+(\d{1,2}:\d{2}\s*[AP]M)", re.IGNORECASE)
LOCAL_STAMP_RE = re.compile(r"^(\d{4}-\d{2}-\d{2})[ T](\d{1,2}:\d{2})(?::(\d{2}))?\s*(GMT\+5|UTC\+5|\+05:?00)?$")
ISO_UTC_RE = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$")
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
KEY_RE = re.compile(r"^[a-z0-9][a-z0-9-]{0,63}$")  # Desk import key rule
MAX_MESSAGE_ID = 200
RECEIVED_FILE_RE = re.compile(r"-received-\d+\.[A-Za-z0-9]+$")
MANIFEST_NAME_RE = re.compile(r"(manifest|gallery|-media-)", re.IGNORECASE)


# --------------------------------------------------------------------------- report

class Report:
    """Collects warnings and exclusions; both end up in report.json."""

    def __init__(self) -> None:
        self.warnings: list[dict] = []
        self.exclusions: list[dict] = []
        self.notes: list[dict] = []

    def warn(self, code: str, message: str, supplier: str | None = None, **extra) -> None:
        self.warnings.append(_compact({"code": code, "supplier": supplier, "message": message, **extra}))

    def exclude(self, reason: str, supplier: str | None = None, **extra) -> None:
        self.exclusions.append(_compact({"reason": reason, "supplier": supplier, **extra}))

    def note(self, code: str, message: str, supplier: str | None = None, **extra) -> None:
        self.notes.append(_compact({"code": code, "supplier": supplier, "message": message, **extra}))


def _compact(d: dict) -> dict:
    return {k: v for k, v in d.items() if v is not None}


# --------------------------------------------------------------------------- small helpers

def utc_now() -> dt.datetime:
    return dt.datetime.now(dt.timezone.utc).replace(microsecond=0)


def iso_utc(value: dt.datetime | None) -> str | None:
    if value is None:
        return None
    if value.tzinfo is None:
        return None
    return value.astimezone(dt.timezone.utc).replace(microsecond=0).strftime("%Y-%m-%dT%H:%M:%SZ")


def parse_iso(value) -> dt.datetime | None:
    if not isinstance(value, str) or not value.strip():
        return None
    text = value.strip().replace("Z", "+00:00")
    try:
        parsed = dt.datetime.fromisoformat(text)
    except ValueError:
        return None
    return parsed if parsed.tzinfo else None


def parse_local_stamp(value) -> dt.datetime | None:
    """'2026-10-02 22:45 GMT+5' -> aware datetime. Bare local stamps are Tashkent time."""
    if not isinstance(value, str):
        return None
    m = LOCAL_STAMP_RE.match(value.strip())
    if not m:
        return None
    date_part, hm, sec, _tz = m.groups()
    try:
        naive = dt.datetime.fromisoformat(f"{date_part}T{hm.zfill(5)}:{sec or '00'}")
    except ValueError:
        return None
    return naive.replace(tzinfo=TASHKENT)


def resolve_relative_time(text, reference: dt.datetime | None, reference_date: dt.date | None = None):
    """Resolve 'Today at 4:52 PM' against the capture time (Tashkent). 'Yesterday' and
    anything else relative stays unresolved (None) on purpose."""
    if not isinstance(text, str):
        return None
    m = RELATIVE_TIME_RE.search(text)
    if not m or m.group(1).lower() != "today":
        return None
    if reference is not None:
        local_ref = reference.astimezone(TASHKENT)
        day = local_ref.date()
    elif reference_date is not None:
        local_ref, day = None, reference_date
    else:
        return None
    try:
        clock = dt.datetime.strptime(m.group(2).replace(" ", "").upper(), "%I:%M%p").time()
    except ValueError:
        return None
    resolved = dt.datetime.combine(day, clock, tzinfo=TASHKENT)
    if local_ref is not None and resolved > local_ref + dt.timedelta(minutes=1):
        return None  # viewer "today" would be after the capture: not trustworthy
    return resolved


def parse_email_date(value) -> dt.datetime | None:
    if not isinstance(value, str) or not value.strip():
        return None
    try:
        parsed = email.utils.parsedate_to_datetime(value.strip())
    except (TypeError, ValueError, IndexError):
        return None
    return parsed if parsed and parsed.tzinfo else None


def clip(text: str, limit: int) -> str:
    text = re.sub(r"\s+", " ", text).strip()
    return text if len(text) <= limit else text[: limit - 1].rstrip() + "…"


def slugify(text: str) -> str:
    slug = re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-")[:60].strip("-")
    return slug or "person"


def normalize_phone(value) -> str | None:
    if not isinstance(value, str):
        return None
    m = PLATFORM_RE.match(value)
    if m:
        value = m.group(1)
    digits = re.sub(r"\D", "", value)
    if not 8 <= len(digits) <= 15:
        return None
    return "+" + digits


def looks_like_phone(value: str) -> bool:
    return bool(PHONE_RE.match(value.strip())) and len(re.sub(r"\D", "", value)) >= 8


def is_http_url(value) -> bool:
    if not isinstance(value, str):
        return False
    parsed = urlparse(value.strip())
    return parsed.scheme in ("http", "https") and bool(parsed.netloc)


def host_of(url: str) -> str:
    host = urlparse(url.strip()).netloc.lower().split("@")[-1].split(":")[0]
    return host[4:] if host.startswith("www.") else host


def host_in(host: str, domains) -> str | None:
    for domain in domains:
        if host == domain or host.endswith("." + domain):
            return domain
    return None


def url_key(url: str) -> str:
    parsed = urlparse(url.strip())
    return (host_of(url) + parsed.path.rstrip("/") + (("?" + parsed.query) if parsed.query else "")).lower()


def extract_email(value) -> str | None:
    if not isinstance(value, str):
        return None
    m = EMAIL_IN_TEXT_RE.search(value)
    return m.group(0).lower() if m else None


def as_list(value) -> list:
    return value if isinstance(value, list) else []


def as_dict(value) -> dict:
    return value if isinstance(value, dict) else {}


def is_number(value) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool)


def fmt_number(value) -> str:
    if isinstance(value, float) and value.is_integer():
        value = int(value)
    if isinstance(value, float):
        return f"{value:.2f}".rstrip("0").rstrip(".")
    return str(value)


# --------------------------------------------------------------------------- ledger snapshot

def snapshot_ledger(ledger_path: Path, out_dir: Path, attempts: int = 3, pause: float = 2.0):
    """Copy the live ledger once, hash the copy and parse only the copy.
    A copy taken mid-rewrite fails to parse and is retaken."""
    target = out_dir / "ledger-snapshot.json"
    last_error = None
    for attempt in range(attempts):
        taken_at = utc_now()
        shutil.copyfile(ledger_path, target)
        raw = target.read_bytes()
        try:
            ledger = json.loads(raw.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            last_error = exc
            if attempt + 1 < attempts:
                time.sleep(pause)
            continue
        if not isinstance(ledger, dict) or not isinstance(ledger.get("contacts"), list):
            raise ValueError("ledger snapshot has no contacts[] list")
        return ledger, hashlib.sha256(raw).hexdigest(), taken_at, target
    raise ValueError(f"ledger snapshot did not parse after {attempts} attempts: {last_error}")


# --------------------------------------------------------------------------- stage

def is_inbound(event: dict) -> bool:
    kind = str(event.get("type") or "")
    return kind in INBOUND_EVENT_TYPES or kind.startswith("supplier-")


def quote_has_price(quote: dict) -> bool:
    for key in ("amount", "amount_min", "amount_max", "amount_usd", "price_usd"):
        if is_number(quote.get(key)):
            return True
    price_range = as_dict(quote.get("price_range_usd_per_piece"))
    if is_number(price_range.get("min")) or is_number(price_range.get("max")):
        return True
    if any(is_number(as_dict(t).get("amount_usd")) for t in as_list(quote.get("price_tiers"))):
        return True
    return any(isinstance(quote.get(k), str) and quote.get(k).strip()
               for k in ("quote_text", "price_text", "price_raw_text"))


def event_offers(contact: dict) -> list[dict]:
    offers = []
    for event in as_list(contact.get("events")):
        if isinstance(event, dict) and is_inbound(event):
            offers.extend(o for o in as_list(event.get("offers")) if isinstance(o, dict))
    return offers


def has_media_received(contact: dict) -> bool:
    media = as_dict(contact.get("received_media"))
    counts = [media.get(k) for k in ("photos_received", "copies_saved", "original_photos_saved",
                                     "original_videos_saved")]
    return any(is_number(c) and c > 0 for c in counts) or media.get("original_pdf_saved") is True


def derive_stage(contact: dict) -> tuple[str, str]:
    """Return (stage, reason). Never 'qualified'."""
    events = [e for e in as_list(contact.get("events")) if isinstance(e, dict)]
    quote = contact.get("quote")
    if isinstance(quote, dict) and quote_has_price(quote):
        return "quoted", "quote object with a price"
    if any(e.get("type") == "supplier-price-quote" for e in events):
        return "quoted", "supplier-price-quote event"
    if event_offers(contact):
        return "quoted", "offers in a supplier reply"
    if any(is_inbound(e) for e in events):
        return "replied", "inbound supplier event"
    if has_media_received(contact):
        return "replied", "supplier media received"
    if contact.get("sent_at") or SENT_STATUS_RE.search(str(contact.get("status") or "")):
        return "contacted", "enquiry sent"
    return "lead", "no confirmed send"


# --------------------------------------------------------------------------- notes

def money(amount, currency: str | None, symbol: str | None) -> str:
    if currency:
        return f"{currency} {fmt_number(amount)}"
    if symbol:
        return f"{symbol}{fmt_number(amount)}"
    return fmt_number(amount)


def quote_sentences(quote: dict) -> list[str]:
    """Supplier-stated figures only; internal targets and our conversions are never read."""
    out = []
    currency = quote.get("currency") if isinstance(quote.get("currency"), str) else None
    symbol = quote.get("currency_symbol") if isinstance(quote.get("currency_symbol"), str) else None
    written = quote.get("currency_text") if isinstance(quote.get("currency_text"), str) else None
    unit = quote.get("unit") if isinstance(quote.get("unit"), str) and quote.get("unit") else "piece"
    amount, low, high = quote.get("amount"), quote.get("amount_min"), quote.get("amount_max")
    price = None
    if is_number(amount):
        price = money(amount, currency, symbol)
    elif is_number(low) and is_number(high) and low != high:
        price = f"{money(low, currency, symbol)}–{fmt_number(high)}"
    elif is_number(low) or is_number(high):
        price = money(low if is_number(low) else high, currency, symbol)
    if price:
        sentence = f"Quoted {price} per {unit}"
        if isinstance(quote.get("price_basis"), str) and quote["price_basis"].strip():
            sentence += f" ({quote['price_basis'].strip()})"
        received = parse_iso(quote.get("price_received_at")) or parse_iso(quote.get("received_at"))
        if received:
            sentence += f" on {received.astimezone(TASHKENT).date().isoformat()}"
        if not currency:
            if written:
                sentence += f"; currency written as '{written}', unconfirmed"
            elif symbol:
                sentence += "; currency symbol as written, unconfirmed"
            else:
                sentence += "; currency not stated"
        out.append(sentence + ".")
    minimum = quote.get("minimum_order")
    if is_number(minimum):
        moq_unit = quote.get("minimum_order_unit") if isinstance(quote.get("minimum_order_unit"), str) else "pieces"
        kind = str(quote.get("minimum_order_kind") or "").lower()
        is_trial = kind.startswith("accepted") and "trial" in kind
        sentence = f"{'Trial quantity accepted' if is_trial else 'Minimum order'} {fmt_number(minimum)} {moq_unit}"
        if isinstance(quote.get("minimum_order_scope"), str) and quote["minimum_order_scope"].strip():
            sentence += f" {quote['minimum_order_scope'].strip()}"
        earlier = [fmt_number(h.get("minimum_order")) for h in as_list(quote.get("minimum_order_history"))
                   if isinstance(h, dict) and is_number(h.get("minimum_order")) and h.get("minimum_order") != minimum]
        if earlier:
            sentence += f" ({'standard minimum' if is_trial else 'earlier'} stated: {', '.join(earlier)})"
        out.append(sentence + ".")
    trial = quote.get("accepted_trial_quantity_pieces")
    if is_number(trial):
        assorted = " assorted" if quote.get("mixed_design_trial_confirmed") is True else ""
        out.append(f"Trial offered: {fmt_number(trial)}{assorted} pieces.")
    for key in ("materials", "base_fabric"):
        if isinstance(quote.get(key), str) and quote[key].strip():
            out.append(f"Material as stated: {quote[key].strip()}.")
            break
    if not price:
        out.append("No price quoted yet.")
    return out


def offers_sentence(offers: list[dict]) -> str | None:
    parts = []
    for offer in offers:
        if not is_number(offer.get("price_usd")):
            continue
        bits = [str(offer.get("item") or "").strip(), str(offer.get("description") or "").strip()]
        dims = as_list(offer.get("dimensions_cm"))
        if len(dims) == 2 and all(is_number(d) for d in dims):
            bits.append(f"{fmt_number(dims[0])}×{fmt_number(dims[1])} cm")
        bits.append(f"USD {fmt_number(offer['price_usd'])}")
        parts.append(" ".join(b for b in bits if b))
    return ("Quoted per piece: " + "; ".join(parts) + ".") if parts else None


def build_supplier_notes(contact: dict, snapshot_date: str, shared_with: list[str]) -> str:
    sentences: list[str] = []
    quote = contact.get("quote")
    if isinstance(quote, dict):
        sentences.extend(quote_sentences(quote))
    elif (line := offers_sentence(event_offers(contact))):
        sentences.append(line)
    elif any(isinstance(e, dict) and e.get("type") == "supplier-price-quote" for e in as_list(contact.get("events"))):
        sentences.append("Price quote received; figures not structured in the ledger.")
    channel = str(contact.get("channel") or "")
    initial = f"Initial channel: {INITIAL_CHANNEL_WORDS.get(channel, channel or 'unknown')}"
    if shared_with:
        initial += f", route shared with {', '.join(shared_with)}"
    sent = parse_iso(contact.get("sent_at"))
    if sent:
        initial += f"; enquiry sent {sent.astimezone(TASHKENT).date().isoformat()}"
    elif not SENT_STATUS_RE.search(str(contact.get("status") or "")):
        initial += "; send not confirmed"
    sentences.append(initial + ".")
    head = " ".join(sentences)
    tail = f"Imported from ledger snapshot {snapshot_date}."
    budget = NOTES_TARGET - len(tail) - 1
    if len(head) > budget:
        head = head[: budget - 1].rstrip() + "…"
    return f"{head} {tail}".strip()


# --------------------------------------------------------------------------- people

def clean_person_name(raw, supplier_name: str) -> str | None:
    """Conservative: two to four alphabetic words, honorifics stripped, no company words."""
    if not isinstance(raw, str):
        return None
    words = re.sub(r"\s+", " ", raw).strip().split(" ")
    while words and words[0].lower().rstrip(".") in HONORIFICS:
        words.pop(0)
    if not 2 <= len(words) <= 4:
        return None
    if any(not re.fullmatch(r"[A-Za-z][A-Za-z'\-]*\.?", w) for w in words):
        return None
    lowered = {w.lower().rstrip(".") for w in words}
    if lowered & COMPANY_WORDS:
        return None
    if lowered <= set(re.findall(r"[a-z]+", supplier_name.lower())):
        return None
    name = " ".join(words)
    return name if len(name) <= MAX_NAME else None


def chat_number(contact: dict) -> str | None:
    """The WhatsApp number whose chat the ledger monitors for this supplier."""
    book = as_dict(contact.get("address_book"))
    if "whatsapp" in str(book.get("application") or "").lower():
        phone = normalize_phone(book.get("phone"))
        if phone:
            return phone
    if contact.get("channel") in ("whatsapp", "shared-whatsapp"):
        return normalize_phone(contact.get("destination"))
    return None


def extract_people(contact: dict) -> list[dict]:
    """Return [{key, name, role, notes, _phone}] in discovery order, deduped case-insensitively."""
    supplier_name = str(contact.get("name") or "")
    candidates: list[tuple[object, str, str | None]] = []
    chat = chat_number(contact)
    profile = as_dict(contact.get("profile_identity"))
    candidates.append((profile.get("display_name"), "WhatsApp profile name", chat))
    business = as_dict(contact.get("whatsapp_profile_identity"))
    business_phone = chat
    for route in as_list(contact.get("new_contact_routes")):
        if isinstance(route, dict) and business_phone is None:
            business_phone = normalize_phone(route.get("value"))
    candidates.append((business.get("display_name"), "WhatsApp business profile name", business_phone))
    published = as_dict(contact.get("contact_source_verification"))
    candidates.append((published.get("published_contact_person"), "contact person published on supplier page",
                       normalize_phone(published.get("published_phone"))))
    for event in as_list(contact.get("events")):
        if not isinstance(event, dict):
            continue
        candidates.append((event.get("contact_name"), "named in a supplier message",
                           normalize_phone(event.get("contact_phone"))))
        candidates.append((event.get("supplier_person"), "named in a supplier message", None))
        candidates.append((event.get("profile_name"), "WhatsApp profile name", chat))
    people: "OrderedDict[str, dict]" = OrderedDict()
    used_keys: set[str] = set()
    for raw, source, phone in candidates:
        name = clean_person_name(raw, supplier_name)
        if not name:
            continue
        folded = name.casefold()
        if folded in people:
            if phone and not people[folded]["_phone"]:
                people[folded]["_phone"] = phone
            continue
        key = slugify(name)
        n = 2
        while key in used_keys:
            key = f"{slugify(name)}-{n}"
            n += 1
        used_keys.add(key)
        people[folded] = {"key": key, "name": name, "role": None,
                          "notes": f"Source: {source} (ledger).", "_phone": phone}
    return list(people.values())


# --------------------------------------------------------------------------- routes

def classify_value(raw: str):
    """Classify a free-form route string -> (channel, destination, label, platform) or None."""
    value = raw.strip()
    if PLATFORM_RE.match(value):
        phone = normalize_phone(value)
        return ("phone", phone, "platform-routed call", True) if phone else None
    if value.lower().startswith("mailto:"):
        value = value[7:]
    if EMAIL_RE.match(value):
        return "email", value.lower(), None, False
    if is_http_url(value):
        host = host_of(value)
        social = host_in(host, SOCIAL_HOSTS)
        if social:
            channel = SOCIAL_HOSTS[social]
            if channel == "whatsapp":
                phone = normalize_phone(urlparse(value).path)
                return ("whatsapp", phone, None, False) if phone else ("other", value, None, False)
            return channel, value, None, False
        if host_in(host, MARKETPLACE_HOSTS):
            return "marketplace", value, None, False
        if re.search(r"(contact|enquiry|inquiry|form)", urlparse(value).path, re.IGNORECASE):
            return "form", value, None, False
        return "website", value, None, False
    if looks_like_phone(value):
        phone = normalize_phone(value)
        return ("phone", phone, None, False) if phone else None
    return None


def whatsapp_evidence(contact: dict) -> set[str]:
    numbers: set[str] = set()

    def add(value):
        phone = normalize_phone(value) if isinstance(value, str) and not PLATFORM_RE.match(value) else None
        if phone:
            numbers.add(phone)

    if contact.get("channel") in ("whatsapp", "shared-whatsapp"):
        add(contact.get("destination"))
    for key in ("address_book", "alternate_address_book"):
        book = as_dict(contact.get(key))
        if "whatsapp" in str(book.get("application") or "").lower():
            add(book.get("phone"))
    for detail in as_list(contact.get("alternate_details")):
        if isinstance(detail, dict) and detail.get("channel") == "whatsapp":
            add(detail.get("value"))
    for route in as_list(contact.get("new_contact_routes")):
        if isinstance(route, dict) and route.get("whatsapp_account_verified") is True:
            add(route.get("value"))
    for event in as_list(contact.get("events")):
        if not isinstance(event, dict):
            continue
        fields = ("phone", "contact_phone", "primary_phone", "alternate_phone", "account", "destination")
        if event.get("whatsapp_registered") is True or event.get("channel") == "whatsapp":
            for field in fields:
                add(event.get(field))
    return numbers


def verified_evidence(contact: dict) -> tuple[set[tuple[str, str]], dict[tuple[str, str], str]]:
    """Routes the ledger shows working: an inbound message arrived on them, or the
    ledger records an explicit verification. Returns (verified keys, failed keys)."""
    verified: set[tuple[str, str]] = set()
    failed: dict[tuple[str, str], str] = {}
    primary_channel = contact.get("channel")
    chat = chat_number(contact)
    primary_email = extract_email(contact.get("destination")) if primary_channel == "email" else None
    for event in as_list(contact.get("events")):
        if not isinstance(event, dict):
            continue
        if event.get("type") == "delivery-failed":
            dest = extract_email(event.get("destination")) or normalize_phone(event.get("destination"))
            if dest:
                channel = "email" if "@" in dest else "phone"
                failed[(channel, dest)] = str(event.get("observed_at") or event.get("recorded_at") or "")[:10]
            continue
        if not is_inbound(event):
            continue
        channel = event.get("channel")
        if channel is None:
            channel = {"whatsapp": "whatsapp", "shared-whatsapp": "whatsapp", "email": "email"}.get(primary_channel)
        if channel == "whatsapp":
            phone = normalize_phone(event.get("account")) or chat
            if phone:
                verified.add(("whatsapp", phone))
        elif channel == "email":
            address = extract_email(event.get("from")) or primary_email
            if address:
                verified.add(("email", address))
    media = as_dict(contact.get("received_media"))
    if has_media_received(contact):
        where = f"{media.get('saved_format', '')} {media.get('manifest', '')}".lower()
        if "whatsapp" in where and chat:
            verified.add(("whatsapp", chat))
        elif "/email/" in where and primary_email:
            verified.add(("email", primary_email))
    for detail in as_list(contact.get("alternate_details")):
        if not isinstance(detail, dict):
            continue
        text = str(detail.get("verification") or "")
        explicit = bool(detail.get("verified_on")) or bool(re.search(r"inbound.*repl", text, re.IGNORECASE))
        if explicit and isinstance(detail.get("value"), str):
            hit = classify_value(detail["value"])
            if hit:
                channel = detail.get("channel") if detail.get("channel") in CHANNELS else hit[0]
                verified.add((channel, hit[1]))
    for route in as_list(contact.get("new_contact_routes")):
        if isinstance(route, dict) and route.get("whatsapp_account_verified") is True:
            phone = normalize_phone(route.get("value"))
            if phone:
                verified.add(("whatsapp", phone))
    return verified, failed


def route_dedupe_key(channel: str, destination: str) -> tuple[str, str]:
    if is_http_url(destination):
        return channel, url_key(destination)
    return channel, destination.lower()


class RouteSet:
    def __init__(self) -> None:
        self.routes: "OrderedDict[tuple[str, str], dict]" = OrderedDict()

    def add(self, channel: str, destination: str, *, label=None, source_url=None, verified=False,
            primary=False, person_key=None, notes=()) -> None:
        key = route_dedupe_key(channel, destination)
        notes = [n for n in notes if n]
        if key in self.routes:
            route = self.routes[key]
            route["isPrimary"] = route["isPrimary"] or primary
            route["verified"] = route["verified"] or verified
            route["label"] = route["label"] or label
            route["sourceUrl"] = route["sourceUrl"] or source_url
            route["personKey"] = route["personKey"] or person_key
            for note in notes:
                if note not in route["_notes"]:
                    route["_notes"].append(note)
            return
        self.routes[key] = {"channel": channel, "destination": destination, "label": label,
                            "sourceUrl": source_url, "verified": verified, "isPrimary": primary,
                            "personKey": person_key, "_notes": list(notes)}


def build_routes(contact: dict, people: list[dict], shared: dict[tuple[str, str], list[str]],
                 report: Report) -> list[dict]:
    key = contact.get("id")
    on_whatsapp = whatsapp_evidence(contact)
    verified, failed = verified_evidence(contact)

    def effective_channel(channel: str, destination: str) -> str:
        return "whatsapp" if channel == "phone" and destination in on_whatsapp else channel

    verified_keys = {route_dedupe_key(effective_channel(c, d), d) for c, d in verified}
    failed_destinations = {d.lower() for _c, d in failed}
    person_by_phone = {p["_phone"]: p["key"] for p in people if p.get("_phone")}
    routes = RouteSet()

    def finish(channel, destination, **kwargs):
        if not destination:
            return
        is_platform = kwargs.get("label") == "platform-routed call"
        if not is_platform:
            channel = effective_channel(channel, destination)
        if channel not in CHANNELS:
            channel = "other"
        if len(destination) > MAX_DESTINATION:
            report.exclude("route destination over 512 characters", key, channel=channel)
            return
        notes = list(kwargs.pop("notes", ()))
        kwargs["verified"] = bool(kwargs.get("verified") or route_dedupe_key(channel, destination) in verified_keys) \
            and not is_platform
        if destination.lower() in failed_destinations:
            kwargs["verified"] = False
            notes.append("Delivery failed (bounce) per ledger.")
        others = [s for s in shared.get(route_dedupe_key("any", destination), []) if s != key]
        if others:
            notes = [n for n in notes if n != "shared route"]
            notes.append(f"Shared route; also recorded for: {', '.join(sorted(others))}.")
        if channel in ("whatsapp", "phone") and destination in person_by_phone:
            kwargs.setdefault("person_key", person_by_phone[destination])
        source_url = kwargs.get("source_url")
        if source_url and (not is_http_url(source_url) or len(source_url) > MAX_URL):
            kwargs["source_url"] = None
        routes.add(channel, destination, notes=notes, **kwargs)

    # 1. primary = the initial ledger route
    channel = contact.get("channel")
    destination = contact.get("destination")
    if isinstance(destination, str) and destination.strip():
        primary_notes = []
        if channel == "shared-whatsapp":
            primary_notes.append("shared route")
        if channel in ("whatsapp", "shared-whatsapp"):
            primary = ("whatsapp", normalize_phone(destination))
        elif channel == "email":
            primary = ("email", extract_email(destination))
        elif channel == "form":
            primary = ("form", destination.strip() if is_http_url(destination) else None)
        else:
            hit = classify_value(destination)
            primary = (hit[0], hit[1]) if hit else (None, None)
        if primary[1]:
            finish(primary[0], primary[1], label="initial enquiry route", source_url=contact.get("source"),
                   primary=True, notes=primary_notes)
        else:
            report.warn("primary-route-unreadable", "ledger channel/destination could not be classified", key)
    # 2. alternate_details (structured, with channel and evidence)
    for detail in as_list(contact.get("alternate_details")):
        if not isinstance(detail, dict) or not isinstance(detail.get("value"), str):
            continue
        hit = classify_value(detail["value"])
        if not hit:
            report.exclude("unclassifiable alternate value", key, field="alternate_details")
            continue
        detail_channel = detail.get("channel") if detail.get("channel") in CHANNELS else hit[0]
        if detail_channel in ("phone", "whatsapp"):
            destination = normalize_phone(detail["value"])
        else:
            destination = hit[1]
        verification = detail.get("verification") if isinstance(detail.get("verification"), str) else ""
        finish(detail_channel, destination, label=hit[2], source_url=detail.get("source"),
               notes=[clip(verification, 300)] if verification else [])
    # 3. routes the supplier itself gave us later
    for route in as_list(contact.get("new_contact_routes")):
        if not isinstance(route, dict) or not isinstance(route.get("value"), str):
            continue
        hit = classify_value(route["value"])
        if not hit:
            report.exclude("unclassifiable alternate value", key, field="new_contact_routes")
            continue
        purpose = route.get("purpose") if isinstance(route.get("purpose"), str) else None
        finish(hit[0], hit[1], label=clip(purpose, MAX_SHORT) if purpose else None,
               source_url=route.get("source"))
    # 4. saved WhatsApp chats
    for book_key in ("address_book", "alternate_address_book"):
        book = as_dict(contact.get(book_key))
        if "whatsapp" in str(book.get("application") or "").lower():
            phone = normalize_phone(book.get("phone"))
            finish("whatsapp", phone, label="saved WhatsApp contact")
    # 5. plain alternates
    for value in as_list(contact.get("alternates")):
        if not isinstance(value, str) or not value.strip():
            continue
        hit = classify_value(value)
        if not hit:
            report.exclude("unclassifiable alternate value", key, field="alternates")
            continue
        channel, destination, label, platform = hit
        notes = ["Marketplace-routed call number, not a direct line."] if platform else []
        finish(channel, destination, label=label, notes=notes)
    for item in as_list(contact.get("excluded_routes")):
        report.exclude("ledger excluded_routes entry (do not contact)", key,
                       value=as_dict(item).get("value") if isinstance(item, dict) else str(item))
    for item in as_list(contact.get("unverified_contacts")):
        report.exclude("ledger unverified_contacts entry (identity not corroborated)", key,
                       value=as_dict(item).get("value") if isinstance(item, dict) else str(item))

    out = []
    for route in routes.routes.values():
        notes = " ".join(route.pop("_notes"))
        route["notes"] = clip(notes, MAX_NOTES) if notes else ""
        if route["label"]:
            route["label"] = clip(route["label"], MAX_SHORT)
        out.append(route)
    primaries = [r for r in out if r["isPrimary"]]
    for extra in primaries[1:]:
        extra["isPrimary"] = False
    return out


def shared_destinations(contacts: list[dict]) -> dict[tuple[str, str], list[str]]:
    """Destination -> supplier keys, for routes that serve more than one supplier."""
    seen: dict[tuple[str, str], set[str]] = {}
    for contact in contacts:
        values = [contact.get("destination")] + as_list(contact.get("alternates"))
        for value in values:
            if not isinstance(value, str) or PLATFORM_RE.match(value):
                continue
            hit = classify_value(value)
            if hit and hit[1]:
                seen.setdefault(route_dedupe_key("any", hit[1]), set()).add(str(contact.get("id")))
    return {k: sorted(v) for k, v in seen.items() if len(v) > 1}


# --------------------------------------------------------------------------- website / city

def website_from_source(url) -> str | None:
    if not is_http_url(url) or len(url) > MAX_URL:
        return None
    url = url.strip()
    if urlparse(url).path.lower().endswith(DOCUMENT_SUFFIXES):
        return None
    if host_in(host_of(url), NOT_A_SITE_HOSTS):
        return None
    return url


def _name_parts(name: str) -> set[str]:
    parts = set()
    for part in re.split(r"/", name):
        cleaned = re.sub(r"[^a-z0-9 ]+", "", part.lower().replace("&", " and "))
        cleaned = re.sub(r"\s+", " ", cleaned).strip()
        if cleaned:
            parts.add(cleaned)
    return parts


def contact_identifiers(contact: dict) -> set[str]:
    ids = set()
    values = [contact.get("destination")] + as_list(contact.get("alternates"))
    values += [d.get("value") for d in as_list(contact.get("alternate_details")) if isinstance(d, dict)]
    values += [r.get("value") for r in as_list(contact.get("new_contact_routes")) if isinstance(r, dict)]
    values.append(as_dict(contact.get("address_book")).get("phone"))
    for value in values:
        if not isinstance(value, str):
            continue
        email_address = extract_email(value)
        if email_address:
            ids.add(email_address)
        elif looks_like_phone(value) or PLATFORM_RE.match(value):
            phone = normalize_phone(value)
            if phone:
                ids.add(phone)
    return ids


TS_STRING = r"'((?:[^'\\]|\\.)*)'"
VIEWER_ENTRY_RE = re.compile(r"\{\s*id:\s*" + TS_STRING + r",\s*name:\s*" + TS_STRING + r",\s*city:\s*" + TS_STRING)
VIEWER_VALUE_RE = re.compile(r"value:\s*" + TS_STRING)


def load_viewer_cities(viewer_dir: Path | None, contacts: list[dict], report: Report):
    """Map ledger ids to cities from the old viewer data, matching viewer entries by
    shared phone/email identifiers plus name overlap. Returns (cities, crosswalk)."""
    if viewer_dir is None:
        return {}, {}
    if not viewer_dir.is_dir():
        report.warn("viewer-data-missing", f"viewer data folder not found: {viewer_dir}")
        return {}, {}
    entries = []
    for ts_file in sorted(viewer_dir.glob("*.ts")):
        text = ts_file.read_text(encoding="utf-8", errors="replace")
        matches = list(VIEWER_ENTRY_RE.finditer(text))
        for i, m in enumerate(matches):
            block = text[m.start(): matches[i + 1].start() if i + 1 < len(matches) else len(text)]
            ids = set()
            for (value,) in (v.groups() for v in VIEWER_VALUE_RE.finditer(block)):
                email_address = extract_email(value)
                phone = None if email_address else (normalize_phone(value) if looks_like_phone(value) else None)
                if email_address or phone:
                    ids.add(email_address or phone)
            entries.append({"id": m.group(1), "name": m.group(2), "city": m.group(3).strip(), "ids": ids})
    ledger_ids = {str(c.get("id")): contact_identifiers(c) for c in contacts}
    ledger_names = {str(c.get("id")): _name_parts(str(c.get("name") or "")) for c in contacts}
    best: dict[str, tuple[int, str]] = {}
    for entry in entries:
        scores = []
        for key in ledger_ids:
            score = 2 * len(entry["ids"] & ledger_ids[key]) + (1 if _name_parts(entry["name"]) & ledger_names[key] else 0)
            if score:
                scores.append((score, key))
        if not scores:
            report.note("viewer-unmatched", f"viewer entry {entry['id']} matched no ledger contact")
            continue
        scores.sort(reverse=True)
        if len(scores) > 1 and scores[0][0] == scores[1][0]:
            report.warn("viewer-ambiguous", f"viewer entry {entry['id']} matches several ledger contacts; city skipped")
            continue
        score, key = scores[0]
        previous = best.get(key)
        if previous and previous[0] >= score:
            report.warn("viewer-duplicate", f"viewer entry {entry['id']} also matched {key}; kept the stronger match")
            continue
        best[key] = (score, entry["id"])
    by_id = {e["id"]: e for e in entries}
    cities, crosswalk = {}, {}
    for key, (_score, viewer_id) in best.items():
        city = by_id[viewer_id]["city"]
        crosswalk[viewer_id] = key
        if city and len(city) <= MAX_SHORT:
            cities[key] = city
    return cities, crosswalk


# --------------------------------------------------------------------------- media probing

def sniff_content_type(head: bytes) -> str | None:
    if head.startswith(b"\x89PNG\r\n\x1a\n"):
        return "image/png"
    if head.startswith(b"\xff\xd8\xff"):
        return "image/jpeg"
    if head[:4] == b"RIFF" and head[8:12] == b"WEBP":
        return "image/webp"
    if head.startswith(b"%PDF-"):
        return "application/pdf"
    if head[4:8] == b"ftyp":
        return "video/mp4"
    return None


def image_size(data: bytes, content_type: str) -> tuple[int, int] | None:
    try:
        if content_type == "image/png":
            if data[12:16] != b"IHDR":
                return None
            return struct.unpack(">II", data[16:24])
        if content_type == "image/jpeg":
            i = 2
            while i + 4 <= len(data):
                if data[i] != 0xFF:
                    i += 1
                    continue
                marker = data[i + 1]
                if marker == 0xFF:
                    i += 1
                    continue
                if marker in (0xD8, 0x01) or 0xD0 <= marker <= 0xD7:
                    i += 2
                    continue
                length = struct.unpack(">H", data[i + 2:i + 4])[0]
                if marker in (0xC0, 0xC1, 0xC2, 0xC3, 0xC5, 0xC6, 0xC7, 0xC9, 0xCA, 0xCB, 0xCD, 0xCE, 0xCF):
                    height, width = struct.unpack(">HH", data[i + 5:i + 9])
                    return width, height
                if marker == 0xDA:
                    return None
                i += 2 + length
            return None
        if content_type == "image/webp":
            chunk = data[12:16]
            if chunk == b"VP8X":
                width = 1 + int.from_bytes(data[24:27], "little")
                height = 1 + int.from_bytes(data[27:30], "little")
                return width, height
            if chunk == b"VP8 ":
                width, height = struct.unpack("<HH", data[26:30])
                return width & 0x3FFF, height & 0x3FFF
            if chunk == b"VP8L":
                bits = int.from_bytes(data[21:25], "little")
                return (bits & 0x3FFF) + 1, ((bits >> 14) & 0x3FFF) + 1
    except (struct.error, IndexError):
        return None
    return None


def hash_file(path: Path) -> tuple[str, int, bytes]:
    digest = hashlib.sha256()
    size = 0
    head = b""
    with path.open("rb") as handle:
        while chunk := handle.read(1024 * 1024):
            if not head:
                head = chunk[:64]
            digest.update(chunk)
            size += len(chunk)
    return digest.hexdigest(), size, head


# --------------------------------------------------------------------------- media collection

class EmailIndex:
    """message id -> (received time, subject), from supplier email/*/message.json files."""

    def __init__(self, supplier_dir: Path, report: Report, supplier: str) -> None:
        self.by_id: dict[str, dict] = {}
        self.by_dir: dict[Path, str] = {}
        for message_file in sorted(supplier_dir.rglob("message.json")):
            try:
                data = json.loads(message_file.read_text(encoding="utf-8"))
            except (OSError, ValueError):
                report.warn("email-metadata-unreadable", "message.json could not be parsed", supplier,
                            path=str(message_file))
                continue
            if not isinstance(data, dict):
                continue
            headers = {}
            for header in as_list(as_dict(data.get("payload")).get("headers")):
                if isinstance(header, dict) and isinstance(header.get("name"), str):
                    headers.setdefault(header["name"].lower(), header.get("value"))
            message_id = data.get("gmail_message_id") or data.get("id") or message_file.parent.name
            received = parse_email_date(data.get("date_header")) or parse_email_date(headers.get("date"))
            if received is None and str(data.get("internal_date") or "").isdigit():
                received = dt.datetime.fromtimestamp(int(data["internal_date"]) / 1000, dt.timezone.utc)
            subject = data.get("subject") if isinstance(data.get("subject"), str) else headers.get("subject")
            self.by_id[str(message_id)] = {"received": received, "subject": subject}
            self.by_dir[message_file.parent.resolve()] = str(message_id)

    def message_for_dir(self, directory: Path) -> str | None:
        directory = directory.resolve()
        for candidate in [directory, *directory.parents]:
            if candidate in self.by_dir:
                return self.by_dir[candidate]
        return None


class MediaCollector:
    def __init__(self, research: Path, responses: Path, report: Report, reference_shas: set[str]):
        self.research = research.resolve()
        self.responses = responses.resolve()
        self.report = report
        self.reference_shas = reference_shas
        self.items: list[dict] = []
        self.by_sha: dict[str, dict] = {}
        self.claimed: set[Path] = set()
        self.bases: Counter = Counter()
        self.duplicates_within_supplier: Counter = Counter()
        self.relisted: Counter = Counter()
        self.manifests: list[dict] = []

    # path-base normalization: manifests mix absolute paths and paths relative to the
    # manifest folder, the supplier folder, responses/, the research folder or a repo root
    def resolve(self, raw, manifest_dir: Path, supplier_dir: Path) -> tuple[Path | None, str | None]:
        if not isinstance(raw, str) or not raw.strip():
            return None, None
        candidate = Path(raw.strip())
        if candidate.is_absolute():
            if candidate.is_file():
                return candidate.resolve(), "absolute"
            parts = candidate.parts
            if "responses" in parts:
                index = len(parts) - 1 - parts[::-1].index("responses")
                rerooted = self.responses.joinpath(*parts[index + 1:])
                if rerooted.is_file():
                    return rerooted.resolve(), "absolute-rerooted"
            return None, None
        bases = [("manifest-dir", manifest_dir), ("supplier-dir", supplier_dir),
                 ("responses", self.responses), ("research", self.research)]
        bases += [("ancestor", parent) for parent in self.research.parents]
        for label, base in bases:
            path = base / candidate
            if path.is_file():
                return path.resolve(), label
        return None, None

    def claim_excluded(self, path: Path | None, supplier: str, reason: str) -> None:
        if path is None or path in self.claimed:
            return
        self.claimed.add(path)
        self.report.exclude(reason, supplier, path=str(path))

    def add(self, supplier: str, raw_path, manifest_dir: Path, supplier_dir: Path, *, capture: str,
            source_label: str | None, received_at: dt.datetime | None, message_id=None, caption: str = "",
            declared_sha=None, declared_bytes=None) -> None:
        path, base = self.resolve(raw_path, manifest_dir, supplier_dir)
        if path is None:
            self.report.exclude("file listed in manifest not found", supplier, path=str(raw_path))
            return
        if path in self.claimed:
            self.relisted[supplier] += 1  # same file listed by another manifest (e.g. batch copies)
            return
        self.claimed.add(path)
        self.bases[base] += 1
        if not path.is_relative_to(self.research):
            self.report.exclude("resolved outside the research folder", supplier, path=str(path))
            return
        if not path.is_relative_to(supplier_dir.resolve()):
            self.report.warn("media-outside-supplier-folder", "manifest file lives in another folder",
                             supplier, path=str(path))
        sha, size, head = hash_file(path)
        content_type = sniff_content_type(head)
        if content_type is None:
            self.report.exclude("unsupported content type", supplier, path=str(path))
            return
        if size > SIZE_LIMITS[content_type]:
            self.report.exclude(f"over the {SIZE_LIMITS[content_type] // MIB} MiB limit for {content_type}",
                                supplier, path=str(path), bytes=size)
            return
        if content_type.startswith("image/"):
            dims = image_size(path.read_bytes(), content_type)
            if dims is None:
                self.report.exclude("image header unreadable", supplier, path=str(path))
                return
            width, height = dims
            if max(width, height) > MAX_IMAGE_SIDE or width * height > MAX_IMAGE_PIXELS:
                self.report.exclude("image dimensions over the probe limit", supplier, path=str(path),
                                    width=width, height=height)
                return
        if isinstance(declared_sha, str) and declared_sha.strip() and declared_sha.strip().lower() != sha:
            self.report.warn("sha256-mismatch", "manifest sha256 differs from the file bytes; computed hash used",
                             supplier, path=str(path), manifest=declared_sha.strip().lower(), computed=sha)
        if is_number(declared_bytes) and declared_bytes != size:
            self.report.warn("bytes-mismatch", "manifest byte count differs from the file", supplier,
                             path=str(path), manifest=declared_bytes, computed=size)
        if sha in self.reference_shas:
            self.report.exclude("same bytes as our own outreach reference/proof asset", supplier, path=str(path))
            return
        if sha in self.by_sha:
            first = self.by_sha[sha]
            if first["supplierKey"] == supplier:
                self.duplicates_within_supplier[supplier] += 1
                self.report.note("duplicate-bytes-within-supplier", "same bytes as an earlier file; kept the first",
                                 supplier, path=str(path), keptPath=first["path"])
            else:
                self.report.warn("cross-supplier-duplicate", f"same bytes already imported for {first['supplierKey']}",
                                 supplier, path=str(path), sha256=sha)
                self.report.exclude("duplicate of media already assigned to another supplier", supplier,
                                    path=str(path), keptFor=first["supplierKey"])
            return
        name = path.name if len(path.name) <= MAX_ORIGINAL_NAME else path.name[-MAX_ORIGINAL_NAME:]
        item = {
            "supplierKey": supplier,
            "path": str(path),
            "sha256": sha,
            "capture": capture,
            "originalName": name,
            "caption": clip(caption, MAX_CAPTION) if caption else "",
            "sourceLabel": clip(source_label, MAX_SOURCE_LABEL) if source_label else None,
            "sourceMessageId": str(message_id) if message_id and len(str(message_id)) <= MAX_MESSAGE_ID else None,
            "receivedAt": iso_utc(received_at),
            "_contentType": content_type,
            "_bytes": size,
        }
        self.by_sha[sha] = item
        self.items.append(item)


def local_date(value: dt.datetime | None) -> str | None:
    return value.astimezone(TASHKENT).date().isoformat() if value else None


def parse_manifest(collector: MediaCollector, supplier: str, supplier_dir: Path, manifest_path: Path,
                   data, emails: EmailIndex) -> str:
    """Dispatch on manifest shape. Returns the recognised schema name or 'ignored'."""
    report = collector.report
    mdir = manifest_path.parent
    if not isinstance(data, dict):
        return "ignored"

    # WhatsApp v1: photos[{index, file, sha256, bytes, displayed_message_time_tashkent, viewer}]
    photos = [p for p in as_list(data.get("photos")) if isinstance(p, dict) and p.get("file")]
    if photos:
        captured = parse_iso(data.get("captured_at"))
        for photo in photos:
            viewer = f"{photo.get('viewer') or ''}"
            received = resolve_relative_time(viewer, captured)
            shown = RELATIVE_TIME_RE.search(viewer)
            if received:
                label = f"WhatsApp screenshot {local_date(received)}"
            else:
                label = f"WhatsApp screenshot, viewer time '{shown.group(0) if shown else photo.get('displayed_message_time_tashkent') or 'unknown'}'"
            collector.add(supplier, photo["file"], mdir, supplier_dir, capture="screenshot", source_label=label,
                          received_at=received, declared_sha=photo.get("sha256"), declared_bytes=photo.get("bytes"))
        return "whatsapp-v1"

    items = [i for i in as_list(data.get("items")) if isinstance(i, dict)]
    # Gallery: items[{sequence_newest_first, sender, time, file, sha256 | skip_reason}]
    if items and any("sequence_newest_first" in i for i in items):
        observed_on = None
        try:
            observed_on = dt.date.fromisoformat(str(data.get("observed_on")))
        except ValueError:
            pass
        for item in items:
            if item.get("skip_reason") or str(item.get("sender") or "").strip().lower() == "you":
                report.exclude("our outgoing reference in a gallery (skip_reason / sent by us)", supplier,
                               manifest=str(manifest_path), detail=str(item.get("skip_reason") or "sender You"))
                continue
            received = resolve_relative_time(item.get("time"), None, observed_on)
            if received:
                label = f"WhatsApp gallery screenshot {local_date(received)}"
            else:
                label = f"WhatsApp gallery screenshot, viewer time '{item.get('time') or 'unknown'}'"
                if observed_on:
                    label += f", observed {observed_on.isoformat()}"
            collector.add(supplier, item.get("file"), mdir, supplier_dir, capture="screenshot", source_label=label,
                          received_at=received, declared_sha=item.get("sha256"))
        return "gallery"

    # Albums: items[{album, position, path, sha256, source_display_time}], albums[{received_at_*}]
    if items and any("album" in i and "position" in i for i in items):
        albums = {a.get("album"): a for a in as_list(data.get("albums")) if isinstance(a, dict)}
        for item in items:
            received = parse_iso(item.get("received_at")) or parse_local_stamp(item.get("source_display_time"))
            album = albums.get(item.get("album"), {})
            if received is None and album.get("received_at_start") and album.get("received_at_start") == album.get("received_at_end"):
                received = parse_iso(album.get("received_at_start"))
            label = f"WhatsApp screenshot {local_date(received) or 'date unknown'}, album {item.get('album')} photo {item.get('position')}"
            collector.add(supplier, item.get("path") or item.get("file"), mdir, supplier_dir, capture="screenshot",
                          source_label=label, received_at=received, message_id=item.get("source_message_id"),
                          declared_sha=item.get("sha256"), declared_bytes=item.get("bytes"))
        proof = as_dict(data.get("conversation_proof"))
        if proof.get("path"):
            path, _ = collector.resolve(proof["path"], mdir, supplier_dir)
            collector.claim_excluded(path, supplier, "conversation proof screenshot (manifest marks it as proof)")
        return "albums"

    # Single preview: {message_id, received_at, preview{path, sha256}}
    preview = as_dict(data.get("preview"))
    if preview.get("path"):
        received = parse_iso(data.get("received_at"))
        collector.add(supplier, preview["path"], mdir, supplier_dir, capture="screenshot",
                      source_label=f"WhatsApp screenshot {local_date(received) or 'date unknown'}",
                      received_at=received, message_id=data.get("message_id"),
                      declared_sha=preview.get("sha256"), declared_bytes=preview.get("bytes"))
        if isinstance(data.get("conversation_screenshot"), str):
            path, _ = collector.resolve(data["conversation_screenshot"], mdir, supplier_dir)
            collector.claim_excluded(path, supplier, "conversation proof screenshot (manifest marks it as proof)")
        return "single-preview"

    # Email originals, attachments and PDF-derived images: files[] | attachments[] | images[]
    entries = []
    for list_key in ("files", "attachments", "images"):
        entries += [e for e in as_list(data.get(list_key)) if isinstance(e, dict)]
    entries = [e for e in entries if e.get("local_path") or e.get("path") or e.get("file")]
    if entries:
        manifest_pdf = data.get("original_pdf") if isinstance(data.get("original_pdf"), str) else None
        dir_message = emails.message_for_dir(mdir)
        schema = "email-files"
        for entry in entries:
            source_pdf = entry.get("source_pdf") if isinstance(entry.get("source_pdf"), str) else manifest_pdf
            derived_hint = f"{entry.get('format') or ''} {entry.get('source') or ''}".lower()
            derived = bool(source_pdf) or "extract" in derived_hint or "derived" in derived_hint
            message_id = entry.get("message_id") or data.get("message_id") or data.get("source_message_id") or dir_message
            meta = emails.by_id.get(str(message_id), {}) if message_id else {}
            received = meta.get("received")
            subject = meta.get("subject")
            raw = entry.get("local_path") or entry.get("path") or entry.get("file")
            if derived:
                schema = "pdf-derived"
                pages = [str(p) for p in as_list(entry.get("pages")) if is_number(p)]
                pdf_name = Path(source_pdf).name if source_pdf else "supplier PDF"
                label = f"Derived from PDF {pdf_name}"
                caption = f"PDF page {', '.join(pages)}" if pages else ""
                collector.add(supplier, raw, mdir, supplier_dir, capture="derived", source_label=label,
                              received_at=received, message_id=message_id, caption=caption,
                              declared_sha=entry.get("sha256"), declared_bytes=entry.get("bytes"))
            else:
                kind = "Email attachment original" if str(entry.get("filename", raw)).lower().endswith(".pdf") \
                    else "Email original"
                label = f"{kind} {local_date(received) or ''}".strip()
                if entry.get("file_id") or is_http_url(entry.get("url")):
                    label += " (linked file)"
                caption = f"Email subject: {subject}" if subject else ""
                collector.add(supplier, raw, mdir, supplier_dir, capture="original", source_label=label,
                              received_at=received, message_id=message_id, caption=caption,
                              declared_sha=entry.get("sha256"), declared_bytes=entry.get("bytes"))
        return schema
    return "ignored"


def classify_unclaimed(rel: str) -> str:
    lowered = rel.lower()
    if "/renders/" in f"/{lowered}":
        return "PDF page render or review sheet (our derivative, not supplier-shared)"
    if "approved-reply" in lowered:
        return "approved-reply proof screenshot (our message)"
    if re.search(r"(conversation|question|follow-up|-response-|chat|contact-request|prefixed)", lowered):
        return "conversation proof screenshot"
    return "not listed in any supplier-media manifest"


def collect_reference_shas(outreach_dir: Path, skip: list[Path]) -> set[str]:
    """Hashes of every media file in the outreach folder outside responses/ (our
    A-E references, send packs, proofs); supplier media never matches these."""
    shas = set()
    if not outreach_dir.is_dir():
        return shas
    skip = [s.resolve() for s in skip]
    for path in sorted(outreach_dir.rglob("*")):
        resolved = path.resolve()
        if any(resolved.is_relative_to(s) for s in skip):
            continue
        if path.is_file() and path.suffix.lower() in MEDIA_EXTENSIONS:
            shas.add(hash_file(path)[0])
    return shas


def collect_media(supplier_keys: list[str], responses: Path, research: Path, out_dir: Path,
                  report: Report) -> MediaCollector:
    outreach = responses.parent
    reference_shas = collect_reference_shas(outreach, [responses, out_dir])
    collector = MediaCollector(research, responses, report, reference_shas)
    for key in supplier_keys:  # sorted: the first supplier wins a cross-supplier duplicate
        supplier_dir = responses / key
        if not supplier_dir.is_dir():
            continue
        emails = EmailIndex(supplier_dir, report, key)
        copy = supplier_dir / "och-supplier-contact.json"
        if copy.is_file():
            try:
                copied = json.loads(copy.read_text(encoding="utf-8"))
                if isinstance(copied, dict) and copied.get("id") not in (None, key):
                    report.warn("supplier-copy-mismatch", "och-supplier-contact.json names another id", key)
            except ValueError:
                report.warn("supplier-copy-unreadable", "och-supplier-contact.json could not be parsed", key)
        for manifest_path in sorted(supplier_dir.rglob("*.json")):
            if not MANIFEST_NAME_RE.search(manifest_path.name):
                continue
            try:
                data = json.loads(manifest_path.read_text(encoding="utf-8"))
            except ValueError:
                report.warn("manifest-unreadable", "manifest could not be parsed", key, path=str(manifest_path))
                continue
            schema = parse_manifest(collector, key, supplier_dir, manifest_path, data, emails)
            collector.manifests.append({"supplier": key, "path": str(manifest_path), "schema": schema})
        for path in sorted(supplier_dir.rglob("*")):
            if not path.is_file() or path.suffix.lower() not in MEDIA_EXTENSIONS:
                continue
            resolved = path.resolve()
            if resolved in collector.claimed:
                continue
            rel = path.relative_to(supplier_dir).as_posix()
            if RECEIVED_FILE_RE.search(path.name):
                collector.add(key, str(resolved), supplier_dir, supplier_dir, capture="screenshot",
                              source_label="WhatsApp screenshot (unmanifested received file)", received_at=None)
                report.note("unmanifested-received", "received file found outside any manifest; imported",
                            key, path=str(resolved))
                continue
            collector.claim_excluded(resolved, key, classify_unclaimed(rel))
    return collector


# --------------------------------------------------------------------------- validation

def validate_bundle(bundle: dict) -> list[str]:
    """Contract checks for ocharo.sourcing-import.v1. Returns a list of problems."""
    errors: list[str] = []

    def text(value, field, low, high, nullable=False):
        if value is None:
            if not nullable:
                errors.append(f"{field}: required")
            return
        if not isinstance(value, str) or not low <= len(value) <= high:
            errors.append(f"{field}: must be a string of {low}-{high} characters")

    if bundle.get("format") != FORMAT:
        errors.append("format: must be " + FORMAT)
    if not isinstance(bundle.get("source"), str) or not bundle["source"].strip():
        errors.append("source: required")
    suppliers = bundle.get("suppliers")
    if not isinstance(suppliers, list):
        return errors + ["suppliers: must be a list"]
    keys = set()
    for s in suppliers:
        sid = f"supplier[{s.get('key')}]"
        key = s.get("key")
        if not (isinstance(key, str) and KEY_RE.fullmatch(key)):
            errors.append(f"{sid}.key: must be 1-64 lower-case letters, digits and hyphens")
        if key in keys:
            errors.append(f"{sid}.key: duplicate")
        keys.add(key)
        text(s.get("name"), f"{sid}.name", 1, MAX_NAME)
        if s.get("stage") not in STAGES or s.get("stage") == "qualified":
            errors.append(f"{sid}.stage: invalid or not importable")
        country = s.get("country")
        if country is not None and not (isinstance(country, str) and re.fullmatch(r"[A-Z]{2}", country)):
            errors.append(f"{sid}.country: must be ISO 3166-1 alpha-2 upper case")
        text(s.get("city"), f"{sid}.city", 1, MAX_SHORT, nullable=True)
        website = s.get("website")
        if website is not None and not (is_http_url(website) and len(website) <= MAX_URL):
            errors.append(f"{sid}.website: must be an http(s) URL up to {MAX_URL}")
        text(s.get("notes"), f"{sid}.notes", 0, MAX_SUPPLIER_NOTES)
        person_keys = set()
        names = set()
        for p in as_list(s.get("people")):
            pid = f"{sid}.person[{p.get('key')}]"
            if not (isinstance(p.get("key"), str) and KEY_RE.fullmatch(p["key"])):
                errors.append(f"{pid}.key: must be 1-64 lower-case letters, digits and hyphens")
            if p.get("key") in person_keys:
                errors.append(f"{pid}.key: duplicate")
            person_keys.add(p.get("key"))
            text(p.get("name"), f"{pid}.name", 1, MAX_NAME)
            folded = str(p.get("name")).casefold()
            if folded in names:
                errors.append(f"{pid}.name: duplicate (case-insensitive)")
            names.add(folded)
            text(p.get("role"), f"{pid}.role", 1, MAX_SHORT, nullable=True)
            text(p.get("notes"), f"{pid}.notes", 0, MAX_NOTES)
        route_keys = set()
        primaries = 0
        for r in as_list(s.get("routes")):
            rid = f"{sid}.route[{r.get('channel')}]"
            if r.get("channel") not in CHANNELS:
                errors.append(f"{rid}.channel: invalid")
            text(r.get("destination"), f"{rid}.destination", 1, MAX_DESTINATION)
            pair = (r.get("channel"), str(r.get("destination")).lower())
            if pair in route_keys:
                errors.append(f"{rid}: duplicate (channel, destination)")
            route_keys.add(pair)
            if r.get("channel") in ("phone", "whatsapp") and not re.fullmatch(r"\+\d{8,15}", str(r.get("destination"))):
                errors.append(f"{rid}.destination: phone must be +<digits>")
            text(r.get("label"), f"{rid}.label", 1, MAX_SHORT, nullable=True)
            source_url = r.get("sourceUrl")
            if source_url is not None and not (is_http_url(source_url) and len(source_url) <= MAX_URL):
                errors.append(f"{rid}.sourceUrl: must be an http(s) URL up to {MAX_URL}")
            if not isinstance(r.get("verified"), bool) or not isinstance(r.get("isPrimary"), bool):
                errors.append(f"{rid}: verified and isPrimary must be booleans")
            primaries += 1 if r.get("isPrimary") is True else 0
            if r.get("personKey") is not None and r.get("personKey") not in person_keys:
                errors.append(f"{rid}.personKey: unknown person")
            text(r.get("notes"), f"{rid}.notes", 0, MAX_NOTES)
        if primaries > 1:
            errors.append(f"{sid}: more than one primary route")
    shas = set()
    for m in as_list(bundle.get("media")):
        mid = f"media[{m.get('originalName')}]"
        if m.get("supplierKey") not in keys:
            errors.append(f"{mid}.supplierKey: unknown supplier")
        if not isinstance(m.get("path"), str) or not Path(m["path"]).is_absolute():
            errors.append(f"{mid}.path: must be absolute")
        sha = m.get("sha256")
        if not (isinstance(sha, str) and SHA256_RE.fullmatch(sha)):
            errors.append(f"{mid}.sha256: must be 64 lower-case hex characters")
        if sha in shas:
            errors.append(f"{mid}.sha256: duplicate")
        shas.add(sha)
        if m.get("capture") not in CAPTURES:
            errors.append(f"{mid}.capture: invalid")
        text(m.get("originalName"), f"{mid}.originalName", 1, MAX_ORIGINAL_NAME)
        text(m.get("caption"), f"{mid}.caption", 0, MAX_CAPTION)
        text(m.get("sourceLabel"), f"{mid}.sourceLabel", 1, MAX_SOURCE_LABEL, nullable=True)
        text(m.get("sourceMessageId"), f"{mid}.sourceMessageId", 1, MAX_MESSAGE_ID, nullable=True)
        received = m.get("receivedAt")
        if received is not None and not (isinstance(received, str) and ISO_UTC_RE.fullmatch(received)):
            errors.append(f"{mid}.receivedAt: must be ISO-8601 UTC")
    return errors


# --------------------------------------------------------------------------- build

def build(research: Path, out_dir: Path, ledger_name: str = DEFAULT_LEDGER, viewer_dir: Path | None = None,
          country: str = "IN") -> tuple[dict, dict, list[str]]:
    research = research.resolve()
    out_dir = out_dir.resolve()
    ledger_path = research / ledger_name
    responses = research / "assets" / Path(ledger_name).stem / "responses"
    if not ledger_path.is_file():
        raise ValueError(f"ledger not found: {ledger_path}")
    if out_dir in (research, responses.resolve()) or out_dir.is_relative_to(responses.resolve()):
        raise ValueError("--out must not be the research folder or inside responses/")
    out_dir.mkdir(parents=True, exist_ok=True)
    report = Report()
    ledger, ledger_sha, taken_at, snapshot_path = snapshot_ledger(ledger_path, out_dir)
    snapshot_date = taken_at.astimezone(TASHKENT).date().isoformat()

    contacts = []
    seen_ids = set()
    for contact in ledger["contacts"]:
        if not isinstance(contact, dict) or not isinstance(contact.get("id"), str) or not contact["id"].strip():
            report.exclude("ledger contact without an id")
            continue
        if contact["id"] in seen_ids:
            report.exclude("duplicate ledger contact id", contact["id"])
            continue
        seen_ids.add(contact["id"])
        contacts.append(contact)
    contacts.sort(key=lambda c: c["id"])

    cities, crosswalk = load_viewer_cities(viewer_dir, contacts, report)
    shared = shared_destinations(contacts)
    suppliers, stage_reasons = [], {}
    for contact in contacts:
        key = contact["id"]
        name = str(contact.get("name") or "").strip()
        if not KEY_RE.fullmatch(key):
            report.exclude("ledger id is not a valid import key (1-64 of a-z, 0-9, -)", key)
            continue
        if not 1 <= len(name) <= MAX_NAME:
            report.exclude("supplier name empty or over 160 characters", key)
            continue
        stage, reason = derive_stage(contact)
        stage_reasons[key] = reason
        people = extract_people(contact)
        routes = build_routes(contact, people, shared, report)
        primary_value = contact.get("destination")
        shared_with = []
        if isinstance(primary_value, str):
            hit = classify_value(primary_value)
            if hit and hit[1]:
                shared_with = [s for s in shared.get(route_dedupe_key("any", hit[1]), []) if s != key]
        suppliers.append({
            "key": key,
            "name": name,
            "stage": stage,
            "country": country,
            "city": cities.get(key),
            "website": website_from_source(contact.get("source")),
            "notes": build_supplier_notes(contact, snapshot_date, shared_with),
            "people": [{k: v for k, v in p.items() if not k.startswith("_")} for p in people],
            "routes": routes,
        })
    supplier_keys = [s["key"] for s in suppliers]

    folders = sorted(p.name for p in responses.iterdir() if p.is_dir()) if responses.is_dir() else []
    unmatched = [f for f in folders if f not in seen_ids]
    for folder in unmatched:
        report.exclude("response folder without a ledger contact (not imported)", folder,
                       mediaFiles=sum(1 for p in (responses / folder).rglob("*")
                                      if p.is_file() and p.suffix.lower() in MEDIA_EXTENSIONS))
    collector = collect_media(supplier_keys, responses, research, out_dir, report)
    media = sorted(collector.items, key=lambda m: (m["supplierKey"], m["receivedAt"] is None,
                                                   m["receivedAt"] or "", m["originalName"], m["path"]))
    extension_types = {".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".webp": "image/webp",
                       ".pdf": "application/pdf", ".mp4": "video/mp4"}
    mismatched = Counter(m["supplierKey"] for m in media
                         if extension_types.get(Path(m["originalName"]).suffix.lower()) != m["_contentType"])
    for supplier_key, count in sorted(mismatched.items()):
        report.warn("extension-content-mismatch",
                    f"{count} file(s) whose bytes differ from the name extension (e.g. JPEG named .png); "
                    "Desk must sniff content, not trust the name", supplier_key, count=count)
    source = (f"ledger {ledger_path} sha256 {ledger_sha} snapshot {iso_utc(taken_at)} "
              f"copy {snapshot_path}; media {responses}")
    bundle = {
        "format": FORMAT,
        "source": source,
        "suppliers": suppliers,
        "media": [{k: v for k, v in m.items() if not k.startswith("_")} for m in media],
    }
    errors = validate_bundle(bundle)

    per_supplier = {}
    for supplier in suppliers:
        items = [m for m in media if m["supplierKey"] == supplier["key"]]
        per_supplier[supplier["key"]] = {
            "stage": supplier["stage"],
            "stageReason": stage_reasons.get(supplier["key"]),
            "city": supplier["city"],
            "routes": len(supplier["routes"]),
            "routesByChannel": dict(sorted(Counter(r["channel"] for r in supplier["routes"]).items())),
            "verifiedRoutes": sum(1 for r in supplier["routes"] if r["verified"]),
            "people": [p["name"] for p in supplier["people"]],
            "media": dict(sorted(Counter(m["capture"] for m in items).items())),
            "mediaBytes": sum(m["_bytes"] for m in items),
            "mediaWithReceivedAt": sum(1 for m in items if m["receivedAt"]),
            "duplicatesWithinSupplier": collector.duplicates_within_supplier.get(supplier["key"], 0),
            "manifestRelistings": collector.relisted.get(supplier["key"], 0),
        }
    all_routes = [r for s in suppliers for r in s["routes"]]
    report_doc = {
        "generatedAt": iso_utc(utc_now()),
        "ledger": {"path": str(ledger_path), "sha256": ledger_sha, "snapshotAt": iso_utc(taken_at),
                   "copy": str(snapshot_path)},
        "bundle": str(out_dir / "sourcing-bundle.json"),
        "valid": not errors,
        "validationErrors": errors,
        "totals": {
            "suppliers": len(suppliers),
            "stages": dict(sorted(Counter(s["stage"] for s in suppliers).items())),
            "people": sum(len(s["people"]) for s in suppliers),
            "routes": len(all_routes),
            "routesByChannel": dict(sorted(Counter(r["channel"] for r in all_routes).items())),
            "verifiedRoutes": sum(1 for r in all_routes if r["verified"]),
            "media": len(media),
            "mediaByCapture": dict(sorted(Counter(m["capture"] for m in media).items())),
            "mediaByContentType": dict(sorted(Counter(m["_contentType"] for m in media).items())),
            "mediaBytes": sum(m["_bytes"] for m in media),
            "mediaWithReceivedAt": sum(1 for m in media if m["receivedAt"]),
            "mediaExtensionMismatch": sum(mismatched.values()),
            "mediaDuplicatesWithinSupplier": sum(collector.duplicates_within_supplier.values()),
            "manifestRelistings": sum(collector.relisted.values()),
        },
        "suppliers": per_supplier,
        "pathBases": dict(sorted(collector.bases.items())),
        "manifests": collector.manifests,
        "viewerCrosswalk": dict(sorted(crosswalk.items())),
        "unmatchedFolders": unmatched,
        "warningsByCode": dict(sorted(Counter(w["code"] for w in report.warnings).items())),
        "exclusionsByReason": dict(sorted(Counter(e["reason"] for e in report.exclusions).items())),
        "warnings": report.warnings,
        "exclusions": report.exclusions,
        "notes": report.notes,
    }
    return bundle, report_doc, errors


def write_json(path: Path, data) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Build a Desk Sourcing import bundle from the shawl-sourcing ledger.")
    parser.add_argument("--research", required=True, type=Path, help="private research folder (read-only)")
    parser.add_argument("--out", required=True, type=Path, help="output folder for snapshot, bundle and report")
    parser.add_argument("--ledger", default=DEFAULT_LEDGER, help=f"ledger file name in --research (default {DEFAULT_LEDGER})")
    parser.add_argument("--viewer-data", type=Path, default=None,
                        help="optional folder of old viewer *.ts data; supplies city per supplier")
    parser.add_argument("--country", default="IN", help="ISO 3166-1 alpha-2 country for every supplier (default IN)")
    args = parser.parse_args(argv)
    if not re.fullmatch(r"[A-Z]{2}", args.country):
        parser.error("--country must be ISO 3166-1 alpha-2 upper case")
    try:
        bundle, report_doc, errors = build(args.research, args.out, args.ledger, args.viewer_data, args.country)
    except (OSError, ValueError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2
    out_dir = args.out.resolve()
    write_json(out_dir / "report.json", report_doc)
    if errors:
        print(f"bundle failed validation ({len(errors)} problems); see report.json", file=sys.stderr)
        return 1
    write_json(out_dir / "sourcing-bundle.json", bundle)
    totals = report_doc["totals"]
    print(json.dumps({"suppliers": totals["suppliers"], "people": totals["people"], "routes": totals["routes"],
                      "media": totals["media"], "mediaByCapture": totals["mediaByCapture"],
                      "warnings": len(report_doc["warnings"]), "exclusions": len(report_doc["exclusions"]),
                      "out": str(out_dir)}, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
