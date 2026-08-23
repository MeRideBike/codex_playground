# ADR-0003: Workflow execution model

## Status

Accepted

## Decision

Model workflow nodes and transitions explicitly, but execute only the initial ordered workflow in Milestone 1. Agent and approval nodes are durable Run Steps; approval stops execution.

## Consequences

The model leaves room for deterministic, conditional, and parallel nodes without prematurely implementing a generic language. New orchestration patterns require a focused execution design.
