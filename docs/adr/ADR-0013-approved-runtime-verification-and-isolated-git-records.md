# ADR-0013: Approved runtime verification and isolated Git records

## Status

Accepted

## Decision

Permit two narrowly scoped process capabilities for mutable bundled samples only:

1. `dotnet run --project [allowlisted verification project]` in a ForgeBoard-owned run workspace, with a 30-second limit and captured output.
2. Fixed Git operations in that same workspace to create a baseline and, after an explicit human action, one isolated review commit containing only changed manifest files.

Neither capability accepts a command, executable, repository path, Git URL, branch, or arbitrary file path from a user or model.

## Consequences

ForgeBoard can now demonstrate actual executable verification and a reviewable commit hash rather than static evidence alone. Process stdout, stderr, duration, exit code, diff summary, and commit hash are stored as audit-oriented tool results. The capability remains deliberately unsuitable for external repository execution; external checkout, worktree management, and pull-request creation require their own policy and sandbox design.
