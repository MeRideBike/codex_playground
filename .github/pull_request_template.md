## Summary

<!-- Explain what changed and why. Link the issue with "Fixes #123" when applicable. -->

## Validation

- [ ] `dotnet build ForgeBoard.sln --configuration Release`
- [ ] `dotnet test ForgeBoard.sln --configuration Release`
- [ ] `npm run build` (when the dashboard changes)
- [ ] Documentation and samples are updated, when applicable.

## Checklist

- [ ] The change is focused and has tests appropriate to its risk.
- [ ] No secrets, private data, or generated build artifacts are included.
- [ ] The dependency direction and security boundaries are preserved.
- [ ] An ADR is included for a significant architectural decision.
