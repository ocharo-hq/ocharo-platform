# Catalogue deployment

`Dockerfile` builds the single host (SPA in `wwwroot`, SQLite + blobs on the `/data` volume) from the `engineering/codebase/` context. Wheelhouse deploys it as the `catalogue` service and site of `../deploy.yml`, signed in through Ocharo's Identity (client `catalogue`); the image needs `Identity:Client:*` and `Catalogue:OrganizationId` to boot, so it has no stand-alone local compose file. Details: `../deployment.md` § Brand and Catalogue pilots.
Rules: wow-two-ws/conventions/development/repo/structure/repo-structure.md
