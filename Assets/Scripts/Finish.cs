using System;
using UnityEngine;

[Serializable] public class FinishPoint
{
    public bool IsOcupied;
    public Transform Pos;
}
public class Finish : MonoBehaviour
{
    public FinishPoint[] StayPoints;
    public static Finish Instance;

    void Awake()
    {
        Instance = this;
    }

    public Vector3 GetFreePoint()
    {
        for(int i = 0; i < StayPoints.Length;i++)
        {
            if(!StayPoints[i].IsOcupied)
            {
                StayPoints[i].IsOcupied = true;
                return StayPoints[i].Pos.position;
            }
        }

        return transform.position;
    }
}
