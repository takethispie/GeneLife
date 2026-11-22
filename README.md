# GeneLife

NPC population simulation

## V1 Status
- Economy, housing and life-stage systems are implemented.
- See [docs/plans/roadmap-v1/STATUS.md](docs/plans/roadmap-v1/STATUS.md) for verification and known limitations.

## Implemented Feature
- Very basic human eat-shower-sleep cycle
- work simulation with companies, job posting and application, revenue and hiring
  based on productivity
- Grocery store minimal implementation
- houses, store, offices building
- full NPC lifecycle: education, work, retirement, health, mortality, estates and population inflow

## Planned Features
- relationship between NPC with an attraction algorithm
- genetic material with reproductive capabilities (NPC can have children)
  merging DNA in a semi-realistic way
- water, electricity consumption
- advanced grocery system

## Testing

Start PostgreSQL for the integration suite:

```bash
docker compose up -d postgres
```

Run the full suite:

```bash
dotnet build GeneLife.sln
dotnet run --project Genelife.UnitTests/Genelife.UnitTests.csproj --no-build --no-launch-profile
```

Tests use isolated Marten schemas and snapshot files, so repeated local runs do not share read-model state.
