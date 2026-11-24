# GeneLife

NPC population simulation

## V1 Status
- Economy, housing and life-stage systems are implemented.
- See [docs/plans/roadmap-v1/STATUS.md](docs/plans/roadmap-v1/STATUS.md) for verification and known limitations.

## V3 Browser Client
- Playable browser client at [`clients/browser`](clients/browser/README.md): PixiJS rendering, SignalR replication, interpolation, viewport filtering, input, HUD and reconnect recovery.
- See [docs/plans/roadmap-v3/STATUS.md](docs/plans/roadmap-v3/STATUS.md) for verification and known limitations.

Run the whole stack (PostgreSQL, API, web and the Vite client) with Aspire:

```bash
dotnet run --project GeneLife.AppHost
```

The AppHost launches the browser client on `http://localhost:5173` and injects
`VITE_API_BASE_URL` from the API's external endpoint, so no local `.env` is needed.

## Implemented Feature
- Very basic human eat-shower-sleep cycle
- work simulation with companies, job posting and application, revenue and hiring
  based on productivity
- Grocery store minimal implementation
- houses, store, offices building
- full NPC lifecycle: education, work, retirement, health, mortality, estates and population inflow

## Planned Features
- relationship between NPCs with an attraction algorithm — planned in [Roadmap V4](docs/plans/roadmap-v4/README.md)
- genetic material with reproductive capabilities (NPC can have children), merging DNA in a semi-realistic way — planned in [Roadmap V4](docs/plans/roadmap-v4/README.md)
- water, electricity consumption
- advanced grocery system

## Testing

The suite is split by execution tier:

- `Genelife.Domain.Tests` — fast tests, no PostgreSQL or API host.
- `Genelife.Api.IntegrationTests` — API host, Marten and PostgreSQL.
- `Genelife.Performance.Tests` — load harness, performance guardrails and long-horizon scenarios.

Start PostgreSQL for the integration suite:

```bash
docker compose up -d postgres
```

Run the fast suite:

```bash
dotnet build Genelife.Domain.Tests/Genelife.Domain.Tests.csproj
dotnet run --project Genelife.Domain.Tests/Genelife.Domain.Tests.csproj --no-build --no-launch-profile -- \
  -trait- "Category=Scenario"
```

Run the API integration suite:

```bash
dotnet build Genelife.Api.IntegrationTests/Genelife.Api.IntegrationTests.csproj
dotnet run --project Genelife.Api.IntegrationTests/Genelife.Api.IntegrationTests.csproj --no-build --no-launch-profile --
```

Run the performance and long-horizon suite:

```bash
dotnet build Genelife.Performance.Tests/Genelife.Performance.Tests.csproj
dotnet run --project Genelife.Performance.Tests/Genelife.Performance.Tests.csproj --no-build --no-launch-profile --
```

Tests use isolated Marten schemas and snapshot files, so repeated local runs do not share read-model state.

Browser client checks:

```bash
cd clients/browser
npm ci
npm run typecheck && npm run lint && npm run test && npm run build
```

See the [client README](clients/browser/README.md) for fixture mode, live E2E and the soak script.
