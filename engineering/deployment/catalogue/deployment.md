# deployment/

`Dockerfile` builds the single host (SPA in `wwwroot`, SQLite + blobs on the `/data` volume); `docker-compose.yml` runs it locally. Wheelhouse deploys it as the `catalogue` service and public site of `../deploy.yml`, without sign-in until the Ocharo identity server lands (owner decision 2026-10-24); a local run keeps the loopback boundary. Details: `../deployment.md` § Brand and Catalogue pilots.
Rules: wow-two-ws/conventions/development/repo/structure/repo-structure.md
