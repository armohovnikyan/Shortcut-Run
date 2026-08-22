//using System.Collections.Generic;
//using UnityEngine;

//public struct Place
//{
//    public float Distance;
//    public Transform RunnerTransform;

//    public bool Passed;
//}

//public class Runner
//{
//        public Transform RunnerTransform;
//        public bool Passed;
//}
//public class GameManagerr : MonoBehaviour
//{
//    public static GameManagerr Instance;
//    public Transform Finish;
//    public List<Runner> Runners = new List<Runner>();
//    List<Place> Distances = new List<Place>();
//    void Awake()
//    {
//      Instance = this;  
//    }

//    public void RegistrRunner(Transform RunnerTransform)
//    {
//        Runners.Add(new Runner{RunnerTransform = RunnerTransform, Passed = false});
//    }

//    public void UnRegisterRunner(Transform RunnerTransform)
//    {
//        for (int i = 0; i < Runners.Count; i++)
//        {
//            if(Runners[i].RunnerTransform == RunnerTransform)
//            {
//                Runners[i].Passed = true;
//                return;
//            }
//        }
//    }

//    void FixedUpdate()
//    {
//        Distances.Clear();
//        foreach(Runner Runner in Runners)
//        {
//        Vector3 dir = Finish.position - Runner.RunnerTransform.position;
//        dir.y = 0;
        
//         if(Runner.Passed)
//         {
//             dir = Vector3.zero;
//         } 
//        Distances.Add(new Place { Distance = dir.sqrMagnitude, RunnerTransform = Runner.RunnerTransform});
//        }

//         Distances.Sort((a, b) => a.Distance.CompareTo(b.Distance));
//    }

//    public int GetMyPlace(Transform myTransform)
//    {
//        for (int i = 0; i < Distances.Count; i++)
//        {
//            if (Distances[i].RunnerTransform == myTransform)
//            {
//                return i + 1; 
//            }
//        }

//        return 0;
//    }
//}
