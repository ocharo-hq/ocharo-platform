# deployment/

Local pilot only (loopback boundary, no accounts). `Dockerfile` + `docker-compose.yml` build the single host (SPA in `wwwroot`, SQLite + blobs on the `/data` volume); a hosted deployment needs authentication first — the boundary middleware rejects non-loopback requests.
No deployment descriptor exists here: Wheelhouse deploys only `../deploy.yml`, which never includes this pilot.
Rules: wow-two-ws/conventions/development/repo/structure/repo-structure.md
