using UnityEngine;

// Anything positioned on a road by a TrackPlacement (single stacks, stamps).
// Lets one editor draw the placement fields and track handles for all of them.
public interface ITrackPlaced
{
    TrackPlacement Placement { get; }
    Transform transform { get; }

#if UNITY_EDITOR
    // One undo step covering the new placement and the resulting move.
    void SetPlacement(TrackPlacement placement, string undoName);
#endif
}
