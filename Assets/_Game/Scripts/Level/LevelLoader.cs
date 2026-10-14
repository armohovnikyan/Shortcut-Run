using System;
using UnityEngine;

/// <summary>
/// Puts one level in the scene at a time — instantiates it from a prefab and destroys the previous one.
/// No scene reloads: replay and next level just swap the level instance. Which level to load is the caller's
/// (GameManager keeps the player's progress in the save); this only turns an index into a level.
/// </summary>
public class LevelLoader : MonoBehaviour
{
    [Tooltip("Levels in play order. After the last one it starts again from the first.")]
    [SerializeField] private Level[] levelPrefabs;
    [Tooltip("Testing: a level already placed in the scene is used instead of the prefab list. " +
             "Replay re-uses it as it is (no fresh copy), so boards taken stay taken.")]
    [SerializeField] private Level sceneLevel;

    private Level current;

    public Level Current => current;

    public event Action<Level> LevelLoaded;

    /// <summary>Loads level `levelIndex` (fresh copy). Past the last level it starts again from the first.</summary>
    public Level Load(int levelIndex)
    {
        if (sceneLevel != null) return Use(sceneLevel);

        if (levelPrefabs == null || levelPrefabs.Length == 0)
        {
            Debug.LogError($"{name}: Level Prefabs is empty and no Scene Level is set — nothing to load.", this);
            return null;
        }

        Unload();
        Level prefab = levelPrefabs[Mathf.Max(0, levelIndex) % levelPrefabs.Length];
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
