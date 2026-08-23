# Contributing to ForgeBoard

Thanks for investing time in ForgeBoard. We welcome bug fixes, documentation improvements, tests, and well-scoped feature proposals.

## Before you begin

- Read the [Code of Conduct](CODE_OF_CONDUCT.md).
- Search existing issues and discussions before opening a new one.
- For substantial changes, open an issue first so maintainers and contributors can agree on scope.
- Never include credentials, private data, or unreviewed generated artifacts in a pull request.

## Local setup

```powershell
dotnet restore ForgeBoard.sln
dotnet build ForgeBoard.sln --configuration Release
dotnet test ForgeBoard.sln --configuration Release
```

For dashboard changes, run `npm ci` and `npm run build` in `src/ForgeBoard.Web`.

## Engineering expectations

- Keep changes focused and easy to review.
- Preserve dependency direction: Domain <- Application <- Infrastructure/API. The Domain project must not depend on EF Core, HTTP, logging, or AI SDKs.
- Add or update tests for behavior changes. Do not weaken existing tests to make a change pass.
- Keep cancellation tokens flowing through I/O and make failures observable in the audit record.
- Use concise, audit-oriented rationale summaries. Do not retain hidden model reasoning.
- Add an ADR in `docs/adr` when making a durable architectural decision.
- Update user-facing documentation when behavior, setup, or configuration changes.

## Pull requests

1. Create a focused branch from `main`.
2. Use an imperative, descriptive title. Conventional Commit prefixes are welcome but not required.
3. Complete the pull-request template and link the relevant issue.
4. Ensure CI is green and resolve review feedback before requesting merge.

Maintainers may ask contributors to split broad changes into smaller pull requests. This keeps the project understandable and the review process healthy.
