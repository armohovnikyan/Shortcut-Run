using System;
using System.Collections.Generic;

/// <summary>
/// Everything the game remembers between sessions, in one plain object that is written to disk as JSON.
/// Only data — who changes a value (Wallet, Upgrade, GameSettings …) and when it is written (GameSave) live elsewhere.
/// Fields, not properties: JsonUtility only writes public fields. New fields get their default value when an
/// older save is loaded, so adding one is safe; renaming one loses what was saved under the old name.
/// </summary>
[Serializable]
public class SaveData
{
    public int coins;
    /// <summary>Levels passed = index of the level the player is on (0 = first).</summary>
    public int levelIndex;
    public bool soundOn = true;
    public bool vibrationOn = true;
    public string playerName = PlayerProfile.DefaultName;
    // A list, not a Dictionary: JsonUtility can't write dictionaries.
    public List<UpgradeLevel> upgrades = new List<UpgradeLevel>();
    public List<BuffCount> buffs = new List<BuffCount>();

    [Serializable]
    public class UpgradeLevel
    {
        public string key;
        public int level;
    }

    [Serializable]
    public class BuffCount
    {
        public string key;
        public int count;
    }

    /// <summary>Saved level of the upgrade with this key; 1 (nothing bought) if it was never bought.</summary>
    public int GetUpgradeLevel(string key)
    {
        foreach (UpgradeLevel upgrade in upgrades)
            if (upgrade.key == key) return upgrade.level;
        return 1;
    }

    public void SetUpgradeLevel(string key, int level)
    {
        foreach (UpgradeLevel upgrade in upgrades)
        {
            if (upgrade.key != key) continue;
            upgrade.level = level;
            return;
        }
        upgrades.Add(new UpgradeLevel { key = key, level = level });
    }

    /// <summary>Saved count of the buff with this key; 0 if it was never bought.</summary>
    public int GetBuffCount(string key)
    {
        foreach (BuffCount buff in buffs)
            if (buff.key == key) return buff.count;
        return 0;
    }

    public void SetBuffCount(string key, int count)
    {
        foreach (BuffCount buff in buffs)
        {
            if (buff.key != key) continue;
            buff.count = count;
            return;
        }
        buffs.Add(new BuffCount { key = key, count = count });
    }
}
