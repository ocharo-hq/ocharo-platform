# Ocharo Trends backend

The HTTPS profile serves the API on `https://localhost:8440`. Set `Trends__SeedPath` to an intake JSON file with a `videos` array, then run:

```sh
dotnet run --project OcharoTrends.Api --launch-profile https
```

Add more intake files through the `Trends:AdditionalSeedPaths` array. For environment configuration, use numbered
variables such as `Trends__AdditionalSeedPaths__0`. Each file contains a `videos` array, and video IDs must be unique
across all intake files. Relative paths resolve from the API content root. The app loads these files on startup.

Intake files feed the internal review queue. `Trends__PublishedGuidePath` selects the public guide snapshot, which
contains a JSON array at its root.

Internal review requires `ConnectionStrings__Database`, `Trends__OrganizationId`, and the `Identity__Client__Authority`, `Identity__Client__ClientId` and `Identity__Client__ClientSecret` settings for an Ocharo Identity client. Configure the client callback and sign-out for the browser origin. The Vite development client uses `https://localhost:8442/api/identity/callback` and `https://localhost:8442/`. Each read requires membership in the configured organization. A write also requires `trends:edit` and the `X-Csrf-Token` issued by `GET /api/identity/session`.

The local HTTPS profile applies the `trends` and `distributed_cache` migration sets when internal review is configured. Use a separate database for local development. Hosted environments apply migrations before starting the app. Development without review configuration exposes the public guide endpoint and reports that review is unavailable.

```sh
dotnet test OcharoTrends.BackendServices.slnx
```

The review integration test requires Docker and uses `postgres:18-alpine`. Its synthetic Identity handler runs only in the test host.
