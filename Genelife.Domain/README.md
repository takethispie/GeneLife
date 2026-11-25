# Genelife - Genos-Style Simulation with Arch ECS

A barebones Genos-style simulation library built with .NET and Arch ECS.

## Features

### Components
- **Needs**: Tracks Hunger, Energy, Hygiene, and Bladder (0-100 scale)
- **CurrentAction**: Tracks what action a Genos is currently performing
- **GenName**: Identifies each Genos

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
- `GET /api/simulation/status` - Get simulation status and all Genoses
- `POST /api/simulation/genoses` - Add a new Genos (body: `{"name": "GenosName"}`)
- `GET /api/simulation/genoses` - Get all Genoses

## Project Structure

```
Genelife/                    (Class Library)
├── Components/
│   ├── Needs.cs
│   ├── CurrentAction.cs
│   └── GenName.cs
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
