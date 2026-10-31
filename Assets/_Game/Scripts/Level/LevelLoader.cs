using System;
using UnityEngine;

/// <summary>
/// Puts one level in the scene at a time — instantiates it from a prefab and destroys the previous one.
/// No scene reloads: replay and next level just swap the level instance. Which level to load is the caller's
/// (GameManager keeps the player's progress in the save); this only turns an index into a level.
/// </summary>
public class LevelLoader : MonoBehaviour
{
    [Tooltip("Which level comes when: a fixed list, then a shuffled pool. Used whenever it is set.")]
    [SerializeField] private LevelSequenceSO sequence;
    [Tooltip("Old way, used only while Sequence is empty: levels in play order, starting again from the first after the last.")]
    [SerializeField] private Level[] levelPrefabs;
    [Tooltip("Testing: a level already placed in the scene is used instead of the prefab list. " +
             "Replay re-uses it as it is (no fresh copy), so boards taken stay taken.")]
    [SerializeField] private Level sceneLevel;

    private Level current;

    public Level Current => current;

    public event Action<Level> LevelLoaded;

    /// <summary>Loads level `levelIndex` (fresh copy). `salt` is the save's pool-shuffle salt (see LevelSequenceSO).
    /// Without a Sequence: past the last prefab in the list it starts again from the first.</summary>
    public Level Load(int levelIndex, int salt = 0)
    {
        if (sceneLevel != null) return Use(sceneLevel);

        Level prefab = sequence != null ? sequence.Pick(levelIndex, salt) : null;
        if (prefab == null && levelPrefabs != null && levelPrefabs.Length > 0)
            prefab = levelPrefabs[Mathf.Max(0, levelIndex) % levelPrefabs.Length];

        if (prefab == null)
        {
            Debug.LogError($"{name}: Sequence and Level Prefabs are both empty and no Scene Level is set — nothing to load.", this);
            return null;
        }

        Unload();
        return Use(Instantiate(prefab));
    }

    public void Unload()
    {
        if (current != null && current != sceneLevel) Destroy(current.gameObject);
        current = null;
    }

    private Level Use(Level level)
    {
        current = level;
        LevelLoaded?.Invoke(level);
        return level;
    }
}
