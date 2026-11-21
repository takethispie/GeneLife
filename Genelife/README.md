# Genelife - Sims-Like Simulation with Arch ECS

A barebones Sims-like simulation library built with .NET and Arch ECS.

## Features

### Components
- **Needs**: Tracks Hunger, Energy, Hygiene, and Bladder (0-100 scale)
- **CurrentAction**: Tracks what action a Sim is currently performing
- **SimName**: Identifies each Sim

### Systems
- **NeedsDecaySystem**: Decreases needs over time at different rates
- **DecisionSystem**: Makes autonomous decisions based on critical needs (< 30)
- **ActionSystem**: Executes actions and restores needs

### Actions
- **Idle**: Default state
- **Sleeping**: Restores Energy
- **Showering**: Restores Hygiene
- **Eating**: Restores Hunger
- **UsingToilet**: Restores Bladder

## Running the Web API

```bash
cd Genelife.Api
dotnet run
```

Then access the Swagger UI at: https://localhost:5001/swagger

### API Endpoints

- `POST /api/simulation/start` - Start the simulation
- `POST /api/simulation/stop` - Stop the simulation
- `GET /api/simulation/status` - Get simulation status and all Sims
- `POST /api/simulation/sims` - Add a new Sim (body: `{"name": "SimName"}`)
- `GET /api/simulation/sims` - Get all Sims

## Project Structure

```
Genelife/                    (Class Library)
├── Components/
│   ├── Needs.cs
│   ├── CurrentAction.cs
│   └── SimName.cs
└── Systems/
    ├── NeedsDecaySystem.cs
    ├── ActionSystem.cs
    └── DecisionSystem.cs

Genelife.Api/               (Web API)
├── Controllers/
│   └── SimulationController.cs
├── Services/
│   └── SimulationManager.cs
└── Program.cs
```
