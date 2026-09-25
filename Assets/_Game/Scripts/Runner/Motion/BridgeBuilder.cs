using UnityEngine;

/// <summary>
/// Takes boards from the carrier and lays them under the runner, one every half board-width travelled.
/// The placer decides the spacing, measured from the board itself.
/// </summary>
public class BridgeBuilder
{
    private const float MinStep = 0.05f;

    private readonly BoardCarrier carrier;
    private Vector3 lastPlaced;
    private bool hasPlaced;
    private float step;

    public BridgeBuilder(BoardCarrier carrier)
    {
        this.carrier = carrier;
    }

    public bool HasBoards => carrier.HasBoards;

    /// <summary>Call when a new bridge starts, so the first board goes down immediately.</summary>
    public void Begin() => hasPlaced = false;

    public bool NeedsBoard(Vector3 feet)
    {
        if (!hasPlaced) return true;

        Vector3 moved = feet - lastPlaced;
        moved.y = 0f;
        return moved.sqrMagnitude >= step * step;
    }

    /// <summary>Places one board under the feet with its top at surfaceY. False = no boards left.</summary>
    public bool TryPlace(Vector3 feet, Vector3 forward, float surfaceY)
    {
        if (!(carrier.TakeLast() is PlaceableBoard board)) return false;

        board.transform.SetParent(null);

        // Board is placed facing the runner's direction, so its local Z is the travel axis.
        Vector3 size = board.Size;
        step = Mathf.Max(MinStep, size.z * 0.5f);

        Vector3 position = new Vector3(feet.x, surfaceY - size.y * 0.5f, feet.z);
        board.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
        board.OnPlaced();

        lastPlaced = feet;
        hasPlaced = true;
        return true;
    }
}
