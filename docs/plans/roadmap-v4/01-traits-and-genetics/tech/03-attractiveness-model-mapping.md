# T03 — Attractiveness model mapping and calibration

**Phase:** 3 — Calibration
**Depends:** T01
**Estimate:** 1–2 sessions
**Status:** Done — offline calibration CLI in `tools/attractiveness`; runtime uses pinned integer coefficients

## Goal

Turn the orphaned `AttractivenessService` prototype into an offline calibration tool that produces deterministic C# coefficients for the runtime attraction score. The simulation must not take a `Microsoft.ML` dependency.

## Context

`AttractivenessService` is an ML.NET console project outside `GeneLife.sln`. It reads `Attractiveness.csv`, trains a model, writes `Attractiveness.mlnet` and loads the model from a relative path. Its `.mbconfig` references `D:\Dev\GeneLife\...`.

The prototype is useful as a schema and a calibration oracle, not as a runtime component. An ML model in the tick loop would add a native/model artifact, a heavy dependency and a black-box decision the admin cannot explain.

## Design

### Tool location

Move or add the prototype as a development tool outside the runtime dependency graph:

```text
tools/attractiveness/
  Attractiveness.mlnet
  Attractiveness.mbconfig
  AttractivenessCalibration.csproj
  AttractivenessCalibrationAdapter.cs
  Program.cs
```

The original training CSV is not checked in; the pre-trained `.mlnet` model is
the oracle. Retraining requires the CSV and updates the checked-in model.

It may remain outside `GeneLife.sln`. It must not be referenced by `Genelife`, `Genelife.API` or `Genelife.Contracts`.

### Mapping adapter

Add `AttractivenessCalibrationAdapter` in the tool:

- `Traits` + `Alive.Age` → prototype `ModelInput` columns;
- `HairColor` → prototype `HairType`;
- `Aptitude` → prototype `Intelligence`;
- `Temperament` → prototype `BehaviorPropension`;
- `Sex` → prototype `Sex`;
- Age is read from `Alive`, never from `Traits`.

The adapter is the only place that knows the prototype's column names.

### Calibration output

Produce a versioned fixture:

```json
{
  "version": "v4-attraction-2",
  "scoreScale": 10000,
  "components": {
    "traitCompatibility": 4200,
    "agePreference": 1800,
    "proximity": 1200,
    "stability": 1500,
    "health": 800,
    "availability": 500
  },
  "traitWeights": {
    "eyeColor": 100,
    "hair": 250,
    "handedness": 100,
    "morphotype": 200,
    "aptitude": 200,
    "heightPotential": 150,
    "temperament": 250
  }
}
```

The runtime reads a generated C# default or a JSON fixture copied into `Genelife/Family/AttractionCoefficients.cs`. It never loads the `.mlnet` file.

### Calibration method

1. Generate a synthetic pair sample from `Traits` using the seed generation rules.
2. Label compatibility with the ML model offline.
3. Fit or grid-search integer weights that approximate the labels.
4. Record the fit quality and the fixture version.
5. Pin the fixture in tests; a change requires an explicit version bump.

### Runtime boundary

- `AttractionScorer` receives `Traits`, age, proximity and stability inputs.
- It returns an integer score plus named components.
- It has no `Microsoft.ML` reference and no file I/O.
- The calibration tool is allowed to reference ML.NET and the runtime domain.

## Tasks

- [x] Move the prototype to `tools/attractiveness` or document why it stays in place.
- [x] Add the canonical mapping adapter.
- [x] Replace the hard-coded `D:\Dev\...` path with a repo-relative tool path.
- [x] Add a CLI mode that writes the coefficient fixture.
- [x] Add the runtime coefficient type with a pinned default version.
- [x] Add a drift test that fails if the fixture version and runtime version disagree.
- [x] Document that the model is an oracle, not a tick-loop dependency.

## Oracle boundary

The ML.NET model is an offline oracle only. The simulation never loads
`Attractiveness.mlnet`, never calls `PredictAllLabels`, and never takes a
`Microsoft.ML` dependency at runtime. `AttractionScorer` consumes the pinned
integer coefficients produced by the tool; the fixture is the contract between
the two. Regenerating the model therefore requires an explicit version bump and
a new checked-in fixture, not a change to the tick loop.

## Files

- `tools/attractiveness/*`
- new `Genelife/Family/AttractionCoefficients.cs`
- new `Genelife/Family/AttractionScorer.cs` (skeleton; completed in R04)
- `Genelife.UnitTests/Family/AttractionCalibrationTests.cs`
- `docs/plans/roadmap-v4/02-relationships-and-attraction/tech/04-attraction-score.md`

## Acceptance

- The mapping adapter covers every prototype input column.
- The tool runs outside the API and can regenerate the fixture.
- No runtime project references `Microsoft.ML` or the `.mlnet` asset.
- The coefficient fixture has a version and is checked in.
- A test fails if the runtime coefficients and fixture version drift.

## Testing

- Map a known `Traits` set to the expected prototype row.
- Round-trip the fixture JSON.
- Assert the runtime scorer uses integers and returns the configured component list.
- Assert `Genelife.csproj` and `Genelife.API.csproj` contain no ML.NET package reference.