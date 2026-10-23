# ventures.ocharo-brand — Engineering Rules

*Last updated: 2026-10-24*

> Repo-specific operational deltas only. Shared rules: wow-two-ws/conventions/.

- Never write to Maftuna's workspace brand files; seed reads, uploads copy.
- Public endpoints serve published releases only; drafts stay behind the admin API: loopback on a local run, public without sign-in on a deployed host until the identity server lands (owner decision 2026-10-24).
- Keep runtime data and built SPA out of git.
