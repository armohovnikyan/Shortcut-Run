using System;
using UnityEngine;

[Serializable]
public class FinishPoint
{
    public bool IsOcupied;
    public Transform Pos;
}

public class Finish : MonoBehaviour
{
    public static Finish Instance;

    [Tooltip("Точки, где встают финишировавшие бегуны — чтобы не стояли друг на друге")]
    public FinishPoint[] StayPoints;

    void Awake()
    {
        Instance = this;
    }

    // Возвращает первую свободную точку стоянки и сразу занимает её.
    // Если свободных нет — отдаёт позицию самого финиша (запасной вариант).
    public Vector3 GetFreePoint()
    {
        for (int i = 0; i < StayPoints.Length; i++)
        {
            if (!StayPoints[i].IsOcupied)
            {
                StayPoints[i].IsOcupied = true;
                return StayPoints[i].Pos.position;
            }
        }

        return transform.position;
    }
}