# Repository and runtime operations

*Last updated: 2026-09-26*

## Initial Git handover

The user requested this order: establish the venture → developer's first commit → enable the native
repository commit switch → scaffold the full project. The first commit contains the documentation
baseline, ignore rules, template audit, and build plan. It contains no generated application code.

Suggested first-commit subject:

```text
docs: established Brand Workspace venture and scaffold plan
```

After making that commit, the user can enable automatic commits by sending this exact standalone
message in the active task:

```text
~commit_on workbench/ventures/ventures.brand-workspace
```

This quoted example does not enable anything. The [shared Git rule][git-rule] states:
“must change records only through the installed native prompt hook, never by editing JSON or replaying events.”
The adapter accepts an exact user directive; the agent cannot switch permission on from prose.
Verify the effective current-task status after the directive. Push and amend remain outside that grant.

## Scaffold continuation

Verify the initial commit and clean task-owned state. Review the [template audit](../research/template-audit.md)
and preserve the initial history during additive import. Register IDE paths and ports only when the
solution and frontend exist. Do not change Studio, the template, or another lane's shared script casually.

## Runtime ownership

The scaffold creates no implicit public service. During implementation, required local runtimes use
persistent managed sessions with identifiable logs. Observe readiness before handing over URLs.
Only stop or restart this task's processes. Native certificate trust remains user-controlled.

[git-rule]: /Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/.codex/commit-permission.md
