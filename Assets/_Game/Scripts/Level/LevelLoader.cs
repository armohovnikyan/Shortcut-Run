using System;
using UnityEngine;

/// <summary>
/// Puts one level in the scene at a time — instantiates it from a prefab and destroys the previous one.
/// No scene reloads: replay and next level just swap the level instance. Remembers the player's level index.
/// </summary>
public class LevelLoader : MonoBehaviour
{
    private const string LevelIndexKey = "LevelIndex";

    [Tooltip("Levels in play order. After the last one it starts again from the first.")]
    [SerializeField] private Level[] levelPrefabs;
    [Tooltip("Testing: a level already placed in the scene is used instead of the prefab list. " +
             "Replay re-uses it as it is (no fresh copy), so boards taken stay taken.")]
    [SerializeField] private Level sceneLevel;

    private Level current;

    public Level Current => current;
    /// <summary>Saved index into the level list — also the level number the UI shows (+1).</summary>
    public int LevelIndex { get; private set; }

    public event Action<Level> LevelLoaded;

    private void Awake()
    {
        LevelIndex = PlayerPrefs.GetInt(LevelIndexKey, 0);
    }

    /// <summary>The player's current level (fresh copy).</summary>
    public Level LoadCurrent()
    {
        if (sceneLevel != null) return Use(sceneLevel);

        if (levelPrefabs == null || levelPrefabs.Length == 0)
        {
            Debug.LogError($"{name}: Level Prefabs is empty and no Scene Level is set — nothing to load.", this);
            return null;
        }

        Unload();
        Level prefab = levelPrefabs[LevelIndex % levelPrefabs.Length];
        return Use(Instantiate(prefab));
    }

    /// <summary>Moves the saved index on and loads that level.</summary>
    public Level LoadNext()
    {
        LevelIndex++;
        PlayerPrefs.SetInt(LevelIndexKey, LevelIndex);
        PlayerPrefs.Save();
        return LoadCurrent();
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
