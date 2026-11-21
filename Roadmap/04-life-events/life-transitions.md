# Life Transitions

Stage transitions are the primary life events in V1. They happen automatically based on age, but each transition triggers cleanup, component swaps, and economic recalculation.

## Stage Progression System (`StageProgressionSystem`)

- **Cadence:** Every 100 ticks (~4 seconds at 25 Hz)
- **Query:** `Alive` + `LifeStage`

### Transition Logic

```csharp
void Update(float deltaTime) {
    var query = new QueryDescription().WithAll<Alive, LifeStage>();
    world.Query(in query, (Entity entity, ref Alive alive, ref LifeStage stage) => {
        LifeStage newStage = DetermineStage(alive.Age);
        if (newStage != stage.Stage) {
            TransitionStage(entity, stage.Stage, newStage, alive);
            stage.Stage = newStage;
        }
    });
}
```

### Age Bands (Constants)

```csharp
const int AgeChildMax     = 5;
const int AgeStudentMin   = 6;
const int AgeWorkerMin    = 18;
const int AgeRetiredMin   = 65;
const int AgeElderMin     = 80;
```

### Transition Handlers

Each transition is a method that handles component add/remove. Methods are idempotent — if called multiple times for the same transition, they don't double-add components.

#### Child → Student (Age 6)

```csharp
void EnterStudent(Entity entity) {
    world.Add(entity, new SkillSet());
    // Student gets a small initial wallet from "parents"
    if (!world.Has<Wallet>(entity))
        world.Add(entity, new Wallet { Balance = 50f });
    world.Add(entity, new SpendingRecord());
}
```

#### Student → Worker (Age 18)

```csharp
void EnterWorker(Entity entity) {
    // Add economic components if missing
    if (!world.Has<Wallet>(entity))
        world.Add(entity, new Wallet { Balance = 500f });
    if (!world.Has<Savings>(entity))
        world.Add(entity, new Savings());
    if (!world.Has<SpendingRecord>(entity))
        world.Add(entity, new SpendingRecord());
    if (!world.Has<Housing>(entity))
        world.Add(entity, new Housing { CurrentTier = HousingTier.Basic });

    // Start job seeking
    var skills = world.Get<SkillSet>(entity).Skills;
    float desiredSalary = ComputeDesiredSalary(skills);
    world.Add(entity, new JobSeeker {
        IsLookingForWork = true,
        DesiredSalary = desiredSalary
    });
}
```

#### Worker → Retired (Age 65)

```csharp
void EnterRetired(Entity entity) {
    // Remove employment
    if (world.Has<Employee>(entity)) {
        var employee = world.Get<Employee>(entity);
        employmentIndex.Remove(entity);
        world.Remove<Employee>(entity);
    }
    world.Remove<JobSeeker, WorkSchedule>(entity);

    // Compute pension
    var wallet = world.Get<Wallet>(entity);
    var savings = world.Get<Savings>(entity);
    var skills = world.Get<SkillSet>(entity);
    float pension = ComputePension(wallet, savings, skills);

    world.Add(entity, new Pensioner { MonthlyPension = pension });
}
```

#### Retired → Elder (Age 80)

```csharp
void EnterElder(Entity entity) {
    world.Remove<Pensioner>(entity);
    world.Add(entity, new Elderly());
}
```

## Transition Validation

After each batch of transitions, `StageProgressionSystem` runs validation:

```csharp
// Invariants that must hold after transitions:
// - No Worker under age 18
// - No Student over age 17
// - No Retired under age 65
// - No Worker has both Employee AND Pensioner
// - No Elder has Pensioner
// - No Student has Employee
```

Violations are logged as errors. In debug builds, throw an exception.

## Edge Cases

### Age Jump from Persistence

When loading a save where a Gene aged past multiple stage boundaries (e.g., saved at age 50, loaded at age 70), `StageProgressionSystem` applies all intermediate transitions in order:

```
Worker (50) → Worker (65) → Retired (65) → Retired (70)
```

The system iterates: checks age 50 (Worker, no change), then once the age field is at 70, it detects Worker→Retired and fires `EnterRetired`. It does not skip stages.

### New Gene Creation

When `PopulationGenerator` creates a Gene at a specific age, it must also set the correct `LifeStage` and initial components. The generator is responsible for calling the appropriate `Enter*` method or setting components directly. `StageProgressionSystem` only handles existing entities — it won't retroactively add components to a freshly spawned Gene that already has the correct stage.

### Zero-Age Genes (Children)

V1 spawns Children but they have minimal behavior. No education, no spending, no wallet. They simply exist as `Alive` + `LifeStage.Child` + `Needs` entities with accelerated aging. At age 6, they transition to Student normally. In future versions, Children will have guardian/parent relationships.

## Acceptance Criteria

- Stage transitions fire automatically at correct age boundaries
- EnterWorker attaches `JobSeeker` with skill-derived salary
- EnterRetired removes all employment components and adds `Pensioner`
- EnterElder removes `Pensioner` and adds `Elderly`
- Transition is idempotent — repeated calls don't duplicate components
- Post-transition invariants hold (no Workers under 18, etc.)
- Loading a save with skipped age bands applies all intermediate transitions