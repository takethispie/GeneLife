# Layered Architecture Refactor — Status

Split of the backend into `Genelife.Domain`, `Genelife.Application`,
`Genelife.Infrastructure`, and a thin `Genelife.API` presentation layer.

## Result

Status: **complete** (with two deliberate presentation-layer exceptions below).

- `Genelife.Domain` — pure domain model and systems.
- `Genelife.Application` — orchestration, read models, ports, snapshots and the
  simulation engine/manager. No Marten, Wolverine, SignalR, Npgsql or ASP.NET Core.
- `Genelife.Infrastructure` — Marten documents/projections, Marten read model,
  persistence, JWT implementation, event writer, world save/restore.
- `Genelife.API` — endpoints, hub, auth middleware, SignalR broadcast/push
  adapters, `Program.cs` composition root.

Namespaces were intentionally left as `Genelife.*` / `Genelife.Api.*` (see
"Namespaces" below) so the ~80 test files did not need a rename pass.

## Projects

`Genelife.Application/Genelife.Application.csproj`
- `net10.0`, implicit usings, nullable.
- References `Genelife.Domain`, `Genelife.Contracts`, `Genelife.Messages`.
- Packages: `Arch`, `Microsoft.Extensions.Caching.Memory`,
  `Microsoft.Extensions.Hosting.Abstractions`,
  `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options`.
- Verified with `dotnet list package --include-transitive`: no Marten,
  Wolverine, SignalR, Npgsql or ASP.NET Core packages.

`Genelife.Infrastructure/Genelife.Infrastructure.csproj`
- `net10.0`, implicit usings, nullable.
- References `Genelife.Application`, `Genelife.Domain`, `Genelife.Contracts`,
  `Genelife.Messages`.
- Packages: `Marten`, `WolverineFx`, `WolverineFx.Marten`,
  `WolverineFx.SignalR`, `Microsoft.AspNetCore.Authentication.JwtBearer`,
  `Microsoft.Data.Sqlite`, `Microsoft.Extensions.Configuration`,
  `Microsoft.Extensions.Hosting.Abstractions`,
  `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options`,
  `Npgsql`.

Both projects are in `GeneLife.sln`.

## Application ports introduced

Defined in `Genelife.Application/Services`:

- `IWorldPersistence` + `IWorldSaveBatch` — replaces the direct
  `WorldSaveService`/`WorldSaveBatch` dependency in `SimulationEngine` and
  `SimulationManager`. `WorldSaveBatch` (infrastructure document batch)
  implements the marker interface; `WorldSaveService` implements the port
  explicitly.
- `IEventConsumerProbe` — replaces the `IDocumentStore`/`IMessageBus`
  `hasEventConsumer` check in `SimulationEngine.Tick`.
- `IReadModelStore` — replaces the direct Marten `SimDocument`/`CompanyDocument`
  queries in `SimulationManager`. Returns `null` when the store is unavailable so
  the manager keeps its snapshot/live-state fallback.

`IConnectionRegistry` moved to `Genelife.Application/Auth`; the in-memory
`ConnectionRegistry` implementation lives in `Genelife.Infrastructure/Auth`.

Persistence contracts that the application still needs were kept in
`Genelife.Application/Persistence`: `ISnapshotStore`, `PersistedWorldState`,
`SnapshotMigrations`. The SQLite implementation and options
(`SnapshotStore`, `SnapshotStoreOptions`) live in
`Genelife.Infrastructure/Persistence`.

## File moves

Moved to `Genelife.Application`:
- `Commands/` (`AdminCommand`, `CommandQueue`, `ICommandQueue`, `SimCommand`)
- `Repositories/` (all in-memory ECS repositories)
- `Snapshots/` (`AoiFilter`, `ReplicationTracker`, `SnapshotHistory`, `Viewport`,
  `ViewportRegistry`)
- `Consumption/`, `Appearance/`
- `Social/` non-Marten (`ChatDirectory`, `ModerationService`,
  `PlayerDirectoryService`, `SocialReadModel`)
- `Auth/` non-JWT (`AuthConstants`, `AuthorizationResult`,
  `ClaimsPrincipalExtensions`, `CommandAuthorizationService`,
  `IConnectionRegistry`)
- `Services/` non-Marten (`EnqueuedCommand`, `FixedStepScheduler`,
  `HouseholdAssigner`, `IntentRejection`, `JobGrowthSystem`, `Metrics`,
  `NetworkIdRegistry`, `PerformanceThresholds`, `PlayerActionService`,
  `PlayerCommandService`, `PopulationGenerator`, `RequestRateLimiter`,
  `ScalingTriggers`, `SimulationLoop`)
- `SimulationEngine.cs`, `SimulationManager.cs`

Moved to `Genelife.Infrastructure`:
- `Persistence/` (`SnapshotStore`, `SnapshotStoreOptions`)
- `Models/` (Marten documents)
- `Projections/` (Marten projections)
- `Social/` Marten (`ChatStore`, `MartenSocialAuditStore`,
  `ChatRetentionService`, `ModerationRetentionService`)
- `Auth/` JWT (`JwtOptions`, `JwtSigningKeyProvider`, `JwtTokenService`,
  `ConnectionRegistry`)
- `Hosting/DotEnv.cs`
- `Properties/WolverineModule.cs`
- `Services/` (`SimulationEventWriter`, `WorldSaveService`,
  `WorldRestoreService`, plus the new `MartenReadModelStore` and
  `MartenEventConsumerProbe`)

## Deliberate exceptions

Two groups stay in `Genelife.API` because moving them would require the
application/infrastructure assemblies to reference the hub type
(`IHubContext<WorldHub>`) or ASP.NET Core:

- `Handlers/` HTTP command handlers (`CreateSimHandler`, `DeleteSimHandler`,
  `CreateCompanyHandler`, `DeleteCompanyHandler`, `SetSimActionHandler`) — they
  return `IResult`/`Results`, so they are presentation adapters.
- `Handlers/Push/` and `Services/SnapshotBroadcastService` — SignalR adapters
  bound to `WorldHub`.

If these must move later, introduce transport ports (for example an
`IWorldClientBroadcaster`) and implement them in `Genelife.API`.

## API composition root

`Program.cs` still owns host wiring (CORS, Swagger, Marten/Wolverine
configuration, JWT authentication/authorization, SignalR, endpoint mapping) and
now registers the new ports:

```csharp
builder.Services.AddSingleton<WorldSaveService>();
builder.Services.AddSingleton<IWorldPersistence>(sp => sp.GetRequiredService<WorldSaveService>());
builder.Services.AddSingleton<IReadModelStore, MartenReadModelStore>();
builder.Services.AddSingleton<IEventConsumerProbe, MartenEventConsumerProbe>();
```

Duplicate `WorldSaveService`/`WorldRestoreService` registrations were removed.
`Genelife.API.csproj` references `Genelife.Application` and
`Genelife.Infrastructure`; `Microsoft.Data.Sqlite` was removed from the API
package list. Marten/Wolverine/JwtBearer remain because `Program.cs` is the
composition root and configures them directly.

## Verification (2026-11-25)

- `dotnet build GeneLife.sln` — succeeded, 0 errors.
- `Genelife.Domain.Tests` — 567 total, 0 failed.
- `Genelife.Api.IntegrationTests` (temporary PostgreSQL 17 container) — 69
  total, 68 passed, 1 failed:
  `WorldHubTests.Connect_same_player_twice_both_receive_snapshots` observed a
  tick delta of 81 (allowed 0–2). The same test fails identically on unmodified
  HEAD `586d18c`, so it is pre-existing and unrelated to this refactor.
- `Genelife.Performance.Tests` without PostgreSQL — 34 total, 1 failed, 2
  skipped. The failure (`Save_stop_restore_preserves_world`) required the
  database. With PostgreSQL, the full `MultiplayerAcceptanceTests` class
  (4 tests) passed; the rest of the suite was not re-run after the database
  check.
- Project reference graph is acyclic:
  `API -> Infrastructure -> Application -> Domain -> Messages`,
  `Application -> Contracts`.
- `dotnet list Genelife.Application package --include-transitive` reported no
  Marten, Wolverine, SignalR, Npgsql or ASP.NET Core packages.

## Follow-ups

- Re-run integration/performance suites where PostgreSQL is available.
- Optional mechanical pass to align namespaces with assemblies.
- Optional transport-port extraction to move the remaining SignalR/HTTP
  adapters into `Genelife.Infrastructure`.
