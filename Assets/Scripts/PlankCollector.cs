using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;
public class PlankCollector : MonoBehaviour
{

     [Header("Настройки сбора досок")]
    public GameObject plankPrefab;
    public Transform stackPosition;
    public string plankTag = "Plank";
    public List<GameObject> CollectedPlanks = new List<GameObject>();
    public float plankHeight = 0.15f;
    ICharacter MainScript;
    void Start()
    {
        MainScript = GetComponent<ICharacter>();
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(plankTag))
        {
            Destroy(other.gameObject);
            AddPlankToStack();
        }
    }
    void AddPlankToStack()
    {
        GameObject newPlank = Instantiate(plankPrefab);
        newPlank.GetComponent<Collider>().enabled = false;
        newPlank.transform.SetParent(stackPosition);

        float spawnYOffset = CollectedPlanks.Count * (0.2f + 0.03f);
        newPlank.transform.localPosition = new Vector3(0, spawnYOffset, 0);
        newPlank.transform.localRotation = Quaternion.identity;

        CollectedPlanks.Add(newPlank);
        MainScript.CheckPlanks();
    }

    public void RemoveAllPlanks()
    {
        foreach(GameObject plank in CollectedPlanks)
        {
            Destroy(plank);
        }

        CollectedPlanks.Clear();
    }

        void Update()
    {
         StackWavering();
    }

    [SerializeField]float MaxOffset = 0.5f;
    [SerializeField]float SwingDirection = 1;
    [SerializeField] float force = 1;
    public void SwingForce(float side)
    {                                     
        force = Mathf.Clamp(force - 0.0002f,0,1);
        
        if(side != 0)
        {
        SwingDirection = -side;
        force = 1;
        }
       
    }
     void StackWavering()
{
    if (CollectedPlanks.Count == 0)
        return;

    int startIndex = CollectedPlanks.Count / 3;

    for (int i = 0; i < startIndex; i++)
    {
        if (CollectedPlanks[i] == null)
            continue;

        float baseY = i * 0.23f;

        CollectedPlanks[i].transform.localPosition =
            new Vector3(0, baseY, 0);
    }

    for (int i = startIndex; i < CollectedPlanks.Count; i++)
    {
        GameObject plank = CollectedPlanks[i];

        if (plank == null)
            continue;

        float normalizedHeight =
            (float)(i - startIndex + 1) /
            (CollectedPlanks.Count - startIndex);

        float heightMultiplier =
            Mathf.Pow(normalizedHeight, 2);

        float offset =
            math.sin(Time.time) *
            MaxOffset
            * heightMultiplier
            * SwingDirection
            * force;

        float baseY = i * 0.23f;

        Vector3 target = new Vector3(
            offset,
            baseY,
            0
        );

        plank.transform.localPosition =
            Vector3.Lerp(
                plank.transform.localPosition,
                target,
                Time.deltaTime * 15f
            );
    }
}
}
