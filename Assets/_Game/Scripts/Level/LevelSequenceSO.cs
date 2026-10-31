using UnityEngine;

/// <summary>
/// Which level comes when. A fixed list for the first levels (hand-picked, same for every player), then a pool of
/// levels played in a shuffled order. The shuffle is a pure function of (level number, salt): the same pair always
/// gives the same level, so replaying after a fall gets the same track, and nothing about the order has to be saved
/// except the salt. The pool is played through once in random order, then reshuffled (a new cycle), and the first
/// level of a new cycle is never the last one of the previous one.
/// A new save gets a new random salt, so a wiped save plays a different pool order — the fixed levels stay the same.
/// </summary>
[CreateAssetMenu(menuName = "Shortcut Run/Level Sequence", fileName = "LevelSequence")]
public class LevelSequenceSO : ScriptableObject
{
    [Tooltip("Levels 1, 2, 3 … in this order. Same for every player.")]
    [SerializeField] private Level[] fixedLevels;
    [Tooltip("After the fixed levels: played through in a shuffled order, then reshuffled. " +
             "An entry with a Mirrored twin plays either version, picked by the same seed rules.")]
    [SerializeField] private PoolEntry[] pool;

    [System.Serializable]
    public class PoolEntry
    {
        public Level level;
        [Tooltip("The same level flipped left-right (optional).")]
        public Level mirrored;
    }

    public Level[] FixedLevels => fixedLevels;
    public PoolEntry[] Pool => pool;

    /// <summary>The level prefab for `levelIndex` (0 = first level). Null when the asset holds no levels at all.</summary>
    public Level Pick(int levelIndex, int salt)
    {
        levelIndex = Mathf.Max(0, levelIndex);
        int fixedCount = fixedLevels?.Length ?? 0;
        int poolCount = pool?.Length ?? 0;

        if (levelIndex < fixedCount) return fixedLevels[levelIndex];
        if (poolCount == 0) return fixedCount == 0 ? null : fixedLevels[levelIndex % fixedCount];

        int n = levelIndex - fixedCount;
        int cycle = n / poolCount;
        int entryIndex = Order(cycle, salt, poolCount)[n % poolCount];
        PoolEntry entry = pool[entryIndex];

        // Each cycle flips its own coin per entry, so a level can come round in the other version next time.
        bool useMirror = entry.mirrored != null
            && (entry.level == null || new System.Random(unchecked(salt * 83492791 ^ (cycle + 1) * -1640531535 ^ entryIndex * 40503)).Next(2) == 1);
        return useMirror ? entry.mirrored : entry.level;
    }

    /// <summary>Pool indices in the order cycle `cycle` plays them.</summary>
    private static int[] Order(int cycle, int salt, int count)
    {
        int[] order = Shuffle(cycle, salt, count);
        if (count <= 1) return order;

        if (count == 2)
        {
            // [0, 1] every cycle: the last of one cycle (1) is never the first of the next (0).
            order[0] = 0; order[1] = 1;
            return order;
        }

        // The previous cycle's last entry is untouched by the fix below (it only swaps the first two), so the
        // raw shuffle of the previous cycle tells it without recursing.
        if (cycle > 0 && order[0] == Shuffle(cycle - 1, salt, count)[count - 1])
        {
            int swap = order[0]; order[0] = order[1]; order[1] = swap;
        }
        return order;
    }

    private static int[] Shuffle(int cycle, int salt, int count)
    {
        var order = new int[count];
        for (int i = 0; i < count; i++) order[i] = i;

        var random = new System.Random(unchecked(salt * 73856093 ^ (cycle + 1) * 19349663 ^ 0x5bd1e995));
        for (int i = count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            int swap = order[i]; order[i] = order[j]; order[j] = swap;
        }
        return order;
    }
}
