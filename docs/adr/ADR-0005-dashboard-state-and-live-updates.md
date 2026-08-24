# ADR-0005: Dashboard state and live updates

## Status

Accepted

## Decision

Use React for dashboard presentation, fetch authoritative mission data from the API, and use SignalR notifications to trigger targeted refetches. The browser holds no orchestration state.

## Consequences

The UI remains replaceable and reconnect-safe, while the database remains the audit source of truth. The initial SignalR event is intentionally small; richer event payloads may be introduced only when profiling shows targeted fetches are insufficient.
