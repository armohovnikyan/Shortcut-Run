using System;
using System.IO;
using UnityEngine;

/// <summary>
/// The save system: loads SaveData once when the game starts and writes it back to one JSON file
/// (Application.persistentDataPath/save.json). Created by GameManager and handed to everything that keeps
/// something between sessions (Wallet, Upgrade, GameSettings, PlayerProfile, level progress); they change
/// Data and call Save. Nothing else in the game reads or writes the file.
/// </summary>
public class GameSave
{
    public const string FileName = "save.json";

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public SaveData Data { get; }

    private GameSave(SaveData data) => Data = data;

    /// <summary>Reads the save file. No file (first run) or a broken one = a new save; startingCoins only counts then.</summary>
    public static GameSave Load(int startingCoins)
    {
        string path = FilePath;
        if (File.Exists(path))
        {
            try
            {
                SaveData loaded = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (loaded != null) return new GameSave(loaded);
            }
            catch (Exception e)
            {
                Debug.LogError($"Save file {path} can't be read — starting a new save. {e.Message}");
            }
        }
        return new GameSave(new SaveData { coins = startingCoins });
    }

    /// <summary>Writes Data to disk. Called right after every change — the file is tiny, and a phone game can be
    /// killed at any moment without a quit callback.</summary>
    public void Save()
    {
        string path = FilePath;
        string temp = path + ".tmp";
        try
        {
            // Write a temp file first and swap it in, so a crash mid-write never leaves a half-written save.
            File.WriteAllText(temp, JsonUtility.ToJson(Data, true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }
        catch (Exception e)
        {
            Debug.LogError($"Save file {path} can't be written. {e.Message}");
        }
    }

    /// <summary>Removes the save file (editor menu, tests). The running game keeps its values until restarted.</summary>
    public static void Delete()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }
}
