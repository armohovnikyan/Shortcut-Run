using UnityEngine;
[CreateAssetMenu(fileName = "BoardSO", menuName = "Scriptable Objects/BoardSO")]
public class BoardSO : ScriptableObject
{
    [SerializeField] private Vector3 scale;
    [SerializeField] private Material material;
    [TagField, SerializeField] private string targetTag;
    public Vector3 Scale => scale;
    public Material Material => material;
    public string TargetTag => targetTag;
}
