# F03 — Pregnancy progression and health

**Phase:** 3 — Pregnancy
**Depends:** F01, F02, `HealthSystem`, `HouseholdFinanceService`
**Estimate:** 2 sessions
**Status:** Planned (V4)

## Goal

Progress a pregnancy month by month, price prenatal care, model a small complication risk, and resolve loss or readiness for birth deterministically.

## Context

`Health` is a long-term value in `[0, 100]` with a decline rate and chronic conditions. `HouseholdFinanceService.TryDebit` already draws wallet then savings. Medical care is purchased through the existing health flow.

## Design

### System

`PregnancySystem` at order 375, cadence 100:

1. Collect active pregnancies; skip `Completed`, `Lost` and `Cancelled`.
2. If `tick >= DueTick`, set `ReadyToBirth` and let `BirthSystem` handle it.
3. Otherwise:
   - charge `MonthlyPrenatalCareCost` through the household finance service to the government/health provider;
   - if the household cannot pay, record care arrears and reduce care quality;
   - recalculate risk from age, health, care paid and previous complications;
   - roll a complication/loss with `reproduction.risk`, keyed by pregnancy id and month;
   - on loss, set `Lost`, emit `PregnancyLost`, start a cooldown;
   - otherwise update `LastProgressTick` and emit `PregnancyProgressed`.
4. Never reduce the carrying parent's health below zero; the health system owns death.

### Risk model

Start from a base risk, then add:

- age above `MaximumConceptionAge - 10`;
- health below 70;
- missed prenatal care months;
- each prior complication.

Clamp to `[0, 1000]` basis points. The risk is displayed, not hidden.

### Work and school

- V4a does not add maternity leave or a separate pregnancy schedule.
- Attendance and health effects remain in the existing systems.
- If a future leave system is added, it reads `Pregnancy` rather than adding another field to `Lifecycle`.

### Money conservation

- Prenatal care is a transfer from the household to the provider (government or company), never a mint.
- Unpaid care is arrears, not negative money.
- The balance scenario must remain green.

### Loss and history

- A lost pregnancy stays as history and is visible in the admin.
- The family can conceive again after the cooldown.
- The loss event includes the risk and care state for audit.

## Tasks

- [ ] Add `PregnancySystem` at order 375.
- [ ] Add prenatal care charge and arrears.
- [ ] Add risk calculation and complication/loss rolls.
- [ ] Add due handling and `ReadyToBirth`.
- [ ] Emit progress, complication and loss events.
- [ ] Add cooldown integration.
- [ ] Add health and conservation tests.

## Files

- new `Genelife/Systems/PregnancySystem.cs`
- new `Genelife/Family/PregnancyRisk.cs`
- `Genelife/Family/ReproductionOptions.cs`
- `Genelife/Housing/HouseholdFinanceService.cs`
- `Genelife.UnitTests/Family/PregnancyTests.cs`

## Acceptance

- A pregnancy progresses monthly and becomes ready at `DueTick`.
- Prenatal care is charged once per month and is ledgered.
- A household with no funds accrues care arrears and higher risk, not negative money.
- Risk is bounded and explainable.
- A loss sets the status, emits an event and starts a cooldown.
- The same seed produces the same progression and outcome.

## Testing

- Run a pregnancy to term with full care.
- Run one with no funds and assert arrears and risk.
- Force a loss and assert the cooldown.
- Replay the same pregnancy id and compare.
- Assert total money is unchanged by care transfers.