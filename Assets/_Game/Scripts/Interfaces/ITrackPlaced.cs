using UnityEngine;

// Anything positioned on a road by a TrackPlacement (single stacks, stamps) — or sitting on a TrackPlatform.
// Lets one editor draw the placement fields, track handles and platform controls for all of them.
public interface ITrackPlaced
{
    TrackPlacement Placement { get; }
    /// <summary>Set = sits on this platform (its child, Transform free); the road placement is ignored.</summary>
    TrackPlatform Platform { get; }
    Transform transform { get; }

#if UNITY_EDITOR
    // One undo step covering the new placement and the resulting move.
    void SetPlacement(TrackPlacement placement, string undoName);

    /// <summary>Puts it on a platform at this pose: becomes its child, leaves the road placement.</summary>
    void PlaceOnPlatform(TrackPlatform platform, Vector3 position, Quaternion rotation, string undoName);

    /// <summary>Takes it off its platform and puts it on the nearest point of the road, centred.</summary>
    void BackToRoad(string undoName);
#endif
}
