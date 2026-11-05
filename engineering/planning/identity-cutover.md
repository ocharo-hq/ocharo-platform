# Identity cutover

*Last updated: 2026-11-05*

Ocharo people sign in by Telegram through Ocharo's own WoW2 Identity instance on local, dev and prod, and every
Ocharo app moves off `Ocharo.Sso`. This is the goal's cross-lane track: vectors, lanes, order, contracts, decisions
and done-when. The orchestrator chat keeps it current; each lane keeps its own version track for its repo.

The ignored `.local/identity/identity-cutover-plan.md` (2026-11-01) stays a working note for line-level change lists
and the local settings writer. Its versions (image `0.5.2`, `Identity.Client` `10.0.15-beta`), its native 8388 topology
and its `Identity:SignUp:IsEnabled` are stale; where it disagrees with this doc, this doc wins.

## Status

- ✅ G1 local: Desk signs in through Ocharo's local Identity — sign-in by phone, the edit check and Maftuna's invite through a trusted contact pass; her Desk session shows Ocharo, `owner`, `desk:edit` and `desk:approve` (2026-11-03). Her own phone moves to the backlog (#46)
- ✅ G2 dev: the same on hosted dev with `@ocharo_dev_bot` — `v0.1.17` (identity `0.5.15`, trusted contacts on) runs clean on dev, deployment `87f21066`, and `v0.1.18` (the bot's messages name Ocharo) runs clean, deployment `c7f6904c`; the owner signed in to Desk by phone through `@ocharo_dev_bot` and sees Ocharo as `owner` (2026-11-03); closed on that sign-in alone (#49)
- ⬜ [parked] G3 prod (#51): the same on prod with `@ocharo_bot` — prod runs `v0.1.18` (identity `0.5.15`, deployment `e9421fb3`, 2026-11-04): services healthy, issuer right (#31), and Identity's boot log shows the webhook registered; prod stored no retired identity keys (#30), and Ocharo's organization takes the preset id `b8c672a6-…` there too (#43), so v0.1.19's Brand and Catalogue `OrganizationId` can be set before the bootstrap creates it; `admin create` ran (job `b874412e`) and the owner signed in to the prod console by phone through `@ocharo_bot` (2026-11-04), so Ocharo's creation and the invite remain
- 🔄 G4 apps: Brand, Catalogue, Marketing, Editorial and Studio sign in through Identity; `Ocharo.Sso` retired — `v0.1.19` (Brand and Catalogue on Identity) runs clean on dev, deployment `40f44d37` (2026-11-04), and both dev sessions read back Ocharo, `owner` and each app's `edit` and `approve` (2026-11-05), and prod waits with G3 (#51). Marketing and Editorial sign in locally through 8404 (`ocharo-marketing` `307a6cc`, `ocharo-platform` `a1148da`, 2026-11-04), unpushed until the pipelines release (#50); Studio follows

## Goals

| Goal | Environment | Bot | Deployed by |
|---|---|---|---|
| G1 | local: Identity `https://localhost:8404`, Desk `https://localhost:8264` | `@ocharo_local_bot`, polling | platform lane |
| G2 | dev | `@ocharo_dev_bot`, webhook | Wheelhouse |
| G3 | prod | `@ocharo_bot`, webhook | wheelhouse lane: settings, promotion, one-offs |
| G4 | every environment, app by app | the environment's bot | platform lane, then Wheelhouse |

G1–G3 are done when, on that environment:

- an Ocharo owner or admin makes an invite, and a person joins Ocharo through the bot with the invite's role;
- that person signs in to Desk by phone;
- Desk's `GET /api/identity/session` shows the organization `b8c672a6-…` (Ocharo), their role and `desk:*`
  permissions;
- on G1 only, the real suppliers and events list, and an edit saves (#39).

G4 is done when each app signs in through Identity on every environment, and a grep of the Ocharo repos finds no
`Ocharo.Sso`, `integration-tickets`, `/api/sso` or `/api/auth/`.

## Target flow

Set by the developer on 2026-11-02 (revised the same day, #33):

- Sign-up: an Ocharo `owner` or `admin` sends an invite link carrying a role. The link opens a sign-up page where the
  person enters their name and profile picture, then takes one action to verify the phone: it sends them to the bot,
  whose verification proves the phone and links their Telegram chat. The link is spent; the person joins Ocharo with
  that role.
- Sign-in: the app's sign-in forwards to Identity; the person types the phone, receives a code from the bot, enters it
  and is signed in. Ocharo people have no email and no password.

Accounts:

- One account per person: one phone is one person; roles stay separate.
- The instance `admin` opens only the Identity console and never enters app tokens.
- Ocharo organization roles reach apps as `org_role`, their permissions as `org_permission`.
- Maftuna becomes Ocharo `owner` through the first real invite.
- Sultonbek is Ocharo `owner` now for testing on dev and prod (later `admin`), and the instance `admin`.

## Lanes

| Lane | Session | Repos | Owns containers | Own track |
|---|---|---|---|---|
| identity | `ocharo - identity` (`local_ad26c1c8-…`) | `wow-two-sdk-beta.identity` | `identity-local-*` | Identity `version-track/v0.5/v0.5.md` |
| platform | `ocharo - platform` (`local_0c76416a-…`) | `ocharo-platform` first, then `ocharo-marketing`, `ocharo-studio` | `ocharo-identity-*`, `brand-workspace-*` | this doc |
| wheelhouse | `ocharo - wheelhouse` (`local_f7b86dc6-…`) | `wow-two-platform.wheelhouse` | `wheelhouse-*` | Wheelhouse version track |

- The orchestrator is `w2 - ocharo with tg sign in` (`local_89e16694-…`); all four launched 2026-11-02.

- Every lane launches at the WoW2 workspace root; its hooks cover all three repos.
- One writer per repo; a lane hands a gap found in another lane's repo to the orchestrator.
- `keysmith-*` and `foreverpin-*` containers belong to no lane here and stay untouched.

## Order

1. identity: gaps #10–#12 and #14 shipped, #8 and #9 as the sign-up slice (#34); one release (image, NuGet, npm); #13 runs beside.
2. wheelhouse: builds the one-off command (#23) and confirms the rest of Identity's deploy needs (#22).
3. platform: starts at once on the published `0.5.7` / `10.0.17-beta` — local Identity to `0.5.7` (#15), Desk cutover
   (#16, #17), the owner signs in to Desk by phone; G1 closes on #1's release with the bot-first invite.
4. platform: the descriptor (#18) on #1's release.
5. wheelhouse: deploys to dev, resets the `identity` set, runs `admin create` with the same ids; G2 sign-in.
6. G3: wheelhouse removes prod's retired keys (#30), promotes prod and runs the prod one-offs; G3 sign-in.
7. platform: G4, app by app (#19).

- #1, #2 and #3 run at once; #4 waits for #1; #5 waits for #2 and #4.
- G1's invite check passed through a trusted contact on `0.5.15` (#45); G3's invite runs the same way on prod, and G2
  skipped its own (#49).
- G4's local work (#19, #47) starts beside G2 and G3 to shorten the path; its `ocharo-platform` pushes wait until G2 closes, and G3
  promotes the release G2 passed on, `v0.1.18`.
- Each goal closes with a cross-lane check against its done-when before the next starts.

## Contracts

Facts one lane delivers to another; the orchestrator relays each after checking it against the commit, release or
deploy.

- identity → platform, wheelhouse: the released versions — image tag, NuGet `WoW2.Sdk.Beta.Be.Identity.*`, npm
  `@wow-two-beta/fe-identity` — and the core `WoW2.Sdk.Beta.Be` floor of `Identity.Client`.
- identity → platform: the settings keys the release binds and retires, the invite deep-link shape, the
  `admin create` and `admin invite` verbs with exit codes and last-line results, and the claims apps read (`sub`,
  `sid`, `org_id`, `org_role`, `org_permission`).
- identity → wheelhouse: the `config check` command with its exit codes and `config-result` line; the one-off verbs a
  deploy runs and the settings they need.
- wheelhouse → platform: the one-off's descriptor or API shape; per environment the deployed release, one-off results
  and any refused setting.
- platform → wheelhouse: a committed descriptor release; the preset organization and owner ids for `admin create`,
  passed in the lane chats, never in a repo.
- developer → lanes: bot tokens, passwords and phone numbers, entered by the developer; no lane prints one.

## Identity

Snapshot `2376611` (tags `v0.5.7`, `be-v10.0.17-beta`), `fe-v0.0.7` at `40fe519`, `main` at `4197034`. Published NuGet
`10.0.17-beta` (`Client` floors core at `10.0.96-beta`; core is at `10.0.97-beta`, a release-only bump), npm `0.0.7`,
image `0.5.7`, amd64 only.

| # | Gap | State at `4197034` |
|---|---|---|
| 8 | Invite sign-up (revised by #33): the invite opens a sign-up page — name, profile picture, one verify-phone action through the bot that proves the phone and links the chat — then joins the organization with the role and spends the invite; a known phone joins with the role | missing — `/start {token}` serves only the `phone-sign-in` link a web send made for a known number; invitations redeem only in the web phone verify step; needs an invitation link purpose, unbound invites whose phone comes from the contact, and a `t.me/<bot>?start=` link builder |
| 9 | Profile on sign-up: the entered name fills `DisplayName` (the shared contact's name when empty); the profile picture is stored and reaches apps | missing — phone sign-up stores user name and phone only; nothing reads the contact's names |
| 10 | Phone-only instance: no password, forgot-password or email on the hosted pages | partial — pages render only declared methods, but password counts as on whenever its services register, and the service always registers them; no `Methods:*` setting is bound |
| 11 | Instance admin: `AddRoles()` in the service, an `admin` seed, `admin create --role admin` (or a verb), a console gate, sign-in by phone and bot code (#33); instance roles never enter app tokens | missing — no instance role exists, the console has no gate, `admin` has `create` only; app tokens already drop role claims |
| 12 | `admin invite` CLI for the first invite | missing; widens the 2026-11-01 decision "the host CLI creates the first admin only" |
| 13 | Members and invitations panel in `fe-identity` (v0.5 iteration 4) | missing — `fe-identity` exports `./contracts` and `./kit` only |
| 14 | Deploy: pipelines `v0.15.0` in Identity's workflows, then `configCheck:` live in its `deploy.yml` | ready to do — `config check` is built and tested; workflows pin `v0.14.0`; the step is commented out |

- Released 2026-11-02 from `4737e9b`: image `v0.5.8`, NuGet `10.0.18-beta`, npm `0.0.8` — #14 (`19841af`), #10
  (`c7d5cfc`), #11 and #12 (`18bfed0`). #13 publishes from `7e122a0`; #8 and #9 build as the sign-up slice (#34).
- For Ocharo, 0.5.8 binds `Identity:Methods:{Password,Email}:IsEnabled` (both `false`) and refuses
  `Identity:SignUp:IsEnabled` in config check; `Identity:Organizations:OwnerRole` is unbound and silently ignored.
- Apps read `org_role` as `{org_id}:{role}` and `org_permission` as a JSON array of `{org_id}:{permission}`.
- An arm64 image needs platforms in `wow-two-platform.pipelines` `delivery.yml`, a repo no lane owns; outside this goal.
- `/api/organizations/{id}/members` has no add route; members join by invitation.
- Released 2026-11-02: `v0.5.9` / npm `0.0.9` ship #13 (`./admin` members and invitations panels); `v0.5.10`, NuGet
  `10.0.19-beta`, npm `0.0.10` ship #8 and #9 — `Identity:SignUp:Profile:{DisplayName,Picture}` (Ocharo: both
  `Required`), `Identity:ProfilePictures:*` defaults per #34, migration `007-invite-sign-up`, codeless verify through
  a per-sign-up bot link, and a `picture` claim served from the issuer. Not yet checked on a real bot.
- Released 2026-11-03: `v0.5.13` (NuGet `10.0.22-beta`, npm `0.0.11`) opens the console to organization owners and
  admins, narrows `admin create` to the instance admin (#43) and guards recycled phone numbers (migration
  `008-telegram-link-changes`); `v0.5.14` (`10.0.23-beta`, `0.0.12`) adds the console shell with Sign out, live
  instance roles, session revocation on any role change, the signed-in pass-through on `/sign-in`, and equal
  owner/admin member management with zero owners allowed.
- Released 2026-11-03: `v0.5.15` (`6c08d12`; NuGet `10.0.24-beta`, npm `0.0.13`) ships trusted contacts slice 1
  (#45): an invitation names a trusted contact, who receives the invitee's sign-up and sign-in codes; revoking a contact
  ends its sessions. Ocharo sets `Identity:TrustedContacts:IsEnabled=true` (default off; needs the bot); migrations
  `009-telegram-messages` and `010-trusted-contacts`. Claims, the BFF contract and Desk are unchanged, and a contact
  sign-in still carries `amr` `otp`. Not yet checked in a browser; the 8404 run is its first real test.
- fe sdk gaps found by the console, outside this goal: `SidebarMenu` types reject `aria-label` under
  `strictTemplates`, and the fe sdk exports no icons (Identity drew six app-local glyphs).
- The sign-up slice pins SkiaSharp to core's `3.119.2` (`6b24984`): core's older native library shadowed 4.x on
  Linux. Moving core to SkiaSharp 4.x is a core be sdk change outside this goal.
- Also open in Identity, outside the target flow: arm64 image, HTTPS from a mounted PEM, compose build fix, stranding
  checks, uz/ru texts; the be sdk's 500 on a missing required member and MailKit's message built outside its `try`.

## Platform

Snapshot `d295565`.

- 15. Local Identity runs image `0.5.2` at `https://localhost:8404` (compose project `ocharo-identity`, ignored in
  `.local/identity/`), PostgreSQL 18 on 8406, Mailpit on 8408/8409, the dev certificate as PEM; ports in WoW2
  `conventions/deployment/hosting/ports.md`. Its database holds 1 organization (Desk's real one, with 23 suppliers and
  68 events) and 1 user, the owner, with a confirmed phone, no email and no password; the `desk` client is registered.
  Its compose still sets the retired `Identity__SignUp__IsEnabled`, and probably `Identity:Organizations:OwnerRole`,
  which 0.5.7 no longer binds (untested).
- 16. Desk cutover: core `10.0.71-beta` → the identity release's floor, add `Identity.Client`, wire the BFF
  (`AddIdentityBff`, `UseIdentityBff`, `MapIdentityBffEndpoints`), rebuild `OrganizationAccessFilter` from claims
  (`org_id` equals the route organization, `org_role` → `Role`, `desk:edit` → `CanEdit`, `desk:approve` →
  `CanApprove`), read `sub` and `sid` in `AccessClaims`, drop Desk's own login, Telegram access, SSO broker and
  antiforgery; the SPA reads `GET /api/identity/session`. Desk's ignored Local file already names the 8404 authority
  and the `desk` client, but no code reads `Identity:Client` yet. Line-level list: the cutover plan's section 2.1.
- 17. Desk projects each signed-in person into `access.users` just in time: an invitee arrives with a new `sub`, and
  18 foreign keys need the row. Hosted Desk also needs a shared `IDistributedCache` (missing; the BFF falls back to
  memory) beside its persisted Data Protection key ring (present, `DataProtection:KeysPath`). The Ocharo organization
  row exists on each hosted environment before its first write — by restore or an adopt verb, per OQ-3.
- 18. Descriptor `engineering/deployment/deploy.yml`: identity image → the identity release (today `0.3.7` with the
  retired `Identity:SignUp:IsEnabled`, which a 0.5.7 boot refuses); the Ocharo instance settings (#20); clients 0–5;
  `Identity:Client:*` per app (#21); `configCheck:`; the delivery workflow on pipelines `v0.15.0` (today `v0.14.0`,
  which refuses `configCheck:`).
- Committed 2026-11-02, #18: `192ecd7` (unpushed, #38) — identity `0.5.8` with 55 keys and the #20 defaults, a
  `configCheck:` item, delivery on pipelines `v0.15.0`, Desk's `Identity:Client` from `Sites:identity:Url` and client 0.
  Wheelhouse generates the certificates, signing, OTP and webhook keys and client 0's secret; the developer types
  the bot user name and token and client 0's two redirect URIs per environment, since `from:` takes no path
  suffix. Clients 1–5 join with their apps in G4. Desk sits on `Identity.Client` `10.0.18-beta` (`746f53c`).
- Closed 2026-11-02, #17's cache (#37): core be sdk `10.0.98-beta` adds `AddPostgresDistributedCache` with its own
  migration set `distributed_cache` (schema `cache`); Desk adopts it in `61b3045`, so a restart or deploy keeps
  people signed in. The `migrate` role needs `CREATE` on Desk's database.
- 19. G4 per app, in order: Brand + Catalogue (one release, drop `*:PublicAccess`), Marketing + Editorial (shared
  `packages/connect`), Studio (keep its workspace-id formula on `org_id` and `sub`), then retire `ocharo-sso`, the
  `shared/Ocharo.Sso` copies, integration tickets and Desk migration `011-identity-cutover`.
- Desk's real data sits in `brand-workspace-postgres-1` (127.0.0.1:8270), migrations at `010-app-sso`.
- `engineering/architecture/telegram-bots.md` names the local token file `identity.local.env`; the real one is
  `.local/identity/telegram.env`.

## Settings

The Ocharo instance's settings, by key; secrets and owner keys stay in Wheelhouse or ignored local files.

- 20. `Identity:SignUp:Mode` = `Invite`; `Identity:Sessions:Expiration` = `Sliding`, `SlidingLifetime` = `5.00:00:00`;
  `Identity:Pages` theme and brand `ocharo` / `Ocharo`; `Identity:Phone:AllowedCountries` = `UZ`;
  `Identity:Telegram:{IsEnabled,BotUserName,BotToken,UpdateMode,WebhookSecret}` per environment (polling locally,
  webhook hosted); phone-only methods (#10) with email unset while OQ-2 keeps hosted phone-only; organization role
  permissions for the apps (`desk:*`, `brand:*`, `catalogue:*`, `marketing:*`, `editorial:*`, `studio:*`); role
  names from the catalog only.
- 21. Each app: `Identity:Client:{Authority,ClientId,ClientSecret,CookieName,PostSignOutRedirectPath}`; the
  authority equals the issuer; a distinct `__Host-ocharo-{app}` cookie per app, since every local app shares
  `localhost`'s cookie jar.

## Wheelhouse

Snapshot: release `v0.12.0` (`0825c65`) on dev and prod, `main` at `aa998be`; v0.12 config check closed; backups wait
for an unopened v0.13.

- 22. Ready today: `configCheck:` (pipelines `v0.15.0` accepts it, `v0.14.0` refuses it; be sdk `10.0.96-beta`);
  settings declared by key with values per product and environment and secrets in the vault, linked keys included
  (`Identity:Server:Issuer` from `Sites:identity:Url`, a client secret shared with Desk); a set reset that rolls only
  the `identity` set back to 0 and snapshots first; several images per product; health checks; the Telegram webhook
  through a public site route on the environment's baseline host.
- 23. Missing, built before G2: a one-off command in a service's release image with that service's rendered settings
  — `admin create`, `admin invite` — since control verbs stop at `stop`, `start`, `restart` and `reset`. The backlog
  already holds it as an `admin create` / `admin sign-in-link` one-off.
- Shipped 2026-11-02, #23: Wheelhouse `v0.13.1` (`6795280`, `5d7630e`) runs a one-off command in a service's
  running release image with its rendered settings (console: service menu › Run a command; API control
  `operation: run`); it keeps the exit code and a redacted last line, and history shows only a masked label. Live on
  Wheelhouse dev; Ocharo's server sits in Wheelhouse prod's console, so G2's `admin create` waits for its prod
  promotion. A `sign-in-link` result needs a reveal-once read (backlog).
- Ocharo dev prepared 2026-11-02 except the dev bot token: the owner keys are typed and saved, not applied, and no
  stale identity value is stored there; until the `ocharo-platform` push nobody applies Ocharo dev settings, since undeclared
  keys would reach every old service. Wheelhouse `v0.13.2` adds a show-once read for `admin invite` links.
- 24. Missing, wanted: a value-level settings record (renders keep fingerprints and changed keys, no values) and a
  warning for stale stored keys. Not proven live: the rollback gate's config check, and refusing a missing required
  setting — Identity's first deploy can prove both.
- First dev deploy, 2026-11-02, Ocharo `v0.1.15` (`b59a522`): migrate passed (identity set 3 → 7, no reset), then
  identity `0.5.12` exited "A client has no ClientId" — the render never writes a descriptor `default:` value — and
  no config check ran, since release-set composition drops each repo's `configCheck:` items. The rollback to
  `v0.1.14` left identity `0.3.7` crash-looping on the new `Clients:0:*` values, which reach every service while
  undeclared; Desk, Brand and Catalogue run healthy. Wheelhouse fixes both as `v0.13.3`; after its dev proof and the
  developer's prod promotion, it reconciles Ocharo dev and redeploys `v0.1.15` with no `ocharo-platform` change.
- Second dev deploy, 2026-11-03, after Wheelhouse prod moved to `v0.13.3`: Ocharo dev runs `v0.1.15` healthy
  (deployment `7e0c4289`, 10 services, 61 descriptor defaults rendered); identity sits at migration 7, its issuer is
  the site origin and its Telegram webhook is registered. The config check still did not run, since the set bundle
  for `v0.1.15` was composed by `0.12.0` and cached; the next Ocharo release composes it fresh.
- Dev bootstrap, 2026-11-03: the developer ran `admin create` (identity `0.5.12` verb) from the Wheelhouse run dialog;
  it exited 0 with status `created`, both preset ids echoed and the instance role `admin` granted. An earlier try
  exited 2 on a placeholder phone and wrote nothing. The `ocharo-platform` push to identity `0.5.14` follows, and its fresh set
  compose is the config check's live proof.
- Third dev deploy, 2026-11-03: `ocharo-platform` `555011d` released `v0.1.16`, and Ocharo dev took it automatically (deployment
  `2e998f6e`, 1m 11s): identity `0.5.12` → `0.5.14`, desk and jobs `0.1.16`, all 10 services healthy. Identity
  migrated 7 → 8 (`008-telegram-link-changes`, expand) and kept its webhook. The freshly composed set ran the config
  check live for the first time: "Config check identity: Consistent". Once the developer allowed read-only product
  database queries in auto mode, a query on `ocharo_dev` showed the preset owner holding the instance role `admin`.
- Custom domains are planned, not built; until then the webhook URL is the baseline host, and `setWebhook` is
  Identity's job.
- Grants: Wheelhouse prod is promoted only for dev-proven releases; a rollback waits for the owner; another product's
  prod is never promoted; deploying another product to dev is fine.

## Decisions

Settled:

- 25. The Ocharo instance runs `SignUp:Mode` `Invite` and `Sliding` sessions of 5 days; role names come from the
  catalog only.
- 26. One poller per bot: `@ocharo_local_bot` belongs to Ocharo's 8404 instance; Identity's own 8389 stack tests phone
  flows with its own bot, or after the 8404 poller stops.
- 27. Hosted Desk's BFF uses a shared `IDistributedCache` and a persisted Data Protection key ring, or every deploy
  signs everyone out.

- 33. The instance `admin` signs in like every Ocharo person: phone and a code from the bot; TOTP or a passkey come
  once others get console access (OQ-1, developer, 2026-11-02). The same day the invite stopped being bot-first: it
  lands on a sign-up page with name, profile picture and one verify-phone action through the bot, which also links
  the chat; gaps #8 and #9 wait for OQ-4.
- 34. The invite sign-up is built in the generic Identity server, not a custom Ocharo host (OQ-4, developer,
  2026-11-02): a custom host first needs Identity to export host composition, pages and claim hooks, then owns invite
  redemption and sessions. The slice ships before G2 — `Identity:SignUp:Profile:{DisplayName,Picture}` each Off,
  Optional or Required (Ocharo: both Required, nothing else asked); the picture stored in Identity's database behind a
  seam and served as a `picture` claim; JPEG, PNG and WebP checked by magic bytes, at most 5 MB, 256–4096 px,
  re-encoded to a square 512 px WebP; codeless verify through the bot. The full pass follows G3: any custom field,
  self-service profile edit, File Storage with image variants, the Telegram photo as a prefill.
- 35. Ocharo signs in by Telegram only: email and password are off through `Identity:Methods`, and hosted Identity
  carries no `Identity:Email:*` keys (OQ-2, developer, 2026-11-02).
- 36. Identity's backlog gains trusted devices and trusted contacts: a person signs in with a code sent to someone
  they trust (developer, 2026-11-02); outside this goal.
- 37. Wheelhouse provisions no Valkey yet (it parses `needs: [valkey]` but fills only PostgreSQL keys), so hosted
  Desk's shared session cache (#27) is a PostgreSQL `IDistributedCache` in the core be sdk, written by the platform
  lane after #18; dev may run on the BFF's memory default until then, prod may not (orchestrator, 2026-11-02).
- 38. The platform lane pushes `ocharo-platform` once: the descriptor plus the Desk cutover, after the wheelhouse lane reports
  Ocharo dev prepared — the one-off shipped, stale identity keys deleted, owner keys and the dev bot token set —
  since a `main` push releases and auto-deploys to dev.

- 39. Desk's real data stays local for now: Identity working end to end comes first, and a separate workflow moves
  the data once File Storage and Events run (OQ-3, developer, 2026-11-02). G2 and G3 drop the data checks; hosted
  Desk projects the Ocharo organization row and each person idempotently, with no dump.
- 40. Core be sdk's backlog gains the SkiaSharp 4.x move (`6f91b36`, unpushed, since every core `main` push
  publishes a release).
- 41. Invites come only from the Identity console: an owner or admin signs in by phone, picks an organization they
  own or administer and makes the link in the invitations panel; Identity's next release removes the `admin invite`
  verb, so #12 is withdrawn and the one-off runs `admin create` only (developer, 2026-11-02). Maftuna's owner invite
  is made there by Sultonbek. The same release adds `org_name` / `org_slug` claims (`9cb95d0`), which Desk uses to
  name the organization row it creates on first sign-in (`5339b9d`).
- 43. `admin create` makes only the instance admin, once (developer, option B, 2026-11-03); the instance admin
  creates organizations in the Identity console, with a preset id for Ocharo (`b8c672a6-…`), then invites from it.
  The bootstrap per environment becomes migrate → `admin create` → console sign-in by phone → create Ocharo →
  invite Maftuna as owner. Ocharo dev's redeploy on identity `0.5.12` runs today's verb once; the end state is the
  same. The next Identity release also guards against a recycled phone moving a Telegram link.
- 44. Wheelhouse `v0.13.3` (`4cacf67`) renders descriptor defaults and keeps each repo's `configCheck:`; proven
  on dev, its prod promotion is the developer's click and gates the Ocharo dev redeploy.
- 45. Trusted contacts move into this goal (supersedes #36's "outside this goal" for contacts; trusted devices stay
  backlog): the invite check's second person, Maftuna, joins and signs in with codes sent to a trusted contact, and
  adds her own phone later by sharing her contact with the bot. The platform lane wrote the requirements; Identity
  settled the flow and its slice 1 API in `9f224fe` and released slice 1 as `v0.5.15`; the platform lane re-pins on it
  (2026-11-03). The `ocharo-platform` push to dev waits until the local invite check passes on 8404. The flow's open choices are asked in the identity lane.
  The check passed on 2026-11-03, and `v0.1.17` carries `0.5.15` with trusted contacts on to dev.
- 46. Maftuna's own phone — adding it through the bot's add-phone link — and her own-phone sign-in move to the backlog
  as a verification item (developer, 2026-11-03: "checking that can be backlog item ( verification )"). Nothing waits
  on it; the push of `45c9d7b` (the bot's messages name Ocharo) goes ahead.
- 47. G4 needs no new Identity feature for sign-in or roles (analysis, 2026-11-03). Roles stay Identity's fixed
  catalog (owner, admin, editor, viewer, billing); each app's permissions are namespaced on them in
  `Identity:Organizations:Roles`, which the descriptor already sets: owner and admin get
  `{desk,brand,catalogue,marketing,editorial}:{edit,approve}` and `studio:edit`, editor every `:edit`. Each app joins
  as one `Identity:Server:Clients:N` entry with its `PermissionNamespace`, plus its own `Identity:Client`. Against
  `Ocharo.Sso`, admin now approves and Studio has no `approve`. Not needed for G4, and absent from Identity: custom
  roles, per-person app access, back-channel sign-out (I18), agent tokens (Desk MCP stays parked). One gap: Editorial's
  image worker calls Desk's `/api/integrations/media/background-removal` with an integration ticket, and Identity has
  no client-credentials grant yet (Identity backlog, G8/G10).
- 48. The lanes drive Wheelhouse for every environment, prod included, through Claude in Chrome: settings, deploys,
  promotions and one-offs (developer, 2026-11-03: "this is why claude extension exists - you have full access to all
  products in wheelhouse, even for prod env"). The developer still enters secrets — bot tokens and passwords. Auto
  mode refuses a prod Deploy click until the `WoW2 Wheelhouse Deploys` allow rule is in place (the owner added it,
  2026-11-04).
- 49. G2 closes on the owner's Desk sign-in through `@ocharo_dev_bot` alone (owner, 2026-11-04: "yes we can verify this
  and fix if needed later"). Dev's invite and the read of its Desk session move to the backlog. G2 passed on `v0.1.18`,
  so G3 promotes `v0.1.18`, and `ocharo-platform`'s G4 push may start; v0.1.19 reaches dev only once wheelhouse sets its six keys.
- 50. Marketing, Editorial and Studio link settings across repositories: `Identity:Client:ClientSecret` from
  `Identity:Server:Clients:N:ClientSecret`, and `Sites:identity:Url`, both in `ocharo-platform`'s descriptor. Pipelines'
  `link_sources` refuses a source its own descriptor doesn't declare, so their CI would fail; the runner's `linked_value`
  looks ready, not run end to end. Typing the secret per environment fails too, since a generated secret can't be read
  back. Their pushes, and `ocharo-platform`'s clients 3 and 4, wait for the pipelines change, which the wheelhouse lane
  builds. It needs no tag (owner, 2026-11-05): consumers pin the pushed commit's SHA.
- 51. Prod parks until last (owner, 2026-11-05: "we need to close identity feature, then files, then events, we are so
  behind in schedule that checking this is literally the last thing now"). Ocharo's creation on prod, the invite, the
  G3 sign-in and every prod promotion wait; prod stays on `v0.1.18`. Identity closes on dev.
- 52. Editorial's Desk call (#47), live public builds and every product server's call to Files use one mechanism: Identity
  issues client-credentials tokens, audience-bound per service and verified offline (owner, 2026-11-05). It closes with
  Identity, since Files is next and already accepts these tokens. The design is still being settled in the orchestrator
  chat; API keys stay only for callers outside Identity.

Open, cross-lane:

- How a service acts for a person (#52): delegation headers or token exchange.

Open, lane-local — each lane asks in its own chat:

- platform: Desk MCP (`ocharo-desk-mcp` ticket) rebased on the BFF session, or off until Identity agent tokens.
- identity: hosted page layout changes; whether an Open-mode "guest" is an account with no organization role.

## Risks

| # | Risk | Mitigation |
|---|---|---|
| 28 | Core `10.0.71 → 10.0.96` gates boot migrations to local runs, so hosted Desk may stop applying its schema at boot | Check a Production-mode boot, or add a `migrate:` item for `desk`; run Desk's e2e on PostgreSQL 18 |
| 29 | Integration tickets die with Desk's cutover: Marketing/Editorial `connected` mode, Studio's ticket path, Editorial's image path, Desk MCP | Run those apps in `local` mode until their own cutover |
| 30 | Wheelhouse values stored under retired identity keys make a 0.5.x deployed boot refuse | Before a promotion, wheelhouse reads the target's settings for the new release and removes its `extra` identity keys; the config check refuses a deploy still carrying one as an unknown key (proven on dev, 2026-11-02), so no Wheelhouse feature is needed |
| 31 | Identity behind the ingress without forwarded headers answers `ID2083` | After the first deploy, check `/.well-known/openid-configuration` shows the site origin as issuer |
| 42 | A safety net marked "not proven live" (#25) is trusted on a first deploy | Prove it on the canary first; Ocharo's first deploy assumed the config check would refuse, and it never ran |
| 32 | Desk scripts sign in through `/api/auth/login` (`verify-platform.py`, `review-platform.py`) | Update or retire them with #16 |
