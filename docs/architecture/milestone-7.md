# Milestone 7 mission budgets and immutable operation policies

A user may set an optional maximum token count and estimated-cost limit when starting a mission. ForgeBoard checks observed accumulated usage before a new step starts and again after a provider returns usage. If a provider crosses a limit, the step retains its output and observed usage but is marked failed; no later step starts. This makes the overage visible instead of discarding evidence. The deterministic provider reports zero usage and cost, so the local sample workflows remain reproducible without credentials.

Every bundled template defines an explicit capability list. ForgeBoard serializes that list into the mission at creation time, displays it in the audit detail, and passes the persisted list to the constrained executor. The executor checks the list before creating an isolated workspace, applying a patch, or asserting a result. Changing the template catalogue later therefore does not expand the permission set of an existing mission.

These controls are scoped to the reviewed local capabilities. They do not yet provide provider-side reservations, signed policy documents, or arbitrary tool authorization.
