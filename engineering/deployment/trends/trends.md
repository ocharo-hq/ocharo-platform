# Ocharo Trends

The public app reads a snapshot of selected filming guides. Each guide explains the format, shows the steps to film it and links to original examples. The internal review board holds the creator intake and saves each organization's useful, skipped or unreviewed decisions.

## Local review

The Trends service uses HTTPS on ports 8440 (API) and 8442 (web). Use the existing trusted development certificates. See the workspace's [local development guidance](../../../../../docs/local-development.md).

1. Set `Trends__SeedPath` to the absolute path of the research repository's `content/creators/jessechrisss.json`. Set `Trends__AdditionalSeedPaths__0` to the absolute path of `content/creators/outfit-references.json` to include the outfit references.
2. To show the example guide set locally, set `Trends__PublishedGuidePath` to the absolute path of `content/local-preview-guides.json` in the research repository.
3. Start the API with `dotnet run --project engineering/codebase/ocharo-trends.backend-services/OcharoTrends.Api --launch-profile https`.
4. In `engineering/codebase/ocharo-trends.frontend-services/`, run `pnpm install --frozen-lockfile` and `pnpm dev`. If needed, set `VITE_API_CA_PATH` to the public CA certificate for the API. Open [the guide library](https://localhost:8442/) or [the review queue](https://localhost:8442/review).

The Development host can serve the public side without an Identity client. Internal review requires `ConnectionStrings__Database`, the service's Ocharo `Identity__Client__*` settings and `Trends__OrganizationId`. Read access requires organization membership. Updating status or notes also requires `trends:edit` and a valid request token. Review decisions persist in schema `trends`.

## Deployable image

Build the single API and SPA image from the Platform repository root.

```sh
docker build -f engineering/deployment/trends/Dockerfile -t ocharo-trends engineering/codebase
```

The container takes its intake and public guides from configured JSON files mounted read-only at runtime. The selected guide snapshot is separate from intake and review data. An absent public snapshot serves an empty library. Set up the dedicated Ocharo Identity client and organization permission before activating a Wheelhouse deployment.

## Editorial data

The research checkout holds the source inventory, evidence and one Markdown file per format. Guide steps give the reader a way to reproduce each effect. Add time references as playback confirms a movement or edit. The board counts a distinct source video once toward 1,000 even when it appears in several guides.
