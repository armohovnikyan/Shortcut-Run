using UnityEngine;

[CreateAssetMenu(fileName = "NamesDataBase", menuName = "Scriptable Objects/NamesDataBase")]
public class NamesDataBase : ScriptableObject
{
    public string[] names;
    public Sprite[] flags;
}
