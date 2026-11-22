# Balance targets and levers

A realistic population simulation is not a realistic economy. The goal is a world that stays legible and interesting for tens of simulated years, not a macroeconomic model.

## Target bands

These are acceptance ranges for a 10-year default run with 1,000–2,000 NPCs.

| Indicator | Target | Why |
|---|---|---|
| Unemployment | 4–8% | enough friction to matter, not a crisis |
| Job vacancy rate | 2–6% | hiring should take time |
| Vacant housing | 3–8% | mobility without shortage |
| Rent burden | 25–40% of worker income | housing is a real choice |
| Household savings | 1–3 months of expenses | buffer against shocks |
| Homelessness | under 3% | visible but not normal |
| Company failures per decade | 5–20% of firms | turnover without collapse |
| Money supply growth | 0–10% per decade | no runaway inflation |
| Wealth Gini | 0.35–0.55 | inequality, not extreme concentration |

## Primary levers

- **Initial capital** per company and per household.
- **Salary bands** by position and seniority.
- **Recruitment cost** and **severance**.
- **Consumer spending rate** by stage and archetype.
- **Company margin** on consumer revenue.
- **Tax rate** and **pension contribution rate**.
- **Rent** by quality tier and **housing supply** growth.
- **Mortality and health decline** rates.
- **Immigration** response to vacancies and housing availability.
- **Pay cycle length** (300 ticks by default, aligned with current payroll).

## What "realistic" means here

- A worker can afford rent, food and some saving on a normal salary.
- Losing a job creates urgency within one or two pay cycles, not instant ruin.
- A company cannot grow without capital and capacity.
- Retirement income is lower than working income but sufficient with savings.
- Old age brings declining health and eventual death, not a cliff at a fixed age.

## Guardrails

- No system may mint money implicitly.
- Any policy that redistributes money must have a fund and a visible shortfall.
- Balance changes must be configuration-driven and covered by a scenario test.
- Metrics are captured every simulated year and stored for the admin; regressions are visible without reading logs.
