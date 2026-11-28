# 00 — Glossary & Naming Conventions

> Controlled vocabulary. These terms are normative: code, contracts, UI and docs use them
> with exactly these meanings. (Naming is a standing project convention, not a style choice.)

## 0.1 Characters & society
| Term | Meaning |
|---|---|
| **Genos** (sg.) / **Genoses** (pl.) | A character in the simulation — NPC or player-controlled. **Never** "sim" for a character. |
| **NPC genos** | A genos not controlled by a player. |
| **Player genos** | The genos a player directs. |
| **Simulation / Simulate** | The *process* of advancing the world. Reserved for the process, never for a character. |
| **Society** | The whole population of genoses and the systems they run under. |
| **Household** | A housing unit + the genoses in it, sharing a home and (usually) money. |
| **Lineage** | A genos and its descendants across generations (ties genoses, genetics and inheritance). |

## 0.2 Space
| Term | Meaning |
|---|---|
| **Tile** | The atomic spatial unit; integer, origin-centred coordinates. |
| **TilePosition** | Authoritative position of a genos/building in tiles (render `Location` is derived). |
| **Lot** | The parcel a road network carves out; interior `(RoadSpacing−1)²`; one building per lot in generated worlds. |
| **Building** | A placed structure (Residential / Office / School / Store / Civic-Shelter) with a validated footprint. |
| **Footprint** | A building's tile extent. |
| **Interior** | The derived room layout inside a placed building, from an authored `InteriorTemplate` per kind. |
| **Room** | A rectangle in a building's local space with a role, capacity and domain binding. |
| **Cutaway** | The roof-removal rendering technique that reveals an interior. |

## 0.3 Time & economy
| Term | Meaning |
|---|---|
| **Tick** | One simulation step. **1,200 ticks = 1 year.** |
| **Cadence** | How often a system runs (every tick / month / pay-cycle). |
| **Money** | Integer-cent currency; every transfer is **ledgered** (conservation applies). |
| **Need** | Hunger / Energy / Hygiene / Bladder — decay and are topped up by actions. |
| **Action** | An activity a genos is currently performing (eating, sleeping, a shift…). |
| **Intent** | A player request from the client, drained at tick start and resolved by the server. |
| **Balance band** | A health target (rent burden, vacancy, Gini, …) — *reported*, not currently retuned. |

## 0.4 Clients & contracts
| Term | Meaning |
|---|---|
| **Browser client** | The PixiJS prototype in `clients/browser`. A **stepping stone**. |
| **Desktop client** | The real renderer: **Godot, C#, desktop-only**. |
| **Wire contract** | `Genelife.Contracts` / `Genelife.Messages` — the only client-shared surface. |
| **Server authority** | The client never mutates the world; it sends intents and renders results. |
| **Replication scope** | One of three: static map / world delta / **scoped interior** (on-request). |
| **Bridge** | The transition from the browser prototype to the desktop client. |

## 0.5 Writing rules
- Prefer **Genos/Genoses**; never "Sim"/"unit"/"agent" for a character.
- "**The simulation**" is the system; "**the society**" is the population; keep them distinct.
- A metric in this GDD must **name its harness and its aggregation** when a number is quoted.
- Do not invent a number: cite a roadmap, a test harness, or mark it an **open question**.
