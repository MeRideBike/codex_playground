# ForgeBoard agent instructions

## Architecture boundaries

- `src/ForgeBoard.Domain` contains business concepts and state invariants only; it must not reference EF Core, HTTP, logging, or AI SDKs.
- `src/ForgeBoard.Application` contains workflows/use cases and provider or persistence interfaces.
- `src/ForgeBoard.Infrastructure` implements external concerns such as EF Core and AI providers.
- `src/ForgeBoard.Api` composes dependencies and exposes transports.
- Keep React code in `src/ForgeBoard.Web`; do not move orchestration decisions into the UI.

## Engineering expectations

Run `dotnet build ForgeBoard.sln` and `dotnet test ForgeBoard.sln` for every .NET behavior change. Add focused tests; do not weaken existing tests. Keep cancellation tokens flowing through I/O. Log structured failures and make them visible in the audit record. Do not retain hidden chain-of-thought—only concise audit-oriented rationale summaries.

## Security and source control

Never commit keys, tokens, connection strings containing secrets, local databases, or user secrets. External integrations stay behind interfaces. No arbitrary host command execution is permitted without a reviewed capability and policy design. Keep commits focused; do not reformat unrelated code or overwrite user work.

## Useful commands

```powershell
dotnet restore ForgeBoard.sln
dotnet build ForgeBoard.sln
dotnet test ForgeBoard.sln
dotnet run --project src/ForgeBoard.Api
```
