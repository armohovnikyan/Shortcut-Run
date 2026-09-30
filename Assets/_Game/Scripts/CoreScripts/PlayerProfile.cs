using System;
using UnityEngine;

/// <summary>
/// Who the player is — for now only the name. The only place the name is changed and saved.
/// The name title over the player and the menu's input field both read it from here.
/// </summary>
public class PlayerProfile
{
    public const string DefaultName = "Player";
    public const int MaxNameLength = 12;

    private const string NameKey = "PlayerName";

    public string Name { get; private set; }

    public event Action<string> NameChanged;

    /// <summary>Loads the saved name; DefaultName until the player types one.</summary>
    public PlayerProfile()
    {
        Name = PlayerPrefs.GetString(NameKey, DefaultName);
    }

    /// <summary>Trims and shortens the name; an empty name falls back to DefaultName.</summary>
    public void SetName(string newName)
    {
        newName = string.IsNullOrWhiteSpace(newName) ? DefaultName : newName.Trim();
        if (newName.Length > MaxNameLength) newName = newName.Substring(0, MaxNameLength);
        if (newName == Name) return;

        Name = newName;
        PlayerPrefs.SetString(NameKey, Name);
        PlayerPrefs.Save();
        NameChanged?.Invoke(Name);
    }
}
