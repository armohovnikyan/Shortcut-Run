using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
public class BotsManager : MonoBehaviour
{
    public static BotsManager Instance;
    public List<Bot> BotsList = new List<Bot>();
    public List<Transform> SpawnPositionsForBots = new List<Transform>();
    public Material[] ColorsForBot;
    [SerializeField] GameObject BotPrefab;
    [SerializeField] SplineContainer Road;
    [SerializeField] Vector3[] Points;
    public int BotsCount;
    int sampleCount = 24;
    void Awake()
    {
      Instance = this;  
    }

     public void GetAllSplinePoints()
    {

        Vector3[] points = new Vector3[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)(sampleCount - 1); // 0 to 1
            float3 localPos = Road.EvaluatePosition(t);
            points[i] = localPos;
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

    public void Initialize()
    {
        GetAllSplinePoints();
        for(int i = 0; i < BotsCount;i++)
        {
            GameObject Bot = Instantiate(BotPrefab,SpawnPositionsForBots[i].position,transform.rotation);
            Bot botScript = Bot.GetComponent<Bot>();
            BotsList.Add(botScript);
            GameManager.Instance.RegisterRunner(Bot.transform);
            botScript.Spawn(GameManager.Instance.Finish,Points);
        }
    }

     Material GetRandomColor()
    {
       return ColorsForBot[UnityEngine.Random.Range(0,ColorsForBot.Length)];
    }

    public void StartTheRun()
    {
        foreach(Bot bot in BotsList)
        {
             bot.StartRun();
        }
    }

}
