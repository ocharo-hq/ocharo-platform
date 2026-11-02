# Identity cutover

*Last updated: 2026-11-02*

Ocharo people sign in by Telegram through Ocharo's own WoW2 Identity instance on local, dev and prod, and every
Ocharo app moves off `Ocharo.Sso`. This is the goal's cross-lane track: vectors, lanes, order, contracts, decisions
and done-when. The orchestrator chat keeps it current; each lane keeps its own version track for its repo.

The ignored `.local/identity/identity-cutover-plan.md` (2026-11-01) stays a working note for line-level change lists
and the local settings writer. Its versions (image `0.5.2`, `Identity.Client` `10.0.15-beta`), its native 8388 topology
and its `Identity:SignUp:IsEnabled` are stale; where it disagrees with this doc, this doc wins.

## Status

- ⬜ G1 local: Desk signs in through Ocharo's local Identity
- ⬜ G2 dev: the same on hosted dev with `@ocharo_dev_bot`
- ⬜ G3 prod: the same on prod with `@ocharo_bot`
- ⬜ G4 apps: Brand, Catalogue, Marketing, Editorial and Studio sign in through Identity; `Ocharo.Sso` retired

## Goals

| Goal | Environment | Bot | Deployed by |
|---|---|---|---|
| G1 | local: Identity `https://localhost:8404`, Desk `https://localhost:8378` | `@ocharo_local_bot`, polling | platform lane |
| G2 | dev | `@ocharo_dev_bot`, webhook | Wheelhouse |
| G3 | prod | `@ocharo_bot`, webhook | the owner promotes; Wheelhouse runs the one-offs |
| G4 | every environment, app by app | the environment's bot | platform lane, then Wheelhouse |

G1–G3 are done when, on that environment:

- an Ocharo owner or admin makes an invite, and a person joins Ocharo through the bot with the invite's role;
- that person signs in to Desk by phone;
- Desk's `GET /api/identity/session` shows the organization `b8c672a6-…` (Ocharo), their role and `desk:*`
  permissions;
- the real suppliers and events list, and an edit saves.

G4 is done when each app signs in through Identity on every environment, and a grep of the Ocharo repos finds no
`Ocharo.Sso`, `integration-tickets`, `/api/sso` or `/api/auth/`.

## Target flow

Set by the developer on 2026-11-02:

- An Ocharo `owner` or `admin` makes an invite link carrying a role.
- The link opens the bot; one contact share gives phone, name and verification at once.
- The link is spent; the person joins Ocharo with that role.
- Sign-in: the person types the phone and gets the code in Telegram at once; Ocharo people have no email and no
  password.

Accounts:

- One account per person: one phone is one person; roles stay separate.
- The instance `admin` opens only the Identity console and never enters app tokens.
- Ocharo organization roles reach apps as `org_role`, their permissions as `org_permission`.
- Maftuna becomes Ocharo `owner` through the first real invite.
- Sultonbek is Ocharo `owner` now for testing on dev and prod (later `admin`), and the instance `admin`.

## Lanes

| Lane | Session | Repos | Owns containers | Own track |
|---|---|---|---|---|
| identity | `ocharo - identity` | `wow-two-sdk-beta.identity` | `identity-local-*` | Identity `version-track/v0.5/v0.5.md` |
| platform | `ocharo - platform` | `ocharo-platform` first, then `ocharo-marketing`, `ocharo-studio` | `ocharo-identity-*`, `brand-workspace-*` | this doc |
| wheelhouse | `ocharo - wheelhouse` | `wow-two-platform.wheelhouse` | `wheelhouse-*` | Wheelhouse version track |

- Every lane launches at the WoW2 workspace root; its hooks cover all three repos.
- One writer per repo; a lane hands a gap found in another lane's repo to the orchestrator.
- `keysmith-*` and `foreverpin-*` containers belong to no lane here and stay untouched.

## Order

1. identity: gaps #8–#12 and #14 ship in one release (image, NuGet, npm); #13 runs beside.
2. wheelhouse: builds the one-off command (#23) and confirms the rest of Identity's deploy needs (#22).
3. platform: starts at once on the published `0.5.7` / `10.0.17-beta` — local Identity to `0.5.7` (#15), Desk cutover
   (#16, #17), the owner signs in to Desk by phone; G1 closes on #1's release with the bot-first invite.
4. platform: the descriptor (#18) on #1's release.
5. wheelhouse: deploys to dev, resets the `identity` set, runs `admin create` with the same ids; G2 sign-in.
6. G3: the owner promotes prod; wheelhouse runs the prod one-offs; G3 sign-in.
7. platform: G4, app by app (#19).

- #1, #2 and #3 run at once; #4 waits for #1; #5 waits for #2 and #4.
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
| 8 | Bot-first invite: an invite deep link opens the bot; one contact share creates the person, joins the organization with the role, links the chat and spends the invite; a known phone joins with the role | missing — `/start {token}` serves only the `phone-sign-in` link a web send made for a known number; invitations redeem only in the web phone verify step; needs an invitation link purpose, unbound invites whose phone comes from the contact, and a `t.me/<bot>?start=` link builder |
| 9 | The shared contact's name fills `DisplayName` when empty | missing — phone sign-up stores user name and phone only; nothing reads the contact's names |
| 10 | Phone-only instance: no password, forgot-password or email on the hosted pages | partial — pages render only declared methods, but password counts as on whenever its services register, and the service always registers them; no `Methods:*` setting is bound |
| 11 | Instance admin: `AddRoles()` in the service, an `admin` seed, `admin create --role admin` (or a verb), a console gate, sign-in per OQ-1; instance roles never enter app tokens | missing — no instance role exists, the console has no gate, `admin` has `create` only; app tokens already drop role claims |
| 12 | `admin invite` CLI for the first invite | missing; widens the 2026-11-01 decision "the host CLI creates the first admin only" |
| 13 | Members and invitations panel in `fe-identity` (v0.5 iteration 4) | missing — `fe-identity` exports `./contracts` and `./kit` only |
| 14 | Deploy: pipelines `v0.15.0` in Identity's workflows, then `configCheck:` live in its `deploy.yml` | ready to do — `config check` is built and tested; workflows pin `v0.14.0`; the step is commented out |

- `/api/organizations/{id}/members` has no add route; members join by invitation.
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
- 24. Missing, wanted: a value-level settings record (renders keep fingerprints and changed keys, no values) and a
  warning for stale stored keys. Not proven live: the rollback gate's config check, and refusing a missing required
  setting — Identity's first deploy can prove both.
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

Open, cross-lane — the orchestrator asks the developer:

- OQ-1 Identity admin sign-in: Wheelhouse one-time links · the Telegram code alone · the code plus TOTP or passkey.
- OQ-2 Hosted email: phone-only for now (recommended; no sender domain yet) · an SMTP relay.
- OQ-3 Desk's real local data to hosted: now (restore Desk's schemas) · later.

Open, lane-local — each lane asks in its own chat:

- platform: Desk MCP (`ocharo-desk-mcp` ticket) rebased on the BFF session, or off until Identity agent tokens.
- identity: hosted page layout changes; whether an Open-mode "guest" is an account with no organization role.

## Risks

| # | Risk | Mitigation |
|---|---|---|
| 28 | Core `10.0.71 → 10.0.96` gates boot migrations to local runs, so hosted Desk may stop applying its schema at boot | Check a Production-mode boot, or add a `migrate:` item for `desk`; run Desk's e2e on PostgreSQL 18 |
| 29 | Integration tickets die with Desk's cutover: Marketing/Editorial `connected` mode, Studio's ticket path, Editorial's image path, Desk MCP | Run those apps in `local` mode until their own cutover |
| 30 | Wheelhouse values stored under retired identity keys make a 0.5.x deployed boot refuse | Delete them per environment before the deploy |
| 31 | Identity behind the ingress without forwarded headers answers `ID2083` | After the first deploy, check `/.well-known/openid-configuration` shows the site origin as issuer |
| 32 | Desk scripts sign in through `/api/auth/login` (`verify-platform.py`, `review-platform.py`) | Update or retire them with #16 |
