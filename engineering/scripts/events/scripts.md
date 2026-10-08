# scripts/

`validate_events_bundle.py` checks an `ocharo.events-import.v1` bundle against the
[events contract](../../architecture/events-contract.md#feed-import) before `--import-events` sees it:
enums, key format and uniqueness, date-precision rules, deadlines, text limits and http(s) links.
Python 3 standard library only.

```bash
python3 -I validate_events_bundle.py <events-bundle.json>   # exit 0 when clean; prints counts and upcoming deadlines
```

Bundles live in the private business workspace (`research/assets/och-events/desk-import/<date>/`), never in Git.
The seed of 2026-10-08 and every weekly discovery scan use the same bundle format; a re-import never
overwrites an event already in Desk.
