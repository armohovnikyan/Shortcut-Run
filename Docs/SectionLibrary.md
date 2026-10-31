# Section library (Brief 1)

Source: AssetRipper export `ExportedProject/Assets/GameObject/*.prefab` (Final_*, Test_*_Alex, StartZone, Tuto_test).
Raw numbers: `Docs/SectionLibrary.json`. Extraction script: kept with the session scratchpad (`sections.py`); it reads each prefab's `path_Straight` / `path_Turn_45` pieces, ignores `HighGround`, and follows the longest road from the lowest-Z end.

## The big finding: an original "section" is a WHOLE LEVEL
Every `Final_N` / `Test_N_Alex` prefab already runs from a 30 m lead-in straight to the finish, with its own boards (`Pickups`), islands and traps. `StartZone` is a separate 85 m start prefab (`S30 R45 L45 S15`, a small jog). `CoreGameDef.asset` is empty in this export (the script data is missing) and no asset references the prefabs, so **the level order and the random pool cannot be read from this export** — only the prefabs themselves.
So the original most likely picks a *whole hand-made layout* from a pool; it does not chain small road chunks. The motif chaining we designed is our own idea, not the original's. (This changes brief 3/5 — see the decision at the end.)

## Units
- Piece scale 1.5 for almost everything; `Test_9_Alex` and `Test_11_Alex` use scale 1 (narrow road 6.1 m), `Test_16_Alex` mixes both.
- Straight piece = 10 units × scale = **15 m** (importer constant). Turn piece = 45°, radius 17 × scale (**25.5 m**, "wide") or 7 × scale (**10.5 m**, "tight").
- Straight blocks in the data are multiples of one piece: 15 m (63×), 30 m (70×), 45 m (10×), 20 m (7×, scale 1), 60 m (1×).
- Turn blocks (45° pieces in a row): tight 1/2/3/4 = 49/65/5/26, wide 1/2/3/4/5 = 68/49/14/14/8 — i.e. 45° and 90° are the staples, tight 180° (×4) is the hairpin, wide 180° is rarer.

## Table (all section prefabs)
| Prefab | Scale | Pieces | Longest path (m) | Forward (m) | Sideways (m, right +) | Pickup groups | Traps | Shape (S = straight metres, L/R = left/right in 45� pieces, tight = short radius) |
|---|---|---|---|---|---|---|---|---|
| Final_1 | 1.5 | 44 | 285 | 183 | -15 | 64 | 3 | `S30 Lx2 Rtightx4 Lx1 Rx1 Lx2 Ltightx2 S15 Rx2 S30` |
| Final_10 | 1.5 | 39 | 356 | 195 | -21 | 71 | 3 | `S30 Ltightx2 Rtightx4 S15 Ltightx4 S15 Rtightx4 S30 Lx4 S15 Rx2 S15` |
| Final_11 | 1.5 | 42 | 720 | 282 | 51 | 158 | 6 | `S30 Rx3 Lx3 Ltightx1 Rtightx1 Lx1 Rx1 Rtightx1 Rx1 Rtightx1 Rx1 Lx4 S30 Lx2 Rx1 Lx1 S30 Lx2 Rtightx2 Rx2 S30 Rx2 Lx2 S30` |
| Final_12 | 1.5 | 36 | 635 | 254 | 25 | 129 | 5 | `S30 Rx1 Lx3 S15 Lx2 Rx4 S30 Lx1 Rx3 S15 Ltightx3 Rx5 Lx4 S60` |
| Final_13 | 1.5 | 30 | 513 | 240 | 15 | 115 | 4 | `S30 Rx1 Lx4 Rx5 S15 Ltightx4 S30 Rx5 Lx1 S15 Lx2 S30` |
| Final_14 | 1.5 | 37 | 413 | 237 | 6 | 92 | 4 | `S30 Rx2 S15 Ltightx4 Rtightx4 Ltightx4 S45 Rtightx4 Ltightx4 S15 Rtightx4 Lx2 S30` |
| Final_15 | 1.5 | 30 | 30 | 30 | 0 | 80 | 3 | `S30` |
| Final_16 | 1.5 | 14 | 30 | 30 | 0 | 77 | 3 | `S30` |
| Final_17 | 1.5 | 20 | 30 | 30 | 0 | 127 | 5 | `S30` |
| Final_1_Short | 1.5 | 33 | 157 | 117 | -36 | 53 | 3 | `S30 Rx1 Lx1 Ltightx2 S15 Rx2 S15` |
| Final_2 | 1.5 | 49 | 45 | 45 | 0 | 72 | 5 | `S45` |
| Final_2_Short | 1.5 | 28 | 45 | 45 | 0 | 42 | 4 | `S45` |
| Final_3 | 1.5 | 39 | 650 | 332 | 157 | 122 | 7 | `S30 Lx1 Rx2 Rtightx2 Ltightx1 S15 Lx2 Ltightx1 Lx1 Rtightx1 Rx4 Lx5 Rtightx2 Rx2 Lx1 Rx2 Lx3 Ltightx2 Rx2 S30` |
| Final_3_Short | 1.5 | 27 | 457 | 260 | 55 | 94 | 6 | `S30 Lx1 Rx2 Rtightx2 Ltightx1 S15 Lx2 Ltightx1 Lx1 Rtightx1 Rx4 Lx5 Rx2 S30` |
| Final_3_TutoVersion | 1.5 | 27 | 457 | 260 | 55 | 94 | - | `S30 Lx1 Rx2 Rtightx2 Ltightx1 S15 Lx2 Ltightx1 Lx1 Rtightx1 Rx4 Lx5 Rx2 S30` |
| Final_4 | 1.5 | 42 | 671 | 317 | 128 | 101 | 7 | `S30 Rx1 Rtightx1 Ltightx1 Lx5 Rtightx3 Rx1 S15 Lx1 Rx3 S30 Rx1 Lx3 S15 Rtightx2 Lx1 Ltightx1 Lx1 Ltightx1 Rx1 Rtightx1 Rx3 Ltightx1 Lx2 S30` |
| Final_4_Short | 1.5 | 28 | 383 | 240 | 66 | 57 | 3 | `S30 Rx1 Rtightx1 Ltightx1 Lx3 Rtightx4 Lx1 Ltightx1 Lx1 Ltightx1 Rx1 Rtightx1 Rx3 Ltightx1 Lx2 S30` |
| Final_5 | 1.5 | 59 | 45 | 45 | 0 | 137 | 5 | `S45` |
| Final_5_Short | 1.5 | 23 | 45 | 45 | 0 | 96 | 5 | `S45` |
| Final_6 | 1.5 | 47 | 735 | 253 | 242 | 130 | 5 | `S30 Ltightx1 Rx4 Lx2 Ltightx1 Lx1 S30 Rx1 Ltightx1 Rtightx1 Rx3 Ltightx1 Lx1 Ltightx1 Lx1 Rx1 Rtightx1 Rx3 Lx1 Rx2 Lx1 Ltightx2 Lx1 S30 Lx1 Rx2 Lx2 S30` |
| Final_6_Short | 1.5 | 22 | 363 | 204 | 102 | 64 | 5 | `S30 Ltightx1 Rx4 Lx2 Ltightx1 Lx1 S30 Rx4 Ltightx2 Lx1 S30` |
| Final_7 | 1.5 | 59 | 85 | 81 | 15 | 190 | 5 | `S30 Rx1 Lx1 S15` |
| Final_8 | 1.5 | 47 | 523 | 243 | 111 | 91 | 5 | `S30 Ltightx2 Rtightx4 S15 Ltightx4 S15 Rtightx4 S45 Rx1 Lx3 S15 Ltightx2 Rtightx4 S15 Ltightx4 S15 Rtightx4 S15 Ltightx2 S30` |
| Final_9 | 1.5 | 49 | 563 | 309 | -6 | 84 | 6 | `S30 Ltightx2 Rtightx4 S15 Ltightx4 S15 Rtightx4 S30 Lx4 S15 Rx2 S15 Ltightx2 Rtightx4 S15 Ltightx4 S15 Rtightx4 S15 Ltightx2 S30` |
| StartZone | 1.5 | 5 | 85 | 81 | 15 | - | - | `S30 Rx1 Lx1 S15` |
| StartZoneBonus | 1.5 | 1 | 15 | 15 | 0 | - | - | `S15` |
| Test_10_Alex | 1.5 | 58 | 180 | 117 | -15 | 76 | 4 | `S30 Rtightx2 S15 Ltightx2 S15 Ltightx2 S15 Rx2 S15` |
| Test_10_Alex_Short | 1.5 | 45 | 180 | 117 | -15 | 58 | 5 | `S30 Rtightx2 S15 Ltightx2 S15 Ltightx2 S15 Rx2 S15` |
| Test_11_Alex | 1.0 | 48 | 407 | 230 | 51 | 78 | 5 | `S30 Lx1 Rx2 Rtightx1 S10 Rtightx2 Ltightx2 S10 Ltightx2 S20 Ltightx2 S10 Rtightx2 Rx2 S10 Ltightx4 S20 Rx2 S20 Rx2 S10 Ltightx2 Rtightx2 Ltightx3 Rtightx1 S20` |
| Test_12_Alex | 1.5 | 59 | 724 | 320 | 4 | 106 | 5 | `S30 Lx2 S15 Rx1 Rtightx3 S45 Ltightx2 S30 Ltightx2 S30 Ltightx2 Rtightx2 S15 Rtightx2 S15 Rx2 Ltightx2 S15 Rtightx2 S15 Rtightx2 Ltightx2 S30 Ltightx2 S30 Ltightx2 S30 Rx1 Rtightx1 S15 Rx1 Rtightx1 Ltightx2 S30` |
| Test_13_Alex | 1.5 | 54 | 120 | 70 | 62 | 133 | 6 | `S30 Rtightx2 S30 Lx1 Ltightx1 S15` |
| Test_13_Short | 1.5 | 35 | 188 | 111 | 108 | 76 | - | `S30 Rtightx2 S15 Lx1 Ltightx1 Rx2 S15 Lx1 Ltightx1 S15` |
| Test_14_Alex | 1.5 | 54 | 73 | 70 | -11 | 185 | 6 | `S30 Ltightx1 Rx1 S15` |
| Test_14_Alex_Short | 1.5 | 39 | 527 | 249 | 72 | 108 | 6 | `S30 Ltightx1 Rx1 Rtightx2 S15 Lx5 Rx4 Ltightx1 S30 Rtightx2 S15 Rtightx2 S15 Ltightx2 S30 Ltightx2 S30 Rtightx2 Ltightx2 S30` |
| Test_15_Alex | 1.5 | 70 | 203 | 111 | 123 | 231 | 6 | `S30 Rtightx2 S30 Lx1 Ltightx1 Rx2 S15 Lx1 Ltightx1 S15` |
| Test_16_Alex | 1.0/1.5 | 82 | 0 | 0 | 0 | 251 | - | `(islands only)` |
| Test_8_Alex | 1.5 | 31 | 485 | 270 | 30 | 41 | 5 | `S45 Lx2 Rx4 S45 Ltightx2 S15 Ltightx2 S45 Rx3 Lx1 Rx2 S30 Ltightx2 S15` |
| Test_9_Alex | 1.0 | 36 | 307 | 132 | 54 | 43 | 4 | `S30 Lx1 Rx1 Rtightx2 S20 Ltightx4 S20 Rtightx2 S20 Rx2 S10 Rx2 S10 Ltightx2 S10 Ltightx3 Rtightx1 S30` |
| Tuto_test | 1.5 | 29 | 391 | 257 | 0 | 35 | - | `S30 Rtightx2 Lx1 Ltightx2 Lx1 Rtightx2 S15 Ltightx1 Lx1 Rx2 Rtightx1 Rx1 Lx2 S15 Rtightx2 Lx1 Ltightx2 Lx1 Rtightx2 S15` |
Notes on the table: "Shape" follows the longest simple road from the lowest-Z end; prefabs with many `ends` (Final_2, 5, 7, 15, 16, 17, Test_13/15/16) are island-hopping levels whose road is mostly separate decks, so their shape column shows only the first connected stretch (`S30`/`S45`) — they need a different treatment (islands, not a road). "Net turn" was not reported: it is 0 for every connected road, as in the 20 imported levels.

## Recurring blocks (what a motif library should contain)
1. **Lead-in** `S30` — every level starts with it. A start variant with a jog: `StartZone` = `S30 R45 L45 S15`.
2. **Run-out** `S15` / `S30` / `S45` before the finish.
3. **Tight hairpin snake** `Ltight2 Rtight4 S15 Ltight4 S15 Rtight4 …` — Final_8, 9, 10 repeat it (Final_9 = Final_8 plus a wide `Lx4` S-bend). Also `Ltight4 Rtight4 Ltight4` in Final_14. The most distinctive block of the game.
4. **Wide U** `Lx4` / `Rx4` (180° at 25.5 m) with `S15` — Final_1, 10, 13.
5. **Wide S-bend / jog** `Rx2 Lx2`, `Rx1 Lx1`, `Lx1 Rx2` (90° kicks) — nearly every Final.
6. **Tight U from two pieces** `Ltight2` after a straight — as a lane change.
7. **Short versions** `Final_1_Short`, `_3_Short`, `_4_Short`, `_6_Short`, `Test_10_Alex_Short`, `Test_14_Alex_Short`: the same layout with fewer repeats — used for early (easier) levels, which fits "first levels short, later longer".
8. **Island-hopping finals** Final_2, 5, 7, 15, 16, 17 (+ `_Short`): road pieces on separate islands; not road chains.

## Per-level numbers to copy for generator limits
Connected-road finals: path length 157–735 m, forward distance 117–332 m (long ones 237–332), sideways offset −36 … +242 m (mostly 0–160). Start→finish angle for the long ones is at most ~40° (Final_6: 253 fwd / 242 side).
