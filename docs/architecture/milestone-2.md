# Milestone 2 dashboard

The React client is a presentation layer. It reads the server-owned Mission aggregate through `/api/missions` and uses the SignalR `missionUpdated` notification only as a cache-refresh signal; it never decides which workflow node runs next.

The Mission Detail view renders the defined initial workflow order explicitly instead of assuming the database will preserve collection order. This avoids making persistence-provider behavior part of the user-visible workflow contract. Revision nodes are placed between the original approval gate and implementation.

The dashboard exposes the audit-oriented fields that already exist in the API: step input, structured output, concise rationale, provider/model, token and cost values, errors, timestamps, and human decisions. It intentionally does not show or retain hidden reasoning.
