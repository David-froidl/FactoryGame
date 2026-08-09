# FactoryGame

A Satisfactory-inspired factory builder. Godot 4 + C# (.NET), built solo on a laptop —
performance and iteration speed beat visual fidelity every time they trade off.

## Hard architecture constraints

These are load-bearing decisions, not style preferences. If one of them seems wrong for
a task you're doing, say so and explain the tradeoff instead of quietly working around it.

1. **Items on belts are never Nodes or scene instances.** A belt segment holds a compact
   list of `(ItemId, distanceToNext)` pairs. Only the head item's position is computed
   directly each tick; every other item inherits movement through its stored offset. This
   is the Factorio model, and it's the only way belt item counts stay off the scene tree
   and off the GC. See [`BeltSegment`](src/Sim/Belts/BeltSegment.cs).
2. **All item/belt rendering goes through `MultiMeshInstance3D`.** One draw call (or a
   handful, one per mesh/material) for the entire factory's items, regardless of how many
   thousand are in flight. See [`BeltVisual3D`](src/Render/BeltVisual3D.cs) for the pattern:
   simulation state (integers, fixed-point units) is read out of the sim once per tick and
   written into `MultiMesh` instance transforms — no per-item Node ever exists.
3. **Simulation runs on a fixed 20 Hz tick in `_PhysicsProcess`, decoupled from render
   framerate.** `project.godot` sets `physics/common/physics_ticks_per_second=20` to drive
   this. Rendering interpolates between the last two tick snapshots inside `_Process` (which
   runs once per rendered frame) — it never advances simulation state. This keeps the sim
   itself perfectly reproducible (fixed ticks, integer math) independent of the player's
   monitor or GPU, which matters for future replays/saves and for not having belt throughput
   vary with framerate.
4. **Recipes, machines and buildables are data, not classes.** Godot Resources or JSON,
   loaded at runtime. One generic `Machine` class (`inputBuffer -> timer -> outputBuffer`);
   a Smelter and an Assembler differ only by the data they're configured with. Nothing about
   a specific machine type should require a new C# class. (Not yet implemented — Phase 1.)
5. **The simulation core (`src/Sim`) has zero Godot dependencies.** It's a plain C# class
   library that runs and is tested headlessly with `dotnet test`, no engine required. Godot
   code reads from it and renders it; it never reads from Godot. If you're tempted to add a
   `using Godot;` anywhere under `src/Sim`, stop — the boundary belongs in `src/Render`
   instead.

## Why fixed-point integers, not floats, in the sim

Positions and speeds in `Factory.Sim` are `int`s in fixed-point "units"
(`SimConstants.UnitsPerTile = 4800` units/tile), not floats in metres. Two reasons:

- **Determinism.** Integer arithmetic gives bit-identical results on any machine, which
  floating point does not guarantee across CPUs/compilers. That matters the moment saves,
  replays, or (eventually) multiplayer need two runs of the same tick sequence to agree.
- **Exact belt rates.** A belt's throughput has to be exactly its rated items/minute, not
  "close enough". `SimConstants.IsExactRate` encodes the constraint: a rate is only exact if
  it divides `60 * TicksPerSecond` (1200 at 20 Hz). `BeltTiers` picks values that satisfy
  this; `BeltSpeedTests` fails the build if a future tier doesn't.

Floats only exist at the render boundary (`src/Render`), where an integer unit position is
converted to metres and interpolated for display.

## Folder layout

```
/                          Godot project root (project.godot, FactoryGame.csproj, FactoryGame.sln)
/src/Sim/                  Pure C# simulation core. Zero Godot references. Unit tested headlessly.
  Factory.Sim.csproj
  SimConstants.cs          Fixed-point units, tick rate, belt-rate-exactness rule.
  Items/                   ItemId, ItemStack.
  Core/                    ISimNode, IItemSink, IItemSource — the interfaces belts/machines share.
  Belts/                   BeltSegment, Splitter, Merger, BeltNetwork, ItemVoid (test/demo sink).
/src/Render/               Godot-facing C# (MultiMesh rendering, tick/frame decoupling glue).
  BeltVisual3D.cs          Node3D that owns one BeltSegment + one MultiMesh; the sim/render boundary.
/src/World/                Godot-facing C# for the static world: terrain and ore node placement.
  HeightmapTerrain.cs      Builds ground mesh + collision from assets/terrain/heightmap.png at _Ready.
  OreNodeResource.cs       Data-only Resource: OreType + OrePurity. No position — see OreNodeMarker.
  OreNodeMarker.cs         Node3D placed in a scene; holds world position + a reference to the data.
/scenes/                   .tscn scene files.
  belt_demo/               Minimal scene: one belt, rendered via MultiMesh, saturated to show motion.
  static_world/            500x500m heightmap terrain, sky/sun, and the 4 ore node markers.
/data/                     Recipes/buildables as Resources or JSON.
  ore_nodes/               One .tres per placed ore deposit (OreNodeResource): type + purity.
/tests/Factory.Sim.Tests/  xUnit tests for src/Sim. Runs with `dotnet test`, no Godot needed.
/assets/                   Art, audio, fonts — tracked through Git LFS (see .gitattributes).
  terrain/heightmap.png    Authored (not runtime-generated) grayscale heightmap for the static world.
```

`FactoryGame.csproj` lives at the repo root because Godot expects the game's C# project next
to `project.godot`. It explicitly excludes `src/Sim/**` and `tests/**` from its compile glob
(both have their own `.csproj`) and references `Factory.Sim` via `ProjectReference` — that
reference is the only point of contact between the Godot project and the sim core.

## Working with the sim core standalone

```bash
dotnet test tests/Factory.Sim.Tests/Factory.Sim.Tests.csproj
```

No Godot install required for this. `FactoryGame.sln` ties all three projects together for
IDE use (Rider, VS, VS Code); `dotnet build FactoryGame.sln` builds everything, including
against the real `GodotSharp` package, without needing the Godot editor open.

## Conventions

- No file over ~300 lines. Split before it gets there.
- Small, focused commits.
- New dependencies or Godot plugins need to be asked about first — none are approved by default.
- Belt/machine rate constants must satisfy `SimConstants.IsExactRate`; `BeltSpeedTests`
  enforces this for everything in `BeltTiers`.

## Phase roadmap

- **Phase 0 (done):** Project scaffolding, sim core (belts, splitters, mergers, backpressure),
  xUnit coverage, minimal MultiMesh belt render demo.
- **Static world (done, out of order — explicitly requested):** Fixed 500x500m heightmap
  terrain (`src/World/HeightmapTerrain.cs`) with flat buildable plateaus baked into the
  heightmap, a sky + directional light, and 4 data-driven ore node Resources (Iron, Copper,
  Limestone, Coal) placed as markers. No procedural generation — the heightmap is a static
  authored asset, not computed at runtime. No player controller, no building placement yet;
  the terrain has collision (`HeightMapShape3D`) so that's ready for Phase 2 to use.
- **Phase 1:** Data-driven machines (`Machine` base class, recipe/buildable Resources),
  item registry, a real belt/item mesh and material instead of placeholder boxes.
- **Phase 2:** Player controller, build mode (placing belts/machines on a grid), save/load.
- **Phase 3:** Power grid, tech tree / unlocks, UI (inventory, build menu, statistics).

Beyond Phase 0 and the static world above, nothing is implemented yet. Do not start Phase 1+
work without being asked — in particular, no player controller, UI, power grid, or tech tree
until explicitly requested.
