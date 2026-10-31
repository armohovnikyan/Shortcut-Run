# Level playtest checklist

Automated check already done (brief 6) on all 66 level prefabs in `Prefabs/Levels/Original` (20 originals + 23 pool levels + 23 mirrored twins): route valid, finish line with collider, spawn box (player spawn on the road), NPC prefabs set, stand points set, finish view point set, bonus platforms set, NavMesh baked, NPC checkpoints end at the finish, board stacks present, road mesh and collider present. **No failures.** What it cannot tell: whether the level plays well. That needs you in Play mode.

## How to play every level quickly
1. Open the game scene, select `LevelLoader`, assign `Scene Level` = one prefab instance (or set `Sequence` and use `Save.levelIndex`), press Play.
2. Or use the sequence preview (`LevelSequence.asset` → Preview) to see which level comes at which number for a salt.

## Per level, tick off
- [ ] Loads without errors in the Console.
- [ ] Runners spawn in a scattered group on the road, player at the back, everyone facing along the road.
- [ ] NPCs run the whole road and cross the finish (at least one reaches it).
- [ ] You can finish it; reward panel opens; Continue loads the next level.
- [ ] Boards can be picked up and bridges built over gaps; no board stack floats in the air or sits inside the road.
- [ ] Falling = game over panel (not stuck).
- [ ] Bonus platforms rise after a first place and the 15x platform works.
- [ ] Camera follows through every turn and does not clip.

## Levels worth extra attention
- **Forks (loops):** Level_Original_02, 05, 07, 10, 12, 13, 15, 20; Pool_Final_1, 1_Short, 4_Short, 6, 10; Pool_Test_10, 10_Short. Check NPCs take either side and race places make sense around the loop.
- **Narrow road (6.1 m, scale 1):** Level_Original_05 and 06 (in the fixed list!). Spawn box there is 4.1 m wide and only 6 m deep on 05, so NPCs start close together.
- **Many islands:** Pool_Final_12 (18 islands), Pool_Test_8 (10 islands), Pool_Final_11 (7).
- **Mirrored twins:** play at least one mirrored level fully; confirm the finish platform and camera look right (the finish platform's own side parts are not mirrored).
- **No islands:** Pool_Final_4_Short and its mirror have none.
- **Level_Original_18** uses Final_5_Short instead of Final_16 (Final_16 has gaps in the road).

## Known gaps (not bugs of this pass)
- Up to 10 runners but only 6 stand points on the finish platform: extra finishers share the last stand point (RunManager does this by design), so they overlap on the platform.
- Imported levels have no jump pads (the template's pads were removed because they belonged to the template's road).
- Not imported from the original: traps (pendulum, rotating cross), raised paths (HighGround), keys.
- Island-hopping originals (Final_2, 5, 7, 15, 16, 17, Test_9 forks variants, 13, 15, 16) are not in the pool.
- Level_Original_11..20 are no longer used once `LevelLoader.Sequence` is set.
- The original starts runners spread over ~74 m behind the road start; ours start in a 16 m box.
- Per-level NPC counts are still 3 on every level (set `npcCountMin/Max` on the Level by hand).
