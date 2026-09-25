using UnityEngine;
[CreateAssetMenu(fileName = "BoardSO", menuName = "Scriptable Objects/BoardSO")]
public class BoardSO : ScriptableObject
{
    [Tooltip("Seconds until a collected board appears again. Real time — keeps counting while paused.")]
    [SerializeField, Min(0f)] private float respawnTime = 2.8f;
    [Tooltip("Horizontal gap between neighbour boards in a stack row.")]
    [SerializeField, Min(0f)] private float stackSpacing = 0.5f;

    public float RespawnTime => respawnTime;
    public float StackSpacing => stackSpacing;
}
