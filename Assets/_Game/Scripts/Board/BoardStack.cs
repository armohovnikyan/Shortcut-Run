using System.Collections.Generic;
using UnityEngine;

// One prefab for every stack. The shape comes from a BoardStackShapeSO — set in the Inspector,
// or passed in by the level spawner through Build() right after Instantiate.
public class BoardStack : MonoBehaviour
{
    [SerializeField] private BoardStackShapeSO shape;
    [SerializeField] private CollectableBoard boardPrefab;
    [Tooltip("Offset from one row to the next — keep it tight (board thickness). (0, h, 0) = rows stacked up. " +
             "Horizontal gap inside a row comes from the board's BoardSO.")]
    [SerializeField] private Vector3 rowStep = new Vector3(0f, 0.15f, 0f);

    private bool built;

    private float BoardSpacing => boardPrefab.Data.StackSpacing;

    public int BoardCount => shape != null ? shape.BoardCount : 0;

    // Start, not Awake: a spawner calling Build() right after Instantiate gets there first.
    private void Start()
    {
        if (shape != null) Build(shape);
    }

    public void Build(BoardStackShapeSO newShape)
    {
        if (built) return;
        built = true;
        shape = newShape;

        foreach (Vector3 localPosition in GetLocalPositions())
        {
            CollectableBoard board = Instantiate(boardPrefab, transform);
            board.transform.SetLocalPositionAndRotation(localPosition, Quaternion.identity);
        }
    }

    private IEnumerable<Vector3> GetLocalPositions()
    {
        for (int row = 0; row < shape.Rows.Count; row++)
        {
            int count = shape.Rows[row];
            float firstX = -(count - 1) * 0.5f * BoardSpacing; // centers the row

            for (int i = 0; i < count; i++)
                yield return rowStep * row + Vector3.right * (firstX + i * BoardSpacing);
        }
    }

    // Shape preview in the Scene view, no Play mode needed.
    private void OnDrawGizmosSelected()
    {
        if (shape == null || boardPrefab == null || boardPrefab.Data == null || built) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Vector3 size = new Vector3(BoardSpacing * 0.9f, 0.1f, 1f);
        foreach (Vector3 localPosition in GetLocalPositions())
            Gizmos.DrawWireCube(localPosition, size);
    }
}
