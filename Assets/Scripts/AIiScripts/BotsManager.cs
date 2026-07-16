using System.Collections.Generic;
using UnityEngine;

public class BotsManager : MonoBehaviour
{
    public static BotsManager Instance;
    public List<Bot> BotsList = new List<Bot>();
    public List<Transform> SpawnPositionsForBots = new List<Transform>();
    public Material[] ColorsForBot;
    [SerializeField] GameObject BotPrefab;
    public int BotsCount;
    void Awake()
    {
      Instance = this;  
    }

    public void Start()
    {
        for(int i = 0; i < BotsCount;i++)
        {
            GameObject Bot = Instantiate(BotPrefab,SpawnPositionsForBots[i].position,transform.rotation);
            Bot botScript = Bot.GetComponent<Bot>();
            BotsList.Add(botScript);
            botScript.Spawn(GameManager.Instance.Finish,GetRandomColor(),GetRandomColor(),GetRandomColor());
        }
    }

     Material GetRandomColor()
    {
       return ColorsForBot[Random.Range(0,ColorsForBot.Length)];
    }

    public void StartTheRun()
    {
        foreach(Bot bot in BotsList)
        {
            bot.StartGoing();
        }
    }

}
