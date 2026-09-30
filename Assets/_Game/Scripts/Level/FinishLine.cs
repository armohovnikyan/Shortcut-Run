using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trigger across the finish. Reports each runner once when it crosses — Player and NPC alike.
/// What crossing means (place, finish flow, bonus) is the run manager's decision.
/// </summary>
[RequireComponent(typeof(Collider), typeof(Rigidbody))]
public class FinishLine : MonoBehaviour
{
    private readonly HashSet<Runner> crossed = new HashSet<Runner>();

    public event Action<Runner> RunnerCrossed;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        // Kinematic body on the line itself, so trigger events fire for any runner collider —
        // NPCs have no Rigidbody of their own.
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Any child collider counts (pickup sphere, body) — the HashSet keeps it to one report per runner.
        Runner runner = other.GetComponentInParent<Runner>();
        if (runner != null && crossed.Add(runner))
            RunnerCrossed?.Invoke(runner);
    }
}
