# ADR-0004: Audit and observability strategy

## Status

Accepted

## Decision

Persist audit-oriented mission, step, and approval data in SQLite. Use structured logging and OpenTelemetry tracing at the HTTP boundary. Store concise rationale summaries, never hidden chain-of-thought.

## Consequences

Each mission is inspectable after completion. Production exporters, retention, and privacy policy require a later operational design.
