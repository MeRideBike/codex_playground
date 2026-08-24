# Milestone 4 constrained implementation and verification

For the `defective-calculator` sample, approval releases two deterministic workflow operations. The Implementer creates an isolated copy beneath the application-owned run-workspace root and changes only the reviewed `Calculator.Subtract` expression from `left + right` to `left - right`. The original `/samples` tree remains read-only.

The Test Engineer then performs explicit static assertions over the isolated source and its existing test expectation. It does not run a process, compile code, invoke a shell, or accept an arbitrary command. Both operations record structured output, concise audit rationale, tools invoked, assertions or file hashes, zero token usage, and zero estimated cost.

For missions without an allowlisted sample, the existing provider-backed Implementer and Test Engineer behavior remains unchanged. Adding another executable sample requires a reviewed executor rule, an allowlisted catalog entry, tests, and an ADR update; no general mutation or execution capability is implied.
