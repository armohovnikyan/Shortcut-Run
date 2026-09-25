using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds the boards a runner is currently carrying, and the speed penalty
/// that causes. Pure data and math — no animation, no movement.
/// Plain class: Runner owns it as a serialized field.
/// </summary>
[Serializable]
public class BoardCarrier
{
    [Space]
    [Header("Carry penalty")]
    [Tooltip("Speed lost per board in hand (0.02 = -2% speed per board)")]
    [SerializeField] private float speedPenaltyPerBoard = 0.02f;
    [Tooltip("Lowest carry multiplier, no matter how many boards")]
    [SerializeField] private float minCarryMultiplier = 0.6f;

    private readonly List<BaseBoard> _carriedBoards = new List<BaseBoard>();

    public IReadOnlyList<BaseBoard> CarriedBoards => _carriedBoards;
    public int Count => _carriedBoards.Count;
    public bool HasBoards => _carriedBoards.Count > 0;

    public float CarryMultiplier =>
        Mathf.Max(minCarryMultiplier, 1f - _carriedBoards.Count * speedPenaltyPerBoard);

    public void Add(BaseBoard board) => _carriedBoards.Add(board);

    /// <summary>Removes and returns the most recently picked-up board, or null if none held.</summary>
    public BaseBoard TakeLast()
    {
        if (_carriedBoards.Count == 0) return null;

        int lastIndex = _carriedBoards.Count - 1;
        BaseBoard board = _carriedBoards[lastIndex];
        _carriedBoards.RemoveAt(lastIndex);
        return board;
    }

    /// <summary>Destroys the boards still in hand. Already placed boards are not in the list, so they stay.</summary>
    public void RemoveAll()
    {
        foreach (BaseBoard board in _carriedBoards)
            if (board != null) UnityEngine.Object.Destroy(board.gameObject);

        _carriedBoards.Clear();
    }
}
