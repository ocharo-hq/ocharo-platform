# Ocharo HTTP errors and confirmed request lifecycle

*Last updated: 2026-10-02*

## Scope and decisions

This sweep covers the seven .NET APIs (Desk, retained Brand, native Catalogue, Marketing,
Blog, Studio, Assets/Motion), the private image worker, seven interactive frontend entry points,
their development proxies, and the published Marketing artifact connection boundary.
Catalogue remains the active owner-review target; the retained Desk is a distinct app.

REST commands and independent queries are sufficient. A grid consumes its own read projection,
so mutation acknowledgement does not replace that projection. Creation remains acknowledged
when a following read fails; retrying the read must not duplicate the successful creation.
Existing success payloads remain where a workflow needs revision/identity or an operation result.
No GraphQL migration or separate database architecture is needed for this lifecycle.
This is an application of the command/read separation described by
[Microsoft's CQRS pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs),
not an adoption of every infrastructure option in that pattern.

Pending mutations retain the form, confirmed rows and navigation state; action controls show
progress. After success, independent projection reads may show skeletons. Read failure ends
the skeleton, retains last good data and reports through localized, deduplicated toasts.
Uncommitted editor drafts, undo/redo, preview and browser recovery remain immediate local state.

## HTTP error boundary

The reusable backend SDK exposes `AddHttpProblemDetails()` and `UseHttpProblemDetails()`.
The opt-in boundary composes trace-aware error mapping, exception handling, MVC result normalization,
a JSON problem writer and empty-status pages. An outer startup boundary handles framework
HostFiltering failures before the product pipeline. Strict MVC response negotiation and an incompatible
`Accept` header must still produce the error JSON body. Product middleware normalizes its intentional
errors at their owning boundary; success bodies are not buffered or rewritten.

The error contract includes `application/problem+json`, matching HTTP/body status, stable type/title,
safe detail, path instance and trace/request identifiers. Validation fields and authentication,
retry, method and no-store headers remain available. Unhandled exception details remain server-side.
The private Python worker and Vite proxies enforce the same response shape at their own boundaries.

`HEAD` remains bodyless under HTTP semantics. Started streams and disconnected clients cannot safely
acquire a replacement response. Host/proxy failures before application middleware require their own
boundary; a pipeline cannot repair bytes another boundary has already sent.
Protocol source: [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457.html).
Framework integration: [ASP.NET Core error handling](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0).

## Visible request feedback

Catalogue and companion operator apps keep request statuses and server diagnostics out of normal
page/grid/card content. Failed initial reads use neutral unavailable/retry surfaces; failed refreshes
retain the last successful projection. Field validation and components whose actual content is
error records retain their relevant authored messages. Safe interface copy replaces arbitrary
server `detail` or exception text. Catalogue suppresses repeated identical locale/key notices
within its 10-second toast duration; distinct later failures remain reportable.

Failed collection deletion no longer closes the editor. Gallery create/duplicate/activate/archive
wait for acknowledgement before refreshing its list. Product save/upload/restore perform independent
detail reads; confirmed receipts remain available when the follow-up read fails.

## First paint

Catalogue uses light `#f5f6fa` and Neutral graphite dark `#141416` backgrounds. Its previous document
had no early theme background, allowing the browser's white canvas before application CSS loaded.
External blocking appearance script and early stylesheet resolve the stored mode/system preference
before mounting, styling `html`, `body` and the mount node consistently.
Companion apps retain their existing palettes and use equivalent external boot assets.
No inline script/style or relaxed Content-Security-Policy is required.

## Executed verification

| Target | Result |
|---|---|
| Native Catalogue frontend | 111 tests; lint, formatting, typecheck, SFC compilation and production build passed |
| Desk frontend | 258 tests; lint, typecheck, SFC compilation and production build passed |
| Brand frontend | 4 tests; lint, typecheck, SFC compilation and production build passed |
| Marketing frontend | 64 tests; lint, typecheck, SFC compilation and production build passed |
| Editorial frontend | 37 tests; lint, typecheck, SFC compilation and production build passed |
| Studio frontend | 104 tests; lint, typecheck, SFC compilation and production build passed |
| Assets frontend and engine | 6 web tests and 2 engine tests; lint, typecheck, SFC compilation and production build passed |
| SDK Web, final source | 263 tests passed, including complete HTTP error boundary coverage |
| Seven backend suites, isolated source copies | 715 tests passed: Catalogue 55, Brand 9, Desk 184, Marketing 258, Blog 176, Studio 29, Motion 4; temporary ProjectReferences to SDK source, not released package pins |
| Private image worker | 26 tests passed with a fake processor, including interrupted upload capacity recovery |
| Native Catalogue and worker CI | Runs 36977227486 (dbf684a) and 36979645014 (131f083) succeeded |
| Final read-only review | Relay headers/validation/correlation, upload disconnects and six proxy disconnect guards verified; no remaining actionable findings in that scope |

## Published verification

| Repository | Latest verified source commit | Completed CI |
|---|---|---|
| Platform | `5058195` | [36982707109](https://github.com/ocharo-hq/ocharo-platform/actions/runs/36982707109), success |
| Marketing | `1e989ab` | [36984576429](https://github.com/ocharo-hq/ocharo-marketing/actions/runs/36984576429), success |
| Studio | `9f4a35f` | [36982714142](https://github.com/ocharo-hq/ocharo-studio/actions/runs/36982714142), success |
| Assets | `3f22a1a` | [36986342373](https://github.com/ocharo-hq/ocharo-assets/actions/runs/36986342373), success |

Published cohesive batches:

- Platform: `dbf684a`, `66a7c91`, `131f083`, `c352b1c`, `5058195`.
- Marketing: `d23b789`, `1e989ab`.
- Studio: `b85059c`, `9f4a35f`.
- Assets: `156c888`, `7ef657d`, `547713c`, `3f22a1a`.

First-paint tests execute within their owning repositories. Brand's container copies the new proxy
helper and boot assets; its actual frontend Docker stage built successfully. Marketing and Assets
have standalone verification workflows covering their frontend, backend, packaging and container gates.
Marketing public packaging uses explicitly isolated export fixtures and establishes no content publication.
Assets verifies the immutable release, copies verified model inputs before tests, and formats generated
TypeScript through the existing app Prettier configuration. Generated release metadata and source models
remain unchanged. The synchronization, format check and full frontend build passed in that order.

Final frontend total: 586 tests, including the two Assets engine tests. Seven Assets Vue style warnings
remain advisory; chunk-size and Desk's future native Vite config-loader warning are also non-blocking.
Required gates passed without exclusions or weakened assertions. Studio's three hosted browser shards
passed. Local verification receipts are preserved in `.local/verification/http-contracts-20261002/`;
that ignored folder is local evidence, not a committed artifact or production runtime result.

## Delivery and owner review

Native Catalogue frontend commit `dbf684a` and worker commits `66a7c91` / `131f083` are pushed. Companion frontend batches are committed and pushed in all four owning repositories. All four latest app verification runs in the preceding table passed. These runs verify committed code and its current released package pins; they do not include the unpublished shared SDK adoption described below.

The shared SDK's 13-file change is staged, with commit/push flags ON in its owning WoW2 workspace. This chat's peer registry currently admits only Ocharo repositories, so its Git hook rejected the SDK commit. An exact SDK peer-scope proposal is pending human decision; the live registry and all Git flags remain unchanged. Seven actual API package pins therefore remain unchanged. Backend implementation is present locally and validated in isolated source-copy suites; it is not yet built against a published SDK package or committed/pushed. Do not interpret these source-copy results as verification of the real package pins.

Release continuation: register the exact approved peer scope or publish from an authorized WoW2 chat; publish the SDK; verify the release and actual package version; pull the release commit; update the seven owning API package pins and lock files; rerun full suites against the released package; commit/push backend batches and verify CI.

Canonical WoW2 convention and planning edits are local. Six tracked files are staged; unrelated staged launch/port work is preserved. The pre-existing untracked first-paint document remains untracked, preserving its other lane's ownership. Endpoint XML-commenting baseline remains undecided.

Tests use isolated fixtures, temporary databases or TestServer boundaries. No test mutation was sent
to the user's running Catalogue database. The user-owned Rider/WebStorm processes and browser were
not started, restarted or operated by this sweep. Automated checks do not establish visual acceptance,
production deployment or external publication of catalogue content.

WoW2's canonical error, API, frontend-state and first-paint owners were updated. Planning tasks
`con-t-005`, `con-t-006` and `con-t-007` track remaining non-Ocharo adoption. The broader XML endpoint
commenting baseline remains queued for a separate decision.
