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
    [SerializeField] TextOfColeectedPlanks CollecteddPlanksTxT;
    [SerializeField] GameObject AddedPlankEffect;
    int CollectedPlanksCount;

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

        float spawnYOffset = CollectedPlanks.Count * 0.1f;
        newPlank.transform.localPosition = new Vector3(0, spawnYOffset, 0);
        newPlank.transform.localRotation = Quaternion.identity;

        Instantiate(AddedPlankEffect,newPlank.transform.position,transform.rotation,stackPosition);

        CollectedPlanks.Add(newPlank);
        MainScript.CheckPlanks();

        if(MainScript is PlayerMovement)
        {
            CollectedPlanksCount++;
            Timer = 0;

            CollecteddPlanksTxT.SetText(CollectedPlanksCount,spawnYOffset * 4 + 1);
        }
    }

    public void RemoveAllPlanks()
    {
        foreach(GameObject plank in CollectedPlanks)
        {
            Destroy(plank);
        }

        CollectedPlanks.Clear();
    }
    float Timer;

    void Update()
    {
         StackWavering();
         if(MainScript is PlayerMovement && CollectedPlanksCount > 0)
         {
            if(Timer < 1)
            {
                Timer += Time.deltaTime;
            }
            else 
            {
                HideNumberOfPickedPlanks();
            }
         }
    }

    void HideNumberOfPickedPlanks()
    {
        CollecteddPlanksTxT.Fading();
        CollectedPlanksCount = 0;
    }


    [SerializeField]float MaxOffset = 0.5f;
    [SerializeField]float SwingDirection = 1;
    [SerializeField] float force = 1;
    public void SwingForce(float side)
    {                                     
        force = Mathf.Clamp(force - 0.0003f,0f,1f);
        
        if(side != 0)
        {
           force = 1;
           SwingDirection = -side;
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
    
        float baseY = i * 0.1f;
    
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

        float currentMaxOffset = MaxOffset * Mathf.Clamp(CollectedPlanks.Count / 20,0f,1f);

        float offset =
            math.sin(Time.time * 4) *
            (currentMaxOffset * force)
            * heightMultiplier
            * SwingDirection;

        float baseY = i * 0.1f;


        Vector3 target = new Vector3(
            offset,
            baseY,
            0
        );

        plank.transform.localPosition =
            Vector3.Lerp(
                plank.transform.localPosition,
                target,
                Time.deltaTime * 25f
            );
    }
}
}
