# ADR-0007: Reviewed deterministic sample execution

## Status

Accepted

## Decision

After a human approves a mission against a supported bundled sample, execute its implementation and verification stages through a deterministic, narrowly scoped capability rather than an AI provider. The capability may copy the supplied allowlisted snapshot into an application-owned run directory, apply one reviewed correction, and perform defined static assertions. It cannot read arbitrary paths, execute commands, modify `/samples`, or choose a patch from model output.

## Consequences

The completed mission demonstrates an actual auditable change and verification result without granting the application general code-execution authority. The behavior is reproducible without credentials and is straightforward to test. Supporting additional mutations requires deliberately extending the catalog and reviewed rules; broader repository, worktree, and command execution remain future work.
