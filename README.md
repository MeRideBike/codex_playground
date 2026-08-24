# ForgeBoard

[![CI](https://github.com/MeRideBike/codex_playground/actions/workflows/ci.yml/badge.svg)](https://github.com/MeRideBike/codex_playground/actions/workflows/ci.yml)
[![CodeQL](https://github.com/MeRideBike/codex_playground/actions/workflows/codeql.yml/badge.svg)](https://github.com/MeRideBike/codex_playground/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

ForgeBoard is an auditable software-engineering workflow platform. It demonstrates explicit agent stages, human approval gates, structured artifacts, and a durable audit trail. It is intentionally not a chat interface and does not conceal workflow decisions behind a single prompt.

> **Project status:** Milestone 10 is a functional, local-first showcase application with a guided mission experience, executable isolated verification, reviewable isolated Git commits, audit export, per-mission budgets, and constrained sample execution. The default provider is deterministic, so the entire project runs without an AI account or credentials.

## What it demonstrates

- A durable, inspectable workflow: `Planner -> Researcher -> Human Approval -> Implementer -> Test Engineer -> Reviewer`.
- Clear architectural boundaries: Domain, Application, Infrastructure, API, and Web UI.
- SQLite-backed missions, step artifacts, approval decisions, and audit metadata.
- A React/Vite dashboard with live SignalR updates.
- Allowlisted sample inspection with file hashes, a reviewed isolated correction, and executable .NET verification with captured process evidence.
- A human-triggered, isolated Git review commit with a recorded commit hash and diff summary; it cannot access an existing user repository.
- Four task-specific bundled templates: failing test, output-encoding security defect, inefficient implementation, and architecture recommendation.
- Downloadable, versioned mission audit exports with an integrity hash and replay guidance; failed steps can be explicitly retried while retaining their earlier error evidence.
- Optional mission token and estimated-cost limits, plus immutable operation policies that constrain reviewed sample capabilities.
- A guided showcase interface that explains the workflow, provides one-click reproducible scenarios, and makes the next human action obvious.
- A provider abstraction that supports future integrations without coupling core workflow logic to an SDK.
- Tests, samples, architecture decision records, Docker support, and GitHub Actions automation.

## Quick start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Node.js 22+ (only for the web dashboard)

```powershell
git clone https://github.com/MeRideBike/codex_playground.git ForgeBoard
cd ForgeBoard
dotnet restore ForgeBoard.sln
dotnet run --project src/ForgeBoard.Api
```

The API listens on `http://localhost:5000` by default. Verify it with:

```powershell
Invoke-RestMethod http://localhost:5000/health
```

In a second terminal, run the dashboard:

```powershell
Set-Location src/ForgeBoard.Web
corepack npm ci
corepack npm run dev
```

Open the URL Vite prints (normally `http://localhost:5173`). The development server proxies API and SignalR traffic to the API.

For a focused product walkthrough, see the [three-minute ForgeBoard showcase](docs/showcase.md).

### Optional OpenAI provider

The default `Fake` provider requires no account. To use the optional OpenAI provider, set credentials outside the repository, then restart the API:

```powershell
$env:ForgeBoard__AgentProvider = "OpenAI"
$env:OpenAI__ApiKey = "your-api-key"
$env:OpenAI__Model = "gpt-5.4"
dotnet run --project src/ForgeBoard.Api --urls http://localhost:5000
```

The provider uses OpenAI's `POST /responses` endpoint with `store: false`, asks for structured audit output, and records returned token usage when available. It never stores an API key or hidden model reasoning. See the official [Responses API reference](https://developers.openai.com/api/reference/cli/resources/responses/methods/create).

## API walkthrough

Create a mission. It runs through research and pauses at the human approval gate.

```powershell
$mission = Invoke-RestMethod -Method Post -ContentType 'application/json' `
  -Uri http://localhost:5000/api/missions `
  -Body '{"title":"Fix calculator regression","description":"Diagnose the failing calculator sample and propose a safe fix."}'
$mission.id
```

Approve it to continue the remaining workflow:

```powershell
Invoke-RestMethod -Method Post -ContentType 'application/json' `
  -Uri "http://localhost:5000/api/missions/$($mission.id)/approval" `
  -Body '{"outcome":"Approved","comment":"The plan is sufficiently scoped. Proceed."}'
```

Available endpoints:

| Endpoint | Purpose |
| --- | --- |
| `GET /health` | Liveness check |
| `GET /api/missions` | List persisted missions |
| `GET /api/missions/{id}` | Inspect a mission and its audit trail |
| `POST /api/missions` | Start a new mission |
| `POST /api/missions/{id}/approval` | Approve, reject, or request revision |
| `POST /api/missions/{id}/retry` | Retry a failed run step while preserving its prior error record |
| `POST /api/missions/{id}/isolated-commit` | Create one human-triggered Git commit in the completed run's isolated workspace |
| `GET /api/missions/{id}/audit-export` | Download a versioned JSON audit export |
| `/hubs/missions` | SignalR endpoint for mission updates |

## Development

```powershell
dotnet build ForgeBoard.sln --configuration Release
dotnet test ForgeBoard.sln --configuration Release

Set-Location src/ForgeBoard.Web
corepack npm ci
corepack npm run build
```

To run the API in a container:

```powershell
docker compose up --build
```

## Architecture

```text
Web UI / HTTP / SignalR
          |
          v
      Application  <-- workflow use cases and contracts
          |
          v
        Domain     <-- state transitions and invariants
          ^
          |
   Infrastructure  <-- SQLite and provider implementations
```

Agent outputs are structured artifacts. The audit record captures input, output, concise rationale summary, provider/model, tool metadata, error state, retry count, token usage, and estimated cost when available. ForgeBoard never requests or retains hidden model reasoning.

Read the [Milestone 1 architecture](docs/architecture/milestone-1.md), [Milestone 2 dashboard design](docs/architecture/milestone-2.md), [Milestone 3 constrained capability](docs/architecture/milestone-3.md), [Milestone 4 execution capability](docs/architecture/milestone-4.md), [Milestone 5 mission templates](docs/architecture/milestone-5.md), [Milestone 6 audit export and recovery](docs/architecture/milestone-6.md), [Milestone 7 budgets and policies](docs/architecture/milestone-7.md), [Milestone 8 guided showcase](docs/architecture/milestone-8.md), [Milestone 9 showcase hardening](docs/architecture/milestone-9.md), [Milestone 10 executable verification](docs/architecture/milestone-10.md), and [ADRs](docs/adr) for the decisions behind this layout.

## Security and responsible use

ForgeBoard does not execute arbitrary commands, access arbitrary repositories, or require credentials. It runs only fixed `dotnet run` and Git operations against application-owned copies of bundled, allowlisted samples; the command, project, paths, remote, and branch are never user- or model-controlled. Future providers must be configured with environment variables or user secrets, never committed configuration. Please read [SECURITY.md](SECURITY.md) before opening a security report.

## Contributing

Contributions are welcome. Start with [CONTRIBUTING.md](CONTRIBUTING.md), follow the [Code of Conduct](CODE_OF_CONDUCT.md), and use the supplied issue and pull-request templates. Significant architectural changes should include an ADR.

## Roadmap

See [ROADMAP.md](ROADMAP.md) for intentionally deferred capabilities and planned design milestones.

## License

ForgeBoard is released under the [MIT License](LICENSE).
