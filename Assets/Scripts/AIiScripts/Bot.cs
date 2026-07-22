using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Collections;

public class Bot : MonoBehaviour
{
    public NavMeshAgent Agent;
   public PlankCollector PlanksInfo;
   public Vector3 Destination;
    public bool RunIsStarted;
    public AnimationsControl Animation;

    public Vector3[] Goals;

    void Start()
    {
        Agent = GetComponent<NavMeshAgent>();
        PlanksInfo = GetComponent<PlankCollector>();
        Animation = GetComponent<AnimationsControl>();
        GameManager.Instance.RegistrRunner(transform);
    }
    public void Spawn(Transform Finish,Vector3[] WayPoints)
    { 
        Goals = WayPoints;
        Destination = Finish.position;

        Animation.SetIdle();
    }
    [SerializeField] int currentWaypoint = 2;
    public bool ShortCutting;

    public int PointToCut;
    public int BestForShortCut;

    public void StartRun()
    {
        Agent.SetDestination(Destination);
        RunIsStarted = true;

        Animation.SetRunning();
    }
    public void Update()
    {
        
        if (currentWaypoint == Goals.Length)
        {
           ReacedTheFinish();
           return;
        }
        if(!RunIsStarted) return;

         if(ShortCutting)
        {
            ShortCut();
            return;
        }

        float sqrDist = GetDistance(Goals[currentWaypoint]);
        
        if (sqrDist < 64)
        {
           currentWaypoint++;

            if (currentWaypoint >= Goals.Length)
            return;

           BestForShortCut = CheckForBestPointForShortCut();

           Debug.Log("ReachPoint");

            if(BestForShortCut > currentWaypoint)
        {
            currentWaypoint = BestForShortCut;
            ShortCutting = true;
            Agent.enabled = false;
        }
        }    
    }

    void ReacedTheFinish()
    {
        if(RunIsStarted)
        {
        Agent.enabled = false;
        Animation.SetDance();
        RunIsStarted = false;

        StartCoroutine(GoToFinalPoint());
        }
    }

    IEnumerator GoToFinalPoint()
    {
        Vector3 Target = Finish.Instance.GetFreePoint();
        while(GetDistance(Target) > 4)
        {
        Move(Target);
        yield return null;
        }
    }

    float GetDistance(Vector3 Point)
    {
        Vector3 dir = Point - transform.position;
        dir.y = 0;
        
        return dir.sqrMagnitude;
    }

    int CheckForBestPointForShortCut()
    {
        int BestPointIndex = currentWaypoint;
        int StartIndexToCheck = currentWaypoint + 2;
        if(StartIndexToCheck > Goals.Length - 3) return BestPointIndex;

        for(int i = StartIndexToCheck; i < Goals.Length - 3;i++)
        {
            float Dist = Vector3.Distance(transform.position, Goals[i]);
            if(Dist > PlanksInfo._collectedPlanks.Count * (PlanksInfo.plankSpacing + 0.5)) continue;

            if(i > BestPointIndex)
            {
                BestPointIndex = i;
            }                   
        }

        return BestPointIndex;
    }

    void ShortCut()
    {
        Move(Goals[currentWaypoint]);
            
        float sqrDist = GetDistance(Goals[currentWaypoint]);
        if(sqrDist < 64)
        {
            ShortCutting = false;
            Agent.enabled = true;
            Agent.Warp(transform.position);
            Agent.SetDestination(Destination); 
        }
    }

    void Move(Vector3 Target)
    {

    Vector3 pos = Vector3.MoveTowards(transform.position,Target,Agent.speed * Time.deltaTime);  
    Vector3 direction = Target - transform.position;

    direction.y = 0f;
    
    if (direction != Vector3.zero)
       transform.rotation = Quaternion.LookRotation(direction);

        transform.position = pos;
    Agent.nextPosition = pos;
    }
}
