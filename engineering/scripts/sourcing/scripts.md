# scripts/

`build_shawl_import_bundle.py` maps the private shawl-sourcing ledger and supplier response folders to an
`ocharo.sourcing-import.v1` bundle for `--import-sourcing` ([contract](../../architecture/sourcing-contract.md#import-bundle)).
Python 3 standard library only.

```bash
python3 -I build_shawl_import_bundle.py --research <mft research folder> --out <private output folder> \
  [--viewer-data <old viewer src/data folder>]   # optional: supplies city
python3 -I -m unittest                           # synthetic fixture; no private data
```

- Copies the ledger to `ledger-snapshot.json` first and reads only that copy; path, SHA-256 and time go into `source`.
  The research folder is never written.
- Writes `sourcing-bundle.json` (validated against the contract) and `report.json` (counts per supplier,
  warnings, exclusions with reasons). Both hold private contact data: keep them in private storage, never in Git.
- `quoted` needs a stated price; `qualified` is never imported. WhatsApp copies are `screenshot`, email files
  `original`, PDF extractions `derived`. Proof screenshots, our references and render sheets are excluded.
