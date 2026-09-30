using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Creates and destroys the runners of one run. Everything goes under the level instance
/// (runners and the bridges they build), so destroying the level cleans the whole run up.
/// </summary>
public class RunnerSpawner
{
    private readonly Level level;
    private readonly Player playerPrefab;
    private readonly GameObject[] npcSkins; // own copy — shuffling must not reorder the Inspector array
    private readonly List<Runner> runners = new List<Runner>();
    private int nextSkin;

    public IReadOnlyList<Runner> Runners => runners;
    public Player Player { get; private set; }

    public RunnerSpawner(Level level, Player playerPrefab, GameObject[] npcSkins)
    {
        this.level = level;
        this.playerPrefab = playerPrefab;
        this.npcSkins = npcSkins == null ? new GameObject[0] : (GameObject[])npcSkins.Clone();
    }

    /// <summary>Spawns the player and the level's NPCs. Returns the player (null if it couldn't be spawned).</summary>
    public Player SpawnAll()
    {
        if (playerPrefab == null || level.PlayerSpawn == null)
        {
            Debug.LogError($"{level.name}: can't spawn the player — Player Prefab (RunManager) or Player Spawn (Level) is empty.", level);
            return null;
        }
        Player = Spawn(playerPrefab, level.PlayerSpawn);

        NPC[] prefabs = level.NpcPrefabs;
        if (level.NpcCount > 0 && (prefabs == null || prefabs.Length == 0))
        {
            Debug.LogWarning($"{level.name}: Npc Count is {level.NpcCount} but Npc Prefabs is empty — no NPCs spawned.", level);
            return Player;
        }

        for (int i = 0; i < level.NpcCount; i++)
        {
            NPC prefab = prefabs[Random.Range(0, prefabs.Length)];
            NPC npc = Spawn(prefab, level.NpcSpawns[i]);
            npc.ApplySkin(NextSkin()); // no skins set = keeps the model baked into the prefab
            npc.SetPath(level.BuildNpcCheckpoints()); // own list = own random side through each loop
        }
        return Player;
    }

    public void DestroyAll()
    {
        foreach (Runner runner in runners)
            if (runner != null) runner.DestroyRunner();

        runners.Clear();
        Player = null;
    }

    // Deals skins like cards: no two NPCs look the same until every skin is used, then reshuffles.
    private GameObject NextSkin()
    {
        if (npcSkins.Length == 0) return null;
        if (nextSkin == 0) Shuffle(npcSkins);

        GameObject skin = npcSkins[nextSkin];
        nextSkin = (nextSkin + 1) % npcSkins.Length;
        return skin;
    }

    private static void Shuffle<T>(T[] items)
    {
        for (int i = items.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    private T Spawn<T>(T prefab, Transform point) where T : Runner
    {
        T runner = Object.Instantiate(prefab, point.position, point.rotation, level.transform);
        runner.SetBoardParent(level.transform);
        runners.Add(runner);
        return runner;
    }
}
