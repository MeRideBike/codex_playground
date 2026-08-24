# Reproducible Samples

The samples are intentionally imperfect, standalone targets for future deterministic missions:

- `defective-calculator`: a small failing-test regression and an architecture-advisory target.
- `insecure-greeting`: a focused HTML-output encoding defect.
- `inefficient-reporting`: a focused repeated-enumeration performance defect.

`samples/missions` contains four versioned deterministic mission presets covering each issue. Milestone 1 treats these as reproducible demonstration inputs; it does not yet provide a file-inspection tool to execute them automatically.

ForgeBoard Milestone 1 never executes commands against these or any host directory.
