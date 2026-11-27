# Godot client — C#, not GDScript, and desktop-only

**Author:** @roberto (Solution Researcher) · **Date:** 2026-11-27 · **Branch:** `docs/roberto/godot-client-decision`
**Decided by:** the product owner, 2026-11-27 — direct instruction, not raised as a board card
**Status:** decided and binding for the next Godot client work
**Constrained by:** [Roadmap V3](../plans/roadmap-v3/README.md) (*"no client-authoritative simulation"*) and the
standing deprioritisation of balance and performance work

---

## 1. Ruling

1. **The Godot desktop client is written in C#.** Not GDScript.
2. **The client is desktop-only.** No web export, no second browser implementation. The browser client remains
   the PixiJS client from V3.
3. **GDScript is allowed only as thin tooling glue** — editor plugins, import/build scripts. Never client logic.

## 2. Why C#, and not GDScript

**The client shares the wire contract, not the simulation.** V3 fixed the architecture: the browser client was
built on *"the V2 server and replication contracts"* with *"no client-authoritative simulation"*. The Godot client
inherits that rule, so it must not reference `Genelife.Domain` or `Genelife.Application` — doing so invites a second
simulation inside the client, which is precisely what V3 forbade.

What it *should* share is `Genelife.Contracts` and `Genelife.Messages` (both `net10.0`). In C# that is a project
reference: the wire format becomes a **compile-time** fact, so a server-side DTO change that the client has not
absorbed is a build error. In GDScript the same message would be re-declared as `Dictionary` keys and drift
silently — a class of bug that only shows up as a runtime field-name mismatch in a running game.

Secondary reasons, both cheaper than they sound:

- **One language across the stack.** The Roslyn/memanto code graph, `dotnet format`, the existing analyzers and the
  .NET test idioms all apply to the client with no second toolchain to configure per agent.
- **The AI tooling is C#-first.** gdUnit4 ships [GdUnit4Net](https://github.com/godot-gdunit-labs/gdUnit4Net) with
  VS/Rider adapters, and the most mature editor bridge
  ([IvanMurzak/Godot-MCP](https://github.com/IvanMurzak/Godot-MCP)) has a Script family that reads/writes `.cs`
  *and builds the C# project* — an agent can therefore verify its own C# work, not just write it.

**Costs accepted:** the Godot **.NET edition** is mandatory (not the standard build); C# has a build step, so script
changes do not hot-reload the way GDScript does; and public Godot+C# examples are scarcer than GDScript ones, which
raises the cost of the first vertical slice.

## 3. Why desktop-only

This is partly imposed and partly chosen:

- **Imposed.** C# projects in Godot 4 *"currently cannot be exported to the web platform"*
  ([C# docs](https://docs.godotengine.org/en/4.7/tutorials/scripting/c_sharp/c_sharp_basics.html)). A C# Godot web
  build does not exist to ship, so "desktop-only" is the honest description of what is available.
- **Chosen.** The web target is already served by the PixiJS client. A second web implementation would be a
  duplicate surface with no user-facing gain, and would double the contract-drift risk the shared DTOs exist to
  remove.

Consequence: the export pipeline covers desktop targets only, and there is no wasm/web export job to maintain.

## 4. What this commits us to

| Area | Commitment |
|---|---|
| Engine | Godot 4.7 **.NET (mono)** build, plus a separately installed .NET SDK (Godot bundles the runtime, not the compiler) |
| Target framework | The Godot project targets `net8.0` **or newer**; it should be `net10.0` to match the 15 existing `*.csproj` in this repo, which is what makes `Genelife.Contracts`/`Genelife.Messages` referencable |
| Scenes | Stay text (`.tscn`/`.tres`) so agent edits are reviewable diffs; `.godot/` stays gitignored |
| Tests | gdUnit4 with C# tests; headless needs the two-step run (`--import --quit`, then run with `--ignoreHeadlessMode`) or it can segfault |
| CI | Desktop exports via the `godot-ci` image; the existing four required checks are unchanged, with a `godot-smoke` job added when the client exists |

## 5. Open risk — RESOLVED (verified PASS, see §8)

> **Resolved 2026-11-27 — verdict PASS.** The spike in §8 proved all three conditions below on this Windows host,
> so this risk no longer applies. Running `net10.0` is confirmed; the `net8.0` fallback is not needed. The recipe is
> retained for the record.

**Godot's host ships .NET 8.** Targeting `net10.0` is *permitted* by the engine's own rule (projects may target
`net8.0` or newer) but it is **not proven here**, and this record does not claim otherwise. Before the client work
leans on it, a spike must show, on this Windows host:

1. a Godot 4.7 .NET project whose `TargetFramework` is `net10.0` opens and builds in the editor;
2. it references `Genelife.Contracts` and instantiates a DTO in a headless scene;
3. a **desktop export** of that project runs, with the `net10.0` assembly resolved at runtime.

**If it fails:** the fallback is multi-targeting the shared contract assemblies to `net8.0`. The two rulings in §1
survive untouched — only the target framework number changes.

## 6. What this does NOT decide

- **No implementation starts.** The client does not exist; this record authorises nothing beyond the two rulings.
- Not decided: the Godot MCP server (several community options, no official one), scene/renderer architecture,
  transport specifics beyond "reuse the V2 replication contract", export platform order, CI job names, timeline.
- No bearing on balance or performance work — both remain deprioritised by the standing ruling.

## 7. Sources

- [C# basics — Godot 4.7 docs](https://docs.godotengine.org/en/4.7/tutorials/scripting/c_sharp/c_sharp_basics.html)
  — .NET requirement (*"Godot 4.5 requires .NET 8 or later"*) and the no-web-export limitation for C#.
- [GodotSharp 4.7.2 on NuGet](https://www.nuget.org/packages/GodotSharp/) — package targets `net8.0`, compatible
  with higher.
- [Godot C# packages move to .NET 8](https://godotengine.org/article/godotsharp-packages-net8/) — *"Starting with
  Godot 4.4, your project will need to target .NET 8 or newer."*
- [IvanMurzak/Godot-MCP](https://github.com/IvanMurzak/Godot-MCP) — requires the .NET build of the editor, recommends
  the .NET 10 SDK; Script family builds the C# project.
- [GdUnit4](https://github.com/godot-gdunit-labs/gdUnit4) / [GdUnit4Net](https://github.com/godot-gdunit-labs/gdUnit4Net)
  — GDScript **and** C# test support.
- [Roadmap V3 — Browser Game Client](../plans/roadmap-v3/README.md) — the no-client-authoritative-simulation rule
  this record inherits.
- Repo state at time of writing: 15 `*.csproj`, all `<TargetFramework>net10.0</TargetFramework>`.
- Negative result worth keeping: **no official Godot MCP server and no `llms.txt`**
  (`docs.godotengine.org/llms.txt`, `llms-full.txt`, `godotengine.org/llms.txt` all return 404, checked 2026-11-27).

## 8. Verification result — PASS (spike, 2026-11-27)

Card `t_dacf9eda` executed the §5 recipe on this Windows host. **Verdict: PASS** — a Godot 4.7.2 .NET project
targeting `net10.0` builds, references `Genelife.Contracts` (`net10.0`), instantiates a DTO in a headless scene, and
a **desktop export runs** with the `net10.0` assembly resolved at runtime. No fallback is needed; the shared
contract assemblies keep targeting `net10.0`.

**Raw evidence**

| Item | Value |
|---|---|
| Godot | `4.7.2.stable.mono.official.ed1daf0bf` (.NET/mono build, from `--version`) |
| .NET SDKs installed | `10.0.100`, `10.0.404` (`dotnet --list-sdks`) — **only .NET 10**; no .NET 8 SDK on this host |
| .NET runtime at play | `Microsoft.NETCore.App 10.0.15` (`RuntimeInformation.FrameworkDescription` → `.NET 10.0.15`) |
| Project SDK | `<Project Sdk="Godot.NET.Sdk/4.7.2">` |
| `TargetFramework` | `net10.0` (the editor import left it unchanged) |
| Contract reference | `<ProjectReference Include=".../Genelife.Contracts/Genelife.Contracts.csproj" />` |

Steps run, all succeeded:

1. `dotnet build` →

   ```
   Genelife.Contracts -> ...\net10.0\Genelife.Contracts.dll
   GenelifeNet10Spike -> ...\.godot\mono\temp\bin\Debug\GenelifeNet10Spike.dll
   Build succeeded.
   ```

2. `Godot --headless --path . res://Main.tscn` →

   ```
   [SPIKE] Godot version = 4.7.2-stable (official)
   [SPIKE] Runtime = .NET 10.0.15
   [SPIKE] Contracts assembly = Genelife.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
   [SPIKE] DTO instantiated: NetworkId=42 Name=Spike Corp Employees=3 Balance=1234.56 Solvency=Solvent
   [SPIKE] Second DTO instantiated: Genelife.Api.DTOs.SnapshotInfo
   [SPIKE] PASS
   ```

3. `Godot --headless --path . --export-release "Windows Desktop" build/spike.exe` → export **succeeded**
   (`dotnet_publish_project` ran, `savepack` completed). The export bundle
   `data_GenelifeNet10Spike_windows_x86_64/` contains both `Genelife.Contracts.dll` and the full .NET 10 runtime
   (`coreclr.dll`, `System.Private.CoreLib.dll`, `mscordaccore_amd64_amd64_10.0.1526.53008.dll`).

4. Running the exported `spike.exe` on this host →

   ```
   [SPIKE] Runtime = .NET 10.0.15
   [SPIKE] Contracts assembly location = ...\build\data_GenelifeNet10Spike_windows_x86_64\Genelife.Contracts.dll
   [SPIKE] DTO instantiated: NetworkId=42 Name=Spike Corp Employees=3 Balance=1234.56 Solvency=Solvent
   [SPIKE] PASS
   ```

**Notes / caveats**

- The `.csproj` was authored by hand with `TargetFramework net10.0` (Godot does not overwrite an existing `.csproj`);
  the editor import left it at `net10.0` and the build used it.
- A C# **export requires a classic `.sln`** next to the `.csproj`. `dotnet new sln` on .NET 10 emits the new `.slnx`
  format, which the exporter rejects (*"no solution file was found"*); a classic
  `Microsoft Visual Studio Solution File, Format Version 12.00` `.sln` with `ExportDebug`/`ExportRelease`
  configurations is what the export step needs.
- A desktop export also needs the matching `4.7.2.stable.mono` export templates installed under
  `%APPDATA%/Godot/export_templates/4.7.2.stable.mono/`, and the export output directory must already exist.
- The exported desktop bundle **ships the full .NET 10 runtime** next to the game, so the client does not depend on a
  host-installed runtime to load the `net10.0` assemblies.
- The spike scaffolding is throwaway and lives **outside the repo** at
  `C:\Users\dustcat\GodotProjects\genelife-net10-spike` (private engine copy under
  `...\genelife-net10-spike-tools\`). Nothing was added to the solution and nothing was committed; this section is
  the only repository change this card produced.