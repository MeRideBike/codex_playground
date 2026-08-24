# Milestone 5 reviewed engineering mission templates

Milestone 5 makes the bundled engineering scenarios explicit rather than inferring behavior from free-form mission text. `IEngineeringMissionCatalog` contains four reviewed templates for the allowlisted `defective-calculator` sample:

- Diagnose the failing calculator test.
- Identify and correct unencoded user-controlled HTML output.
- Find and replace repeated prefix enumeration with a linear accumulation.
- Recommend a presentation-boundary improvement without changing source.

The selected template is persisted with the mission, included in the planner and researcher inputs, shown in the dashboard, and used to select the deterministic implementation and verification rule after human approval. The API rejects a template/sample mismatch. A sample mission without an explicit template retains the existing failing-test template for backward-compatible local development.

Templates are a small reviewed catalogue, not a general workflow language or arbitrary code-modification interface. Adding one requires catalog, executor, test, and ADR updates.
