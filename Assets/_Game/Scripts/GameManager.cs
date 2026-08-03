using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
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
    public Player player;
    public Transform playerSpawnPos;

    public List<Transform> Runners = new List<Transform>();
    List<Place> Distances = new List<Place>();
    Transform playerTransform;
    public event Action Playing;
    public event Action OnStart;
    public event Action Pause;
    public GameFlow GameFlow = GameFlow.Waiting;
    Coroutine routine;
    void Awake()
    {
        Instance = this;
    }
    public void RaiseOnStart() => OnStart?.Invoke();
    public void RaiseOnPause() => Pause?.Invoke();

    public void RegisterRunner(Transform RunnerTransform)
    {
        Runners.Add(RunnerTransform);
    }
    private void Start()
    {
        Debug.Log("Enter");
        Initialize();
        Playing -= OnPlay;
        Playing += OnPlay;
        Pause -= OnPause;
        Pause += OnPause;
    }
    void Initialize()
    {
        Player player = Instantiate(this.player, playerSpawnPos.position, Quaternion.identity, transform);
        playerTransform = player.transform;
        BotsManager.Instance.Initialize();
    }
    void OnPlay()
    {
        routine = StartCoroutine(StartCountdownRoutine());
    }
    public Transform GetPlayerTransform() { return playerTransform; }
    void FixedUpdate()
    {
        if (GameFlow != GameFlow.Playing) return;
        Distances.Clear();
        foreach (Transform Runner in Runners)
        {
            Vector3 dir = Finish.position - Runner.position;
            dir.y = 0;

            Distances.Add(new Place { Distance = dir.sqrMagnitude, RunnerTransform = Runner });
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

    private IEnumerator StartCountdownRoutine()
    {
        yield return new WaitForSeconds(3f);
        BotsManager.Instance.StartTheRun();

        //Animation.SetRunning();
        //_isGameStarted = true;
        Playing?.Invoke();

        GameFlow = GameFlow.Playing;
        routine = null;
    }
    void OnPause()
    {
        if (GameFlow == GameFlow.Pause)
            Time.timeScale = 0;
        else if (GameFlow == GameFlow.Playing)
            Time.timeScale = 1;
    }
}
public enum GameFlow
{
    Waiting,
    Playing,
    Pause,
    End
}
