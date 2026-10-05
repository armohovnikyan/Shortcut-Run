using System;
using UnityEngine;

/// <summary>
/// Board speed boost: raises the runner from its base speed to a fixed boosted speed, then back.
/// Only the speed and its ramps live here — WHEN the boost is on is decided by Runner.
/// Plain class: Runner owns it as a serialized field.
/// </summary>
[Serializable]
public class SpeedBoost
{
    [Tooltip("Speed while fully boosted. Normal speed is the runner's Base Speed.")]
    [SerializeField, Min(0f)] private float boostedSpeed = 9f;
    [Tooltip("Seconds to go from normal speed up to the boosted speed.")]
    [SerializeField, Min(0f)] private float rampUpTime = 0.5f;
    [Tooltip("Seconds to go from the boosted speed back to normal, once on normal road.")]
    [SerializeField, Min(0f)] private float rampDownTime = 1f;

    /// <summary>0 = normal speed, 1 = fully boosted.</summary>
    public float Amount { get; private set; }

    public float SpeedFrom(float baseSpeed) => Mathf.Lerp(baseSpeed, Mathf.Max(baseSpeed, boostedSpeed), Amount);

    /// <summary>Moves the boost towards on/off along its ramp. True = the speed changed this frame.</summary>
    public bool Tick(bool active, float deltaTime)
    {
        float target = active ? 1f : 0f;
        if (Amount == target) return false;

        float rampTime = active ? rampUpTime : rampDownTime;
        Amount = rampTime <= 0f ? target : Mathf.MoveTowards(Amount, target, deltaTime / rampTime);
        return true;
    }

    public void Clear() => Amount = 0f;
}
