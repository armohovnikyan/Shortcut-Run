using System;
using UnityEngine;

/// <summary>Player-only horizontal movement: turn from input, then step forward. Height is RunnerMotion's job.</summary>
[Serializable]
public class PlayerSteering
{
    [Space]
    [SerializeField] private float turnSpeed = 90f;

    public void Rotate(Transform transform, float turnInput, float deltaTime)
    {
        if (Mathf.Abs(turnInput) > 0.01f)
            transform.Rotate(0f, turnInput * turnSpeed * deltaTime, 0f);
    }

    public Vector3 ForwardStep(Transform transform, float speed, float deltaTime)
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.normalized * speed * deltaTime;
    }
}
