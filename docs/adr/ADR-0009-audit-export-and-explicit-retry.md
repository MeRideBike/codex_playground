# ADR-0009: Versioned audit export and explicit failed-step retry

## Status

Accepted

## Decision

Provide a local JSON export for persisted mission audit records, including a schema identifier, replay guidance, and a SHA-256 integrity digest. Permit retry only after an explicit user action on a failed mission, and retain the previous error while incrementing the step retry count.

## Consequences

Audit records can be shared or retained outside the SQLite database without claiming to preserve hidden reasoning. The digest offers a simple integrity check for the exported payload, not a signature or non-repudiation guarantee. Human-triggered retry makes recovery visible and avoids silent or uncontrolled repeat provider calls. Fine-grained retry policy, budgets, signatures, and full replay remain future work.
