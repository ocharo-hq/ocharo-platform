# Events deployment

*Last updated: 2026-11-10*

`Dockerfile` builds the single host (SPA in `wwwroot`) from the `engineering/codebase/` context: stage 1 builds
`ocharo-events.frontend-services`, stage 2 publishes `OcharoEvents.Api`, stage 3 runs it. The service keeps no event
rows: WoW2 Events holds the catalog and each organization's tracking (`research/events-app.md` #2). It keeps its sign-in
sessions in schema `ocharo_events_cache` and its Data Protection key ring in `ocharo_events.data_protection_keys` of the
product database (`ocharo_{env}`), so a restart or deploy keeps everyone signed in; it has no volume.

Wheelhouse deploys it as the `events` service and site of `../deploy.yml`, signed in through Ocharo's Identity as client
11 (`ocharo-events`), with `Events:BaseUrl` linked to Identity's resource 3 and the person's token exchanged for the
`events` resource. The image's `migrate` step applies the `ocharo_events` set (`001-baseline`, the key ring table) and
the session cache's `distributed_cache` set; `config check` runs before each restart. A deployed boot refuses without
`ConnectionStrings:Database`, `Identity:Client:*` or `Events:BaseUrl`, so the image has no stand-alone local compose file.

Rules: wow-two-ws/conventions/development/repo/structure/repo-structure.md
