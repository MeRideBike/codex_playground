# ADR-0006: Constrained sample capability and provider policy

## Status

Accepted

## Decision

Expose bundled sample evidence through an allowlisted application interface rather than paths, shell commands, or general repository tools. Register the deterministic provider by default; allow OpenAI only through explicit configuration and a supplied API key.

## Consequences

Sample missions are reproducible and testable without host-machine access or external credentials. The provider receives bounded evidence rather than autonomous tools. Adding another sample requires an intentional catalog and file-list change; arbitrary repository integration remains deferred.
