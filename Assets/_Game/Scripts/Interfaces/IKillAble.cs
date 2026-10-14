using UnityEngine;

public interface IKillAble
{
    /// <summary>Knocks the runner off the road. False = it can't be knocked right now (not racing, already out).</summary>
    bool TryKnockOut(Vector3 launchDirection);
}
