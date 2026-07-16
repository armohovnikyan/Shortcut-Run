using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public struct Place
{
    public float Distance;
    public Transform RunnerTransform;
}
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public Transform Finish;
    public List<Transform> Runners = new List<Transform>();
    List<Place> Distances = new List<Place>();
    void Awake()
    {
      Instance = this;  
    }

    public void RegistrRunner(Transform RunnerTransform)
    {
        Runners.Add(RunnerTransform);
    }

    void FixedUpdate()
    {
        Distances.Clear();
        foreach(Transform Runner in Runners)
        {
           float Distance =  Vector2.Distance(new Vector2(Finish.position.x,Finish.position.z),new Vector2(Runner.position.x,Runner.position.z));
           Distances.Add(new Place { Distance = Distance, RunnerTransform = Runner});
        }

         Distances.Sort((a, b) => a.Distance.CompareTo(b.Distance));
    }

    public int GetMyPlace(Transform myTransform)
    {
        for (int i = 0; i < Distances.Count; i++)
        {
            if (Distances[i].RunnerTransform == myTransform)
            {
                return i + 1; 
            }
        }

        return 0;
    }
}
