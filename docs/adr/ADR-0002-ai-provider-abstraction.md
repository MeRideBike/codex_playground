# ADR-0002: AI provider abstraction

## Status

Accepted

## Decision

Application code uses `IAgentProvider` and receives an `AgentAuditOutput` containing structured result plus audit metadata. Milestone 1 registers `DeterministicFakeAgentProvider` by default.

## Consequences

Tests and local development require no paid account. A real provider can be introduced as a configuration-selected Infrastructure implementation without coupling the workflow to an SDK.
