# ADR-0010: Per-mission budgets and immutable reviewed operation policy

## Status

Accepted

## Decision

Allow optional token and estimated-cost ceilings on a mission. Persist a capability policy derived from the selected reviewed template at mission creation, and require constrained execution to check that stored policy before performing a capability.

## Consequences

Budget usage and policy are visible in the audit record and are not silently broadened by later template changes. Observed overages stop later workflow stages and retain the costly result for inspection. Because a provider may report usage only after a call completes, this is a post-observation guard rather than a hard provider-side reservation. Provider quotas, signed policies, and per-tool user grants remain future work.
