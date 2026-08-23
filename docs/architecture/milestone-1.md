# Milestone 1 architecture and risk assessment

## Scope

Milestone 1 implements a fixed, explicit engineering workflow. It is extensible through `WorkflowNodeDefinition` and transitions, but is not a workflow programming language. The execution service only supports the shipped sequence plus a targeted researcher revision loop.

## Major risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Unreviewable agent behavior | Agents exchange persisted structured artifacts; every invocation becomes a Run Step. |
| Accidental credential or host access | The default provider is deterministic and no command tool exists. |
| Invalid transitions | Mission and RunStep methods enforce state preconditions and are unit-tested. |
| Provider lock-in | Application depends only on `IAgentProvider`; fake output has no account dependency. |
| Lost execution evidence | SQLite persists mission, steps, and approval records; API returns the aggregate. |

## Deliberate limitations

The fake Implementer does not modify files and the fake Test Engineer does not execute tests. These outcomes are clearly represented as artifacts, not fabricated successful work. UI visualization, real providers, migrations, authorization, and production retention policies are future milestones.
