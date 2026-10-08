"""Validate an ocharo.events-import.v1 bundle against events-contract.md and EventValidationService rules."""
import collections
import datetime as dt
import json
import re
import sys
import urllib.parse

TODAY = dt.date.today()
KEY = re.compile(r"^[a-z0-9][a-z0-9-]{0,63}$")
COUNTRY = re.compile(r"^[A-Z]{2}$")
ENUMS = {
    "scope": {"local", "regional", "international"},
    "format": {"runway", "trade-fair", "market", "exhibition", "competition", "festival", "conference", "other"},
    "status": {"candidate", "shortlisted", "applying", "applied", "confirmed", "attended", "skipped", "rejected"},
    "datePrecision": {"day", "month", "estimated", "unknown"},
    "priority": {"high", "normal", "low"},
}
FIELDS = ["key", "title", "organizer", "scope", "format", "country", "city", "venue", "startsOn", "endsOn",
          "datePrecision", "applicationDeadline", "participation", "cost", "website", "sourceUrl", "status",
          "priority", "notes"]
SINGLE_LINE = {"title": 200, "organizer": 160, "city": 120, "venue": 200}
MULTI_LINE = {"participation": 2000, "cost": 500, "notes": 1500}  # seed and scan bundles keep notes short; the contract allows 8000
REGIONAL = {"KZ", "KG", "TJ", "TM", "AZ", "RU", "AF"}


def u16(text):
    return len(text.encode("utf-16-le")) // 2


def date(value, errors, where):
    if value is None:
        return None
    if not isinstance(value, str) or not re.fullmatch(r"\d{4}-\d{2}-\d{2}", value):
        errors.append(f"{where}: not ISO date {value!r}")
        return None
    try:
        d = dt.date.fromisoformat(value)
    except ValueError:
        errors.append(f"{where}: invalid date {value!r}")
        return None
    if not dt.date(2000, 1, 1) <= d <= dt.date(2100, 12, 31):
        errors.append(f"{where}: out of range {value}")
    return d


def main(path):
    raw = open(path, encoding="utf-8").read()
    bundle = json.loads(raw)
    errors, warnings = [], []
    if set(bundle) != {"format", "source", "events"}:
        errors.append(f"top-level keys {sorted(bundle)}")
    if bundle.get("format") != "ocharo.events-import.v1":
        errors.append("format mismatch")
    if not isinstance(bundle.get("source"), str) or not bundle["source"].strip():
        errors.append("source must be a non-empty string")
    keys = collections.Counter()
    for i, e in enumerate(bundle["events"]):
        w = f"events[{i}] {e.get('key')}"
        if set(e) != set(FIELDS):
            errors.append(f"{w}: fields {sorted(set(e) ^ set(FIELDS))}")
        for k, v in e.items():
            if v is not None and not isinstance(v, str):
                errors.append(f"{w}: {k} is not a string or null")
            if isinstance(v, str) and v != v.strip():
                errors.append(f"{w}: {k} has surrounding whitespace")
            if isinstance(v, str) and v.strip() == "" and k not in ("participation", "notes"):
                errors.append(f"{w}: {k} blank — use null")
        key = e.get("key")
        if not isinstance(key, str) or not KEY.match(key) or key.endswith("\n"):
            errors.append(f"{w}: bad key")
        keys[key] += 1
        for k, allowed in ENUMS.items():
            if e.get(k) not in allowed:
                errors.append(f"{w}: {k}={e.get(k)!r}")
        for k, cap in SINGLE_LINE.items():
            v = e.get(k)
            if k == "title" and not v:
                errors.append(f"{w}: title required")
            if v is not None:
                if u16(v) > cap:
                    errors.append(f"{w}: {k} > {cap}")
                if any(ord(c) < 32 or 127 <= ord(c) < 160 for c in v):
                    errors.append(f"{w}: {k} has control chars")
        for k, cap in MULTI_LINE.items():
            v = e.get(k)
            if v is not None:
                if u16(v) > cap:
                    errors.append(f"{w}: {k} {u16(v)} > {cap}")
                if any((ord(c) < 32 and c not in "\n\r\t") or 127 <= ord(c) < 160 for c in v):
                    errors.append(f"{w}: {k} has control chars")
        if not (e.get("participation") or "").strip():
            errors.append(f"{w}: participation empty")
        if not (e.get("notes") or "").strip():
            errors.append(f"{w}: notes empty")
        elif "Source:" not in e["notes"]:
            errors.append(f"{w}: notes lack 'Source:'")
        if e.get("cost") is not None and re.fullmatch(r"\s*[$€£]?\s*0+([.,]0+)?\s*", e["cost"]):
            errors.append(f"{w}: cost is zero")
        c = e.get("country")
        if c is not None and not COUNTRY.match(c):
            errors.append(f"{w}: country {c!r}")
        # scope mapping rule
        if c == "UZ" and e["scope"] != "local":
            errors.append(f"{w}: UZ must be local")
        if c in REGIONAL and e["scope"] != "regional":
            errors.append(f"{w}: {c} must be regional")
        if c and c != "UZ" and c not in REGIONAL and e["scope"] != "international":
            errors.append(f"{w}: {c} must be international")
        if c is None and e["scope"] != "international":
            warnings.append(f"{w}: no country, scope {e['scope']}")
        # dates
        s = date(e.get("startsOn"), errors, w + " startsOn")
        en = date(e.get("endsOn"), errors, w + " endsOn")
        dl = date(e.get("applicationDeadline"), errors, w + " applicationDeadline")
        p = e.get("datePrecision")
        if en and not s:
            errors.append(f"{w}: endsOn without startsOn")
        if s and en and en < s:
            errors.append(f"{w}: endsOn < startsOn")
        if p == "unknown" and (e.get("startsOn") or e.get("endsOn")):
            errors.append(f"{w}: unknown precision with dates")
        if p in ("day", "month", "estimated") and not s:
            errors.append(f"{w}: {p} precision needs startsOn")
        if p in ("month", "estimated") and s and s.day != 1:
            errors.append(f"{w}: {p} startsOn not the 1st")
        if dl and s and dl > s:
            errors.append(f"{w}: deadline after start")
        if dl and not dt.date(2025, 1, 1) <= dl <= dt.date(2028, 12, 31):
            errors.append(f"{w}: deadline implausible {dl}")
        # urls
        for k in ("website", "sourceUrl"):
            v = e.get(k)
            if v is None:
                continue
            parsed = urllib.parse.urlsplit(v)
            if parsed.scheme not in ("http", "https") or not parsed.hostname or len(v) > 2048 or " " in v:
                errors.append(f"{w}: {k} not an http(s) URL")
        # privacy guard: phone-like numbers
        for k in ("participation", "notes", "cost"):
            v = e.get(k) or ""
            if re.search(r"\+\d[\d\s()-]{7,}", v):
                errors.append(f"{w}: {k} contains a phone number")
    for key, n in keys.items():
        if n > 1:
            errors.append(f"duplicate key {key}")
    for msg in warnings:
        print("WARN", msg)
    for msg in errors:
        print("ERROR", msg)
    ev = bundle["events"]
    print(f"events={len(ev)} errors={len(errors)} warnings={len(warnings)}")
    for field in ("scope", "format", "status", "priority", "datePrecision"):
        print(field, dict(sorted(collections.Counter(e[field] for e in ev).items())))
    upcoming = sorted((e["applicationDeadline"], e["key"]) for e in ev
                      if e["applicationDeadline"] and e["applicationDeadline"] >= TODAY.isoformat()
                      and e["status"] not in ("skipped", "rejected"))
    print("upcoming deadlines", upcoming[:5])
    print("all deadlines", sorted((e["applicationDeadline"], e["key"]) for e in ev if e["applicationDeadline"]))
    print("max notes", max(u16(e["notes"]) for e in ev), "max participation", max(u16(e["participation"]) for e in ev))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
