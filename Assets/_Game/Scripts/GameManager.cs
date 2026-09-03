using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct Place
{
    public float Distance;
    public Transform RunnerTransform;
    public bool Passed;
}
public class Runner
{
    public Transform transform;
    public bool Passed;

    public Runner(Transform runner, bool passed)
    {
        transform = runner;
        Passed = passed;
    }
}
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public Transform Finish;
    public Player _player;
    public Transform playerSpawnPos;

    public List<Runner> Runners = new List<Runner>();
    List<Place> Distances = new List<Place>();
    Transform playerTransform;

    public event Action Playing;
    public event Action OnStart;
    public event Action Pause;
    public event Action OnDeath;
    public event Action OnRestart;
    public event Action<int> OnFinished;
    
    public GameFlow GameFlow = GameFlow.Waiting;
    Coroutine routine;
    
    void Awake()
    {
        Instance = this;
    }
    public void RaiseOnStart() => OnStart?.Invoke();
    public void RaiseOnPause() => Pause?.Invoke();
    public void RaiseOnFinished(int place) => OnFinished?.Invoke(place);
    public void RaiseOnDeath() => OnDeath?.Invoke();
    public void RaiseOnRestart() => OnRestart?.Invoke();    
    private void Start()
    {
        Initialize();
        
        UIManager.Instance.OnCountdownFinished -= OnPlay;
        UIManager.Instance.OnCountdownFinished += OnPlay;
        
        Pause -= OnPause;
        Pause += OnPause;
        
        OnFinished -= Finished;
        OnFinished += Finished;
        
        OnDeath -= Death;
        OnDeath += Death;

        OnRestart -= Restart;
        OnRestart += Restart;

    }
    void Initialize()
    {
        Player player = Instantiate(_player, playerSpawnPos.position, Quaternion.identity, transform);
        playerTransform = player.transform;
        BotsManager.Instance.Initialize();
    }
    public Transform GetPlayerTransform() { return playerTransform; }
    public void RegisterRunner(Transform runner)
    {
        Runners.Add(new Runner(runner, false));
    }
    public void UnregisterRunner(Transform targetRunner)
    {
        foreach (var runner in Runners)
        {
            if (runner.transform == targetRunner)
            {
                runner.Passed = true;
                return;
            }
        }
    }
    void FixedUpdate()
    {
        if (GameFlow != GameFlow.Playing) return;
        Distances.Clear();
        foreach (var runner in Runners)
        {
            Vector3 dir = Finish.position - runner.transform.position;
            dir.y = 0;

            Distances.Add(new Place { Distance = dir.sqrMagnitude, RunnerTransform = runner.transform });
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
    void OnPlay()
    {
        BotsManager.Instance.StartTheRun();
        GameFlow = GameFlow.Playing;
    }
    void OnPause()
    {
        if (GameFlow == GameFlow.Pause)
            Time.timeScale = 0;
        else if (GameFlow == GameFlow.Playing)
            Time.timeScale = 1;
    }
    void Finished(int place)
    {
        if (place == 1)
        {
            //FirstPlaceLogic
        }
        else
        {
            //StartCoroutine(GoToFinalPoint());
            //cameraFollow.RaceEnded();
            //_isGameStarted = false;
        }
    }
    void Death()
    {
        UIManager.Instance.RaiseOnDeath();
        GameFlow = GameFlow.Died;
    }
    void Restart()
    {
        string scene = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(scene);
    }
}
public enum GameFlow
{
    Waiting,
    Playing,
    Pause,
    Died,
    End
}
//void FixedUpdate()
//{
//    Distances.Clear();
//    foreach (Runner Runner in Runners)
//    {
//        Vector3 dir = Finish.position - Runner.RunnerTransform.position;
//        dir.y = 0;

//        if (Runner.Passed)
//        {
//            dir = Vector3.zero;
//        }
//        Distances.Add(new Place { Distance = dir.sqrMagnitude, RunnerTransform = Runner.RunnerTransform });
//    }

//    Distances.Sort((a, b) => a.Distance.CompareTo(b.Distance));
//}

//public int GetMyPlace(Transform myTransform)
//{
//    for (int i = 0; i < Distances.Count; i++)
//    {
//        if (Distances[i].RunnerTransform == myTransform)
//        {
//            return i + 1;
//        }
//    }

//    return 0;
//}