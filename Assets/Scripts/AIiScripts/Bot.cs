using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;
using System.Collections.Generic;

public class Bot : MonoBehaviour
{
    public NavMeshAgent Agent;
    public MeshRenderer Renderer;
   public PlankCollector PlanksInfo;
   public Vector3 Destination;
    public bool RunIsStarted;

    public Vector3[] Goals;

    void Start()
    {
        Agent = GetComponent<NavMeshAgent>();
        PlanksInfo = GetComponent<PlankCollector>();
        GameManager.Instance.RegistrRunner(transform);
    }
    public void Spawn(Transform Finish, Material ShirtColor,Material PantsColor,Material HairColor,Vector3[] WayPoints)
    {
        Material[] mats = Renderer.materials; 
        mats[0] = ShirtColor;
        mats[1] = PantsColor;
        mats[5] = HairColor;
        Renderer.materials = mats;   
        Goals = WayPoints;

       Destination = Finish.position;
    }
    [SerializeField] int currentWaypoint = 2;
    public bool ShortCutting;
    public float dis;


    void SetPoints()
    {
        NavMeshPath path = new NavMeshPath();

        if (Agent.CalculatePath(Destination, path))
        {
            Goals = path.corners;
        }
    }

    public int PointToCut;
    public void Update()
    {
        if (Goals == null || Goals.Length == 0 || !RunIsStarted) return;
        
        if(ShortCutting)
        {
            ShortCut();
            return;
        }

        if (!Agent.pathPending && Agent.remainingDistance <= Agent.stoppingDistance)
        {
            currentWaypoint += 2;

            if (currentWaypoint < Goals.Length)
            {
                if(PlanksInfo._collectedPlanks.Count > 5) PointToCut = CheckForBestPointForShortCut();

                if(PlanksInfo._collectedPlanks.Count > 5)
                {
                    if(PointToCut < Goals.Length)
                    {
                    PointToCut = CheckForBestPointForShortCut();
                    if(Vector3.Distance(transform.position, Goals[PointToCut]) < PlanksInfo._collectedPlanks.Count * PlanksInfo.plankSpacing)
                    {
                        currentWaypoint =  PointToCut;
                        ShortCutting = true;

                        Debug.Log("Short");
                        Agent.enabled = false;
                    }
                    else
                    {  
                    Agent.SetDestination(Goals[currentWaypoint]);         
                    }
                    }
                }
                else
                {
                    Agent.SetDestination(Goals[currentWaypoint]);        
                }          
            }
            else
            {
                Agent.SetDestination(Destination); // final finish line transform
            }
        }
    }

    int CheckForBestPointForShortCut()
    {
        float BestDist = 0;
        int BestPointIndex = currentWaypoint;
        for(int i = currentWaypoint + 2; i < Goals.Length - 5;i++)
        {
            float Dist = Vector3.Distance(transform.position, Goals[i]);
            if(Dist > PlanksInfo._collectedPlanks.Count * PlanksInfo.plankSpacing) continue;

            if(Dist > BestDist)
            {
                BestDist = Dist;
                BestPointIndex = i;
            }                   
        }

        return BestPointIndex;
    }

    void ShortCut()
    {
        
       Vector3 pos = Vector3.MoveTowards(
    transform.position,
    Goals[currentWaypoint],
    Agent.speed * Time.deltaTime);


    Vector3 direction = Goals[currentWaypoint] - transform.position;
    direction.y = 0f;
    
    if (direction != Vector3.zero)
       transform.rotation = Quaternion.LookRotation(direction);

    transform.position = pos;
    Agent.nextPosition = pos;

        if(Vector3.Distance(transform.position, Goals[currentWaypoint]) < 1)
        {
            ShortCutting = false;
            Agent.Warp(transform.position);
            Agent.enabled = true;
            Agent.SetDestination(Goals[currentWaypoint]); 
        }
    }
}
