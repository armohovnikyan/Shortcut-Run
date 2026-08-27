using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Linq;
using Unity.AI.Navigation;
public class BotsManager : MonoBehaviour
{
    public static BotsManager Instance;
    public List<Bot> BotsList = new List<Bot>();
    public List<Transform> SpawnPositionsForBots = new List<Transform>();
    public Material[] ColorsForBot;
    [SerializeField] GameObject BotPrefab;
    [SerializeField] SplineContainer[] Roads;
    [SerializeField] Vector3[] Points;
    private NavMeshSurface navSurface;
    public int BotsCount;
    public int sampleCount = 24;
    void Awake()
    {
      Instance = this;  
      navSurface = GetComponent<NavMeshSurface>();
    }

     public void GetAllSplinePoints()
    {
        int CountOfPointsForEach = sampleCount / Roads.Count();

        Vector3[] points = new Vector3[sampleCount];
        int NumberOfRoad = 0;

        foreach(SplineContainer road in Roads)
        {
          for (int i = 0; i < CountOfPointsForEach; i++)
          {
              float t = i / (float)(CountOfPointsForEach - 1); // 0 to 1
              float3 localPos = road.EvaluatePosition(t);
              points[i + (NumberOfRoad * CountOfPointsForEach)] = localPos;
          }
          NumberOfRoad++;
        }

        Points = points;
    }

    private void OnDrawGizmos()
{
    if (Points == null) return;

    Gizmos.color = Color.red;

    foreach (var p in Points)
    {
        Gizmos.DrawSphere(p, 0.3f);
    }
}

    public void BakeSurface()
    {
        
    }

    public void Start()
    {
        GetAllSplinePoints();
        for(int i = 0; i < BotsCount;i++)
        {
            GameObject Bot = Instantiate(BotPrefab,SpawnPositionsForBots[i].position,transform.rotation);
            Bot botScript = Bot.GetComponent<Bot>();
            BotsList.Add(botScript);
            botScript.Spawn(GameManager.Instance.Finish,Points);
        }
    }

    public void StartTheRun()
    {
         navSurface.BuildNavMesh();
        foreach(Bot bot in BotsList)
        {
             bot.StartRun();
        }
    }

}
