# Game data (`/data`)

Item and recipe definitions for the simulation. Parsed by `Factory.Sim.Data.GameDataLoader`
(`src/Sim/Data/GameDataLoader.cs`) — that loader is the single source of validation; nothing
downstream re-checks this data. Loading fails fast and loud on bad data; nothing is ever
silently skipped or defaulted.

## `items/items.json`

```json
{
  "items": [
    { "id": "ferrite_ore", "displayName": "Ferrit-Erz" }
  ]
}
```

- `id` — stable key referenced from recipes. Required, non-empty, unique.
- `displayName` — required, non-empty.
- Runtime `ItemId` values are assigned automatically, in array order, starting at 1 (0 is
  reserved for "no item"). Do not rely on a specific numeric value; always look items up
  by `id`.

## `recipes/recipes.json`

```json
{
  "recipes": [
    {
      "id": "ferrite_smelting",
      "inputs": [ { "item": "ferrite_ore", "count": 1 } ],
      "outputs": [ { "item": "ferrite_ingot", "count": 1 } ],
      "durationTicks": 60
    }
  ]
}
```

- `id` — required, non-empty, unique.
- `inputs` — list of `{ "item": <items.json id>, "count": <positive int> }`. May be empty
  (e.g. a future extractor that has no input). Every `item` must exist in `items.json`.
- `outputs` — same shape as `inputs`, but must contain at least one entry.
- Duration — specify **exactly one** of:
  - `durationTicks` (int, preferred) — sim ticks at `SimConstants.TicksPerSecond` (20).
  - `durationSeconds` (number) — converted to ticks at load time using exact decimal
    arithmetic; the result must be a whole number of ticks or loading fails (e.g. `3` ->
    `60` ticks at 20 Hz; `2.5` -> `50` ticks; a value like `0.13` would be rejected since
    it doesn't land on a whole tick).

Any violation of the rules above (duplicate id, unknown item reference, non-positive
count/duration, ambiguous or missing duration field, missing file, malformed JSON) throws
`GameDataException` naming the offending record.

## Not yet implemented

Extractors (ore -> raw item, no input, ~2s cycle) are intentionally not defined here yet
(Phase 1 scope). Phase 2 will add them as ordinary `RecipeDefinition`s with an empty
`inputs` array — the loader already supports zero-input recipes, so no schema or loader
change is expected to be needed for that.
