# ADR-0001: Overall architecture

## Status

Accepted

## Decision

Use a layered architecture with Domain, Application, Infrastructure, and API projects. Domain owns invariants; Application owns orchestration contracts; Infrastructure owns EF Core and external adapters; API is the composition root.

## Consequences

Provider and persistence changes do not leak into mission state logic. The extra boundary is limited to meaningful external concerns rather than applying patterns mechanically.
