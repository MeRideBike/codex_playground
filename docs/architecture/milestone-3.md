# Milestone 3 constrained engineering capability

Milestone 3 permits source inspection only through the `ISampleWorkspace` contract. The infrastructure implementation has a fixed catalog and a fixed file allowlist for each catalog entry. It accepts neither host paths nor command strings, reads only the bundled sample files, and records a SHA-256 hash for every observed file.

The workflow passes the in-memory snapshot to the selected provider, while each step persists only the sample identifier, paths, and hashes in its tool-results audit record. The deterministic provider uses the snapshot to identify the calculator defect without executing code or fabricating a file change.

`OpenAiAgentProvider` is optional and selected only by `ForgeBoard:AgentProvider=OpenAI` together with `OpenAI:ApiKey`. It uses the Responses API with response storage disabled, requests a strict audit payload, and records provider/model/token fields but never API credentials or hidden reasoning. The default remains the deterministic fake provider.
