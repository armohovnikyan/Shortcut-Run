using UnityEngine;
[CreateAssetMenu(fileName = "BoardSO", menuName = "Scriptable Objects/BoardSO")]
public class BoardSO : ScriptableObject
{
    [TagField, SerializeField] private string targetTag;
    public string TargetTag => targetTag;
}
