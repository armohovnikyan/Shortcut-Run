using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Collections;

public class Bot : MonoBehaviour, ICharacter, IKillable
{
    public NavMeshAgent Agent;
    public PlankStacker PlanksInfo;
    public BridgeBuilder BridgeInfo;
    public Vector3 Destination;
    public bool RunIsStarted;
    public AnimationsControl Animation;

    public GameObject Skin;
    public Transform AnimatorParent;
    public GameObject[] Skins;

    public Vector3[] Goals;

    [Header("Убийство при касании игроком")]
    [Tooltip("Насколько высоко подлетает бот (как у батута)")]
    public float knockoutHeight = 3f;
    [Tooltip("На какое расстояние вперёд по направлению игрока улетает бот")]
    public float knockoutDistance = 4f;
    [Tooltip("Сколько секунд длится дуга полёта целиком (вверх и обратно вниз)")]
    public float knockoutDuration = 1.2f;

    public bool IsKnockedOut { get; private set; }

    void Start()
    {
        Agent = GetComponent<NavMeshAgent>();
        PlanksInfo = GetComponent<PlankStacker>();
        BridgeInfo = GetComponent<BridgeBuilder>();
        Animation = GetComponent<AnimationsControl>();

        // ВРЕМЕННАЯ ДИАГНОСТИКА — удали после того, как разберёшься
        if (PlanksInfo == null) Debug.LogError($"[Bot] На {gameObject.name} нет компонента PlankStacker!");
        if (BridgeInfo == null) Debug.LogError($"[Bot] На {gameObject.name} нет компонента BridgeBuilder!");
        GameManager.Instance.RegistrRunner(transform);
    }
    public void Spawn(Transform Finish, Vector3[] WayPoints)
    {
        Goals = WayPoints;
        Destination = Finish.position;

        //Skin = Skins[Random.Range(0,Skins.Length)];
        GameObject Model = Instantiate(Skins[Random.Range(0, Skins.Length)], transform.position, transform.rotation, AnimatorParent);
        Model.name = "mixamorig:Hips";
        Animation.Rebind();
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
        if (IsKnockedOut) return;

        if (currentWaypoint == Goals.Length)
        {
            ReacedTheFinish();
            return;
        }
        if (!RunIsStarted) return;

        if (ShortCutting)
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

            if (BestForShortCut > currentWaypoint)
            {
                currentWaypoint = BestForShortCut;
                ShortCutting = true;
                Agent.enabled = false;
            }
        }
    }

    public void IsFailing()
    {
        Animation.SetFailing();
    }

    public void GetKnockedOut(Vector3 launchDirection)
    {
        if (IsKnockedOut) return; // уже улетает — повторно не реагируем

        IsKnockedOut = true;
        RunIsStarted = false;
        ShortCutting = false;

        if (Agent != null) Agent.enabled = false;
        if (BridgeInfo != null) BridgeInfo.enabled = false; // чтобы не мешал своей логикой моста/прыжка/падения

        GameManager.Instance.UnRegisterRunner(transform);

        StartCoroutine(KnockoutRoutine(launchDirection));
    }

    private IEnumerator KnockoutRoutine(Vector3 launchDirection)
    {
        launchDirection.y = 0f;
        launchDirection.Normalize();

        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < knockoutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / knockoutDuration);

            float height = 4f * knockoutHeight * t * (1f - t);
            Vector3 horizontal = launchDirection * knockoutDistance * t;

            transform.position = startPos + horizontal + Vector3.up * height;

            yield return null;
        }

        bool landedOnRoad = Physics.Raycast(
            transform.position + Vector3.up * 0.5f,
            Vector3.down,
            5f,
            BridgeInfo.roadLayer
        );

        if (landedOnRoad)
        {
            ResumeRunning();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void ResumeRunning()
    {
        IsKnockedOut = false;
        RunIsStarted = true;

        if (BridgeInfo != null) BridgeInfo.enabled = true; // возвращаем обычную логику моста

        if (Agent != null)
        {
            Agent.enabled = true;
            Agent.Warp(transform.position);
            Agent.SetDestination(Destination);
        }

        GameManager.Instance.RegistrRunner(transform);
        CheckPlanks(); // вернёт Running или RunningWithPlanks в зависимости от того, есть ли доски
    }

    public void CheckPlanks()
    {
        if (PlanksInfo.CollectedPlanks.Count > 0)
        {
            Animation.SetRunningWithPlanks();
        }
        else
        {
            Animation.SetRunning();
        }
    }

    void ReacedTheFinish()
    {
        if (RunIsStarted)
        {
            Agent.enabled = false;
            Animation.SetDance();
            RunIsStarted = false;

            GameManager.Instance.UnRegisterRunner(transform);

            StartCoroutine(GoToFinalPoint());
        }
    }

    IEnumerator GoToFinalPoint()
    {
        Vector3 Target = Finish.Instance.GetFreePoint();
        while (GetDistance(Target) > 4)
        {
            Move(Target);
            yield return null;
        }

        PlanksInfo.RemoveAllPlanks();

        Vector3 direction = Destination - transform.position;
        direction.y = 0f;
        transform.rotation = Quaternion.LookRotation(direction);
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
        if (StartIndexToCheck > Goals.Length - 3) return BestPointIndex;

        for (int i = StartIndexToCheck; i < Goals.Length - 3; i++)
        {
            float Dist = Vector3.Distance(transform.position, Goals[i]);
            if (Dist > PlanksInfo.CollectedPlanks.Count * (BridgeInfo.plankSpacing + 2)) continue;

            if (i > BestPointIndex)
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
        if (sqrDist < 64)
        {
            ShortCutting = false;
            Agent.enabled = true;
            Agent.Warp(transform.position);
            Agent.SetDestination(Destination);
        }
    }

    void Move(Vector3 Target)
    {

        Vector3 pos = Vector3.MoveTowards(transform.position, Target, Agent.speed * Time.deltaTime);
        Vector3 direction = Target - transform.position;

        direction.y = 0f;

        if (direction != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(direction);

        transform.position = pos;
        Agent.nextPosition = pos;
    }
}