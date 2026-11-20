# Events deployment

*Last updated: 2026-11-20*

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

SCFD's stored copies of event images reach the card through two optional settings, `Files:BaseUrl` (typed `url`) and
`Files:ApiKey` (a secret), the address of WoW2 Files and a read-only key (`files.read`) of SCFD's tenant `scfd`; the key
is SCFD's sibling key, so SCFD's own read+write key is never shared. With either missing, or when Files fails or answers late, the
image route answers 302 to that image's own source `url` instead (only an absolute https link without credentials, else
404), so a card keeps the source photo it shows today until the copy is served, and the service boots and runs as before. Both wait for the Files `scfd`
read-only key: until the Files owner declares and issues it, leave them unset. Set the secret with `wow2 secret set` (it
prompts for the value), never in a file, a log or a chat.

`ocharo-workflow.json` is Ocharo's workflow in Events: the stage labels in uz, ru and en, their classes, the default
stage and the tracking fields' titles. A deploy that changes it runs Events' `workflow put --spec -` once, through
Wheelhouse Run ([deployment.md](../deployment.md#events-workflow)); the Events page reads the stored workflow and never
writes it.

Rules: wow-two-ws/conventions/development/repo/structure/repo-structure.md
