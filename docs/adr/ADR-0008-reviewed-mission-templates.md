# ADR-0008: Reviewed mission templates select constrained operations

## Status

Accepted

## Decision

Represent the bundled engineering scenarios as an application-level reviewed template catalogue. Persist the chosen template on a mission and use it to select deterministic research, implementation, and verification behavior for its allowlisted sample. Reject template/sample mismatches instead of attempting to infer a safe action from free-form text.

## Consequences

Each demo mission now has task-specific evidence and audit output, while retaining human approval before an operation can change an isolated workspace. The catalogue is intentionally finite and easy to inspect. Adding a template is a code and review change, not a prompt-only expansion; arbitrary repository and command capabilities remain out of scope.
