# Level pool — plan and session briefs (decision: pool of original levels)

Decided: the original picks a whole hand-made layout from a pool. We do the same: import every original level prefab, then pick levels by a fixed list for the first ones and by seed after that. No chaining of road chunks, so no road-assembly code. The motif chaining experiment (`Scripts/Level/Sections/*`, `SectionChainPreview*`) is no longer needed; delete it in brief 6.

Data: `Docs/SectionLibrary.md` / `.json` (all original prefabs measured). Already imported: `Prefabs/Levels/Original/Level_Original_01..20` (levels 1–10 in original order, 11–20 a fixed pick; level 18 uses `Final_5_Short` instead of `Final_16`).

## What the pool contains (from SectionLibrary.md)
- **Connected-road levels** (our `TrackRoute` handles them): Final_1, 3, 4, 6, 8, 9, 10, 11, 12, 13, 14 + `_Short` variants (1, 3, 4, 6), Test_8, 10 (+Short), 11 (scale 1), 12, 14 (+Short), Tuto_test.
- **Island-hopping levels** (road = separate decks with gaps; `TrackRoute` can't follow them): Final_2, 5, 7, 15, 16, 17 (+ `_Short`), Test_9 (forks), 13, 15, 16. Need extra work or stay out of the pool.
- Scale 1 levels (narrow road 6.1 m): Test_9, Test_11, part of Test_16. Keep them out of the random pool or let them be "narrow" levels.
- `Short` variants = early/easy levels. `Tuto` = tutorial.

## Design
1. **LevelSequence ScriptableObject** (replaces `LevelLoader.levelPrefabs`): a fixed list for levels 1..N (hand-picked, today 1–10) and then pools. Each pool entry = level prefab + optional tags (short / normal / long, road width, has islands). After the fixed list the loader picks from the pool using `Random(seed = levelNumber + saved salt)` so replay after a fall gives the same level; a new salt after finishing the whole pool or on "reset data".
2. **Variety without new levels:** pick order shuffled per seed, never the same level twice in a row, and an optional **mirrored copy** of each level (17 of 20 levels drift to the right). Mirror must be baked at import time into a second prefab (a runtime negative scale breaks colliders, NavMesh and text) — mirror the splines/pieces on the X axis before building.
3. **Per-level data on the Level prefab:** NPC count (fixed, or min–max rolled at load), spawn box for runner spawn points (noise-placed, player's point always first, player starts last).
4. Difficulty/rarity weights are not used. Growth comes from the order of the pool (short levels first).

## Sessions

### Brief 1 — Import the rest of the originals
Use the existing importer (`Scripts/Editor/OriginalImport/*`, window Window → Shortcut Run → Original Level Import) to build every connected-road prefab in the pool list above that is not yet imported, into `Prefabs/Levels/Original/Pool/` with a clear name (`Pool_Final_08`, `Pool_Final_08_Short`, …). Add a batch mode (one click imports a list of source prefabs, reports successes/failures). Source: `C:\AssetRipper\temp\AssetRipper_export_20261005_154538\ExportedProject\Assets\GameObject\`. Check each: road, islands, board stacks, finish and spawn present; `TrackRoute` valid; level loads and one NPC can finish it. Fix importer bugs found (fork handling, `PickUps` vs `Pickups` naming, scale-1 sections). Done when all connected-road originals exist as playable prefabs and a short report lists what could not be imported and why.

### Brief 2 — Island-hopping levels
Decide per level (Final_2, 5, 7, 15, 16, 17, Test_9, 13, 15, 16): make `TrackRoute` / race progress support decks with gaps (distance along an ordered list of decks and jumps), or leave them out. Include how NPCs cross gaps (boards/jump pads), what the importer does with disconnected pieces, and the one level we already patched (Level 18 uses `Final_5_Short` instead of `Final_16`). Done when they are playable or explicitly excluded with the reason noted in `Docs/SectionLibrary.md`.

### Brief 3 — LevelSequence asset and loader
New `LevelSequenceSO` (fixed list + pools, tags, seed rules above) and `LevelLoader` using it. Seed salt saved in `GameSave`; replay = same level, next level = next pick; no immediate repeats; works with the "sceneLevel" testing override. Editor window/inspector: preview the sequence for levels 1–60 (names list) for a given salt. Done when level 11+ picks pool levels deterministically and a wiped save starts again from the fixed list.

### Brief 4 — Mirrored levels
Add "Mirror" to the importer (X-flip of all pieces, islands, stacks, spawn, finish, jump pads, camera points) producing `…_Mirrored` prefabs; tag them in the pool as variants of the same level. Verify handedness-sensitive things (turn L/R pieces, board stack facing, text, NavMesh). Done when a mirrored level plays like the original in a test run and the sequence can pick either.

### Brief 5 — Runner spawn box and NPC counts
Spawn box on the level (x × z m, editable gizmo) and a seeded noise sampler giving N spawn points (min spacing, on the road/start deck); point #1 is the player's, always at the back. `Level` gets `npcCountMin/Max` (equal = fixed) replacing the fixed count; `RunnerSpawner` reads it. Set per-level values from the originals if they can be measured (3–9 in the original; the summary said the original varies). Keep hand-placed spawn transforms working. Done when levels 1–20 have an NPC count and spawn points that do not overlap.

### Brief 6 — Cleanup and playtest pass
Delete the unused chaining experiment (`Scripts/Level/Sections/`, `Editor/SectionChainPreviewEditor.cs`). Playtest list: every pool level loads, player and NPC finish, boards/jump pads/bonus platforms work. Collect a bug list; traps (pendulum, rotating cross), HighGround and keys stay a separate future task.

### Order
1 → 3; 2 and 4 and 5 in parallel with 3 after 1; 6 last.
