using UnityEngine;

/// <summary>
/// Takes boards from the carrier and lays them under the runner, one every board length plus gap
/// (PlaceableBoard.BridgeStep). The spacing comes from the board itself.
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

    /// <summary>Placed boards go under this (the level), so they're cleaned up with it. Null = scene root.</summary>
    public Transform Parent { get; set; }

    /// <summary>Metres of bridge one board covers — the same step TryPlace uses.</summary>
    public static float StepLength(PlaceableBoard board) => Mathf.Max(MinStep, board.BridgeStep);

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

        // false = keep the board's own local scale, not the world scale it had in the hands
        // (a scaled hand point would otherwise make placed boards — and the step between them — bigger).
        board.transform.SetParent(Parent, false);

        // Board is placed facing the runner's direction, so its local Z is the travel axis.
        Vector3 size = board.Size;
        step = StepLength(board);

        Vector3 position = new Vector3(feet.x, surfaceY - size.y * 1.5f, feet.z);
        board.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
        board.OnPlaced();

        lastPlaced = feet;
        hasPlaced = true;
        return true;
    }
}
