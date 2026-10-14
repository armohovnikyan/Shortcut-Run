using UnityEditor;
using UnityEngine;

// Editor shortcuts for the save file (GameSave). Delete only while the game is stopped — a running game
// would write its values back on the next change.
public static class SaveMenu
{
    [MenuItem("Tools/Shortcut Run/Delete Save")]
    private static void DeleteSave()
    {
        GameSave.Delete();
        Debug.Log($"Save deleted: {GameSave.FilePath}");
    }

    [MenuItem("Tools/Shortcut Run/Open Save Folder")]
    private static void OpenSaveFolder() => EditorUtility.RevealInFinder(Application.persistentDataPath);
}
