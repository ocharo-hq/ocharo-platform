# OcharoBrand.Tests.Support

*Last updated: 2026-11-09*

What the Integration and E2E tiers share below the host: `BrandTestDatabase` starts one `postgres:18-alpine` container
per test run (Testcontainers; its reaper removes it when the run ends), migrates a template with Brand's `brand` set and
session cache, and copies it into a fresh product database per test; `LegacyBrandStore` writes a retired SQLite store as
its last release left it, by hand or from a product database's `brand` rows.
