# ADR-0012: Focused sample environments and change inspection

## Status

Accepted

## Decision

Use a distinct allowlisted sample environment for each mutable showcase scenario: a calculator regression, an HTML-output encoding defect, and a repeated-enumeration performance issue. Expose a read-only comparison between the original allowlisted snapshot and the isolated workspace after a reviewed correction.

## Consequences

Each demonstration has a single, understandable purpose and its audit evidence names only the files relevant to that purpose. The comparison endpoint can reveal only files already present in the sample snapshot and only from the application-owned run workspace; it neither accepts a path from the caller nor executes a command. Advisory missions correctly show no source change.
