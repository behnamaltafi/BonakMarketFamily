## Summary

Describe the requirement, change and explicitly out-of-scope modules.

## Native Reuse Audit (required)

Read [the repository rules](../AGENTS.md) and [the native reuse checklist](../dev-docs/framework/web-api/native-reuse-audit.md). For each acceptance criterion, cite inspected symbols and paths from the target branch.

| Requirement | Native source / symbol | Reuse / Adapter / Extend / New | Gap and justification | Verification |
|---|---|---|---|---|
| | | | | |

- [ ] Relevant native admin/public workflows, services, query extensions, domain, settings, permissions and related modules were inspected.
- [ ] No parallel business rules, data sources, prices, settings or tables were introduced where a native owner exists.
- [ ] Differences from native behavior and any scope expansion are documented; required approval is recorded.

## Verification

- [ ] Build and relevant unit/contract tests executed.
- [ ] SQL translation, provider integration, store/customer isolation and ACL verified when applicable.
- [ ] Admin configuration, localization and frontend contract compatibility verified when applicable.
- [ ] Final diff matches approved scope.

List test commands, results, blockers and any unexecuted verification here. Do not mark checks as passed without evidence.
