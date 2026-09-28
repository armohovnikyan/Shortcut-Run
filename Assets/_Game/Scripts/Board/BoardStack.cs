using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// A pile of CollectableBoards laid out by a BoardStackShapeSO. The boards are built in the editor as
// saved child objects (prefab instances of boardPrefab), so the level prefab is complete and nothing
// is spawned at runtime. Rebuilds itself when the shape, prefab or row step changes.
// Every direct child with a CollectableBoard belongs to the stack — other children are left alone.
[ExecuteAlways]
public class BoardStack : MonoBehaviour
{
    [SerializeField] private BoardStackShapeSO shape;
    [SerializeField] private CollectableBoard boardPrefab;
    [Tooltip("Offset from one row to the next — keep it tight (board thickness). (0, h, 0) = rows stacked up. " +
             "Horizontal gap inside a row comes from the board's BoardSO.")]
    [SerializeField] private Vector3 rowStep = new Vector3(0f, 0.15f, 0f);

    private bool CanLayOut => shape != null && boardPrefab != null && boardPrefab.Data != null;
    private float BoardSpacing => boardPrefab.Data.StackSpacing;

    public int BoardCount => shape != null ? shape.BoardCount : 0;

    // Row by row, bottom first; each row centred on the stack's pivot along local X.
    private List<Vector3> GetLocalPositions()
    {
        var positions = new List<Vector3>();
        if (!CanLayOut) return positions;

        for (int row = 0; row < shape.Rows.Count; row++)
        {
            int count = shape.Rows[row];
            float firstX = -(count - 1) * 0.5f * BoardSpacing; // centers the row

            for (int i = 0; i < count; i++)
                positions.Add(rowStep * row + Vector3.right * (firstX + i * BoardSpacing));
        }
        return positions;
    }

    private List<CollectableBoard> GetBoardChildren()
    {
        var boards = new List<CollectableBoard>();
        foreach (Transform child in transform)
            if (child.TryGetComponent(out CollectableBoard board)) boards.Add(board);
        return boards;
    }

    // Outline preview for when the boards can't be built yet (e.g. no prefab assigned).
    private void OnDrawGizmosSelected()
    {
        if (!CanLayOut || GetBoardChildren().Count > 0) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Vector3 size = new Vector3(BoardSpacing * 0.9f, 0.1f, 1f);
        foreach (Vector3 localPosition in GetLocalPositions())
            Gizmos.DrawWireCube(localPosition, size);
    }

#if UNITY_EDITOR
    private bool rebuildQueued;

    private void OnEnable()
    {
        if (Application.isPlaying) return;
        Undo.undoRedoPerformed += QueueRebuild;
        BoardStackShapeSO.Changed += OnShapeAssetChanged;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= QueueRebuild;
        BoardStackShapeSO.Changed -= OnShapeAssetChanged;
    }

    private void OnValidate()
    {
        if (!Application.isPlaying) QueueRebuild();
    }

    // Rows edited in the shape asset itself.
    private void OnShapeAssetChanged(BoardStackShapeSO changed)
    {
        if (changed == shape) QueueRebuild();
    }

    // Used by TrackPlacedStamp, which owns its stacks and sets them up from its pattern.
    public void Configure(BoardStackShapeSO newShape, CollectableBoard newBoardPrefab)
    {
        if (shape == newShape && boardPrefab == newBoardPrefab) return;
        shape = newShape;
        boardPrefab = newBoardPrefab;
        EditorUtility.SetDirty(this);
        QueueRebuild();
    }

    // Objects can't be created or destroyed inside OnValidate — do it on the next editor tick.
    private void QueueRebuild()
    {
        if (rebuildQueued) return;
        rebuildQueued = true;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            rebuildQueued = false;
            if (!IsUpToDate()) RebuildBoards();
        };
    }

    // Compared against the real children, not a saved fingerprint: the rebuild isn't part of undo,
    // so after an undo only the children themselves tell the truth.
    private bool IsUpToDate()
    {
        List<Vector3> expected = GetLocalPositions();
        List<CollectableBoard> boards = GetBoardChildren();
        if (boards.Count != expected.Count) return false;

        bool fromPrefab = boardPrefab != null && PrefabUtility.IsPartOfPrefabAsset(boardPrefab);
        for (int i = 0; i < boards.Count; i++)
        {
            if ((boards[i].transform.localPosition - expected[i]).sqrMagnitude > 1e-8f) return false;
            // Catches a swapped Board Prefab. Plain copies (prefab field pointing at a scene object) have no source.
            if (fromPrefab && PrefabUtility.GetCorrespondingObjectFromSource(boards[i]) != boardPrefab) return false;
        }
        return true;
    }

    [ContextMenu("Rebuild Boards")]
    public void RebuildBoards()
    {
        // Prefab assets in the Project window also get OnValidate — never write through those.
        if (Application.isPlaying || EditorUtility.IsPersistent(this)) return;

        List<CollectableBoard> old = GetBoardChildren();
        foreach (CollectableBoard board in old)
        {
            if (!CanDelete(board.gameObject))
            {
                string owner = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(board.gameObject);
                Debug.LogWarning($"{name}: can't change these boards here — they're saved inside the prefab " +
                                 $"'{owner}'. Open that prefab (double-click it) and change the stack there.", this);
                return;
            }
        }
        foreach (CollectableBoard board in old)
            DestroyImmediate(board.gameObject);

        List<Vector3> positions = GetLocalPositions();
        for (int i = 0; i < positions.Count; i++)
        {
            // Prefab instance, not a copy, so later edits to the board prefab reach every placed board.
            var board = PrefabUtility.IsPartOfPrefabAsset(boardPrefab)
                ? (CollectableBoard)PrefabUtility.InstantiatePrefab(boardPrefab, transform)
                : Instantiate(boardPrefab, transform);
            board.name = $"Board {i}";
            board.transform.SetLocalPositionAndRotation(positions[i], Quaternion.Euler(new Vector3(0f,90f,0f)));
        }

        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    // A board that is only its own CollectableBoard instance can go. One that is baked into an outer
    // prefab (the level, when edited as an instance in a scene) can't — unless it was added there.
    private static bool CanDelete(GameObject board)
    {
        GameObject outermost = PrefabUtility.GetOutermostPrefabInstanceRoot(board);
        return outermost == null || outermost == board || PrefabUtility.IsAddedGameObjectOverride(board);
    }
#endif
}
