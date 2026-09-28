using System;
using System.Collections.Generic;
using UnityEngine;

// A pattern of board stacks, placed on a road as one unit by TrackPlacedStamp.
// Offsets are in metres relative to the stamp's anchor and are applied along the track,
// so a long pattern bends with the road instead of sticking out straight on a curve.
[CreateAssetMenu(fileName = "BoardStamp", menuName = "Scriptable Objects/Board Stamp")]
public class BoardStampSO : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public BoardStackShapeSO shape;

        [Tooltip("Metres along the road from the stamp's anchor. + = towards the section's last knot.")]
        public float forward;

        [Tooltip("Metres across the road from the stamp's anchor. + = right. " +
                 "Metres, not -1..1, so the pattern keeps its shape on roads of any width.")]
        public float side;

        [Tooltip("Quarter turn of this stack, added to the stamp's own Facing.")]
        public TrackFacing facing;
    }

    [SerializeField] private Entry[] entries = { new Entry() };

    public IReadOnlyList<Entry> Entries => entries;

    public int BoardCount
    {
        get
        {
            int total = 0;
            foreach (Entry e in entries)
                if (e.shape != null) total += e.shape.BoardCount;
            return total;
        }
    }

#if UNITY_EDITOR
    // Level objects aren't told when an asset is edited — stamps using this one listen here.
    public static event Action<BoardStampSO> Changed;
    private void OnValidate() => Changed?.Invoke(this);
#endif
}
