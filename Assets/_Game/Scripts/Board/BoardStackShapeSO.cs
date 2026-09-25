using System.Collections.Generic;
using UnityEngine;

// One stack shape = how many boards sit in each row. Positions are calculated by BoardStack.
[CreateAssetMenu(fileName = "BoardStackShape", menuName = "Scriptable Objects/Board Stack Shape")]
public class BoardStackShapeSO : ScriptableObject
{
    [Tooltip("Boards per row, bottom row first. Rows are centered, so a shorter row sits in the gaps of the row below. " +
             "3,2,1 = 6-board pyramid.")]
    [SerializeField] private int[] rows = { 2, 1 };

    public IReadOnlyList<int> Rows => rows;

    public int BoardCount
    {
        get
        {
            int total = 0;
            foreach (int count in rows) total += count;
            return total;
        }
    }
}
