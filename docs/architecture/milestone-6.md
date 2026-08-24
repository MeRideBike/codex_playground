# Milestone 6 audit portability and workflow recovery

ForgeBoard can export any persisted mission as a `forgeboard.audit.v1` JSON document. The document includes the mission, steps, approvals, observable artifacts, concise rationale summaries, and replay guidance. It deliberately excludes hidden model reasoning. A SHA-256 digest covers the serialized payload and is included with the export so a recipient can detect changes when using the same serialization contract.

The dashboard exposes this through **Export audit**. It is a download-only capability and does not transmit data to an external service.

When a workflow step fails, the mission is marked failed with its error recorded. A user may explicitly retry the failed step. The step's retry count increases and its previous error stays in the audit record even if the later attempt succeeds. ForgeBoard never automatically retries model or deterministic operations: retry remains a human-controlled recovery decision.
