using System;

/// <summary>
/// Who the player is — for now only the name. The only place the name is changed; saved through GameSave.
/// The name title over the player and the menu's input field both read it from here.
/// </summary>
public class PlayerProfile
{
    public const string DefaultName = "Player";
    public const int MaxNameLength = 12;

    private readonly GameSave save;

    public string Name => string.IsNullOrWhiteSpace(save.Data.playerName) ? DefaultName : save.Data.playerName;

    public event Action<string> NameChanged;

    /// <summary>DefaultName until the player types one.</summary>
    public PlayerProfile(GameSave save) => this.save = save;

    /// <summary>Trims and shortens the name; an empty name falls back to DefaultName.</summary>
    public void SetName(string newName)
    {
        newName = string.IsNullOrWhiteSpace(newName) ? DefaultName : newName.Trim();
        if (newName.Length > MaxNameLength) newName = newName.Substring(0, MaxNameLength);
        if (newName == Name) return;

        save.Data.playerName = newName;
        save.Save();
        NameChanged?.Invoke(newName);
    }
}
