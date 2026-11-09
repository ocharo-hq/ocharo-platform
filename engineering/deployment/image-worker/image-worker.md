# Image worker

*Last updated: 2026-11-09*

Build and compose definition of the private image worker that Desk calls for background removal.

- `Dockerfile` — the worker's image, built from the `engineering/codebase/ocharo-image.backend-services/` context (its
  `.dockerignore` applies); `deploy.yml`'s `image-worker` service builds it the same way.
- `docker-compose.yml` — builds that image and binds the worker to `127.0.0.1:8360`.
