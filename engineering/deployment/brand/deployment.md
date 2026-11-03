# deployment/

`Dockerfile` builds the single host (SPA in `wwwroot`, SQLite + blobs on the `/data` volume); `docker-compose.yml` runs it locally. Wheelhouse deploys it as the `brand` service and site of `../deploy.yml`, signed in through Ocharo's Identity (client `brand`); the image needs `Identity:Client:*` and `Brand:OrganizationId` to boot, so this compose file no longer starts it on its own. Details: `../deployment.md` § Brand and Catalogue pilots.
Rules: wow-two-ws/conventions/development/repo/structure/repo-structure.md
