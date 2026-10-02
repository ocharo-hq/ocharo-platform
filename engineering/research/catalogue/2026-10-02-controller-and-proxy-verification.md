# Catalogue controllers and development proxy verification

*Last updated: 2026-10-02*

## Scope

The approved WoW2 rule requires block-bodied MVC controller members. Genuine lambda expressions
inside method bodies remain valid. Endpoint XML comments and response metadata are a separate,
pending proposal; this change adopts none of that proposal.

## Controller sweep

- Platform: nine controllers, 52 expression-bodied members converted.
- Marketing/retained Blog: 14 controllers, 66 expression-bodied members converted.
- Studio and Assets/Motion: one controller each; both already complied.
- Desk uses Minimal APIs and has no MVC controller classes.
- Roslyn parsed all 25 controllers, including the controller outside a `Controllers/` folder.
- Before/after token comparison permits only expression-to-block conversion and whitespace.
  Routes, signatures, response expressions, attributes and delegate lambdas remain equivalent.
- Final audit reports zero expression-bodied controller members.

## Catalogue route and proxy failure

[`DesignsController.Create`](../../codebase/ocharo-catalogue.backend-services/OcharoCatalogue.Api/Catalogue/Controllers/DesignsController.cs)
owns `POST /api/catalogue/designs`. `CatalogueController` owns settings, import and export subroutes.
The existing create contract returns `200` with the saved design; its contract was preserved.

Read-only runtime checks reproduced `GET /api/catalogue/designs` as `200` on backend HTTPS `8278`
and `500` through frontend HTTPS `8282`. Node reported `DEPTH_ZERO_SELF_SIGNED_CERT`; the running
frontend had no `VITE_API_CA_PATH` or `NODE_EXTRA_CA_CERTS` override. The existing public .NET
certificate made the same Node HTTPS request succeed.

The Vite proxy now checks for an existing .NET development certificate and loads its public PEM
for loopback targets. It preserves hostname/certificate verification, honors `VITE_API_CA_PATH`,
and leaves remote targets on Node's normal trust. Temporary certificate files are removed.
Build mode never accesses the certificate store. No private key is exported and no trust command runs.
Public-only PEM export semantics: [Microsoft dev-certs](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-dev-certs).

The user-owned Vite process reloaded the edited configuration automatically. The frontend proxy
then returned `200` for the same read-only GET. No user service or browser was started/restarted
by the agent; no test writes were sent to the running catalogue database.

## Executed verification

| Target | Result |
|---|---|
| Catalogue backend | Build passed; 51 tests passed |
| Catalogue route lifecycle | Explicit create/prepare/publish/archive test passed using an isolated temporary database; included above |
| Retained Brand backend | Build passed; 5 tests passed |
| Marketing backend | 254 tests passed; integrated frontend build passed |
| Retained Blog backend | 161 tests passed; integrated frontend build passed |
| Catalogue development proxy | 8 regression checks passed |
| Catalogue frontend | Typecheck, SFC compilation, lint and production build passed |
| Controller source | Roslyn token equivalence and whitespace checks passed |

The proxy tests cover loopback hostnames, explicit certificates, remote targets, build isolation,
missing-certificate failure and plaintext rejection. Existing certificate/trust settings remain unchanged.
Browser creation and owner review remain unverified; these checks do not imply production deployment.
