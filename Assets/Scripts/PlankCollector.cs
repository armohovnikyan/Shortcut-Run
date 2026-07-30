using UnityEngine;
using System.Collections.Generic;
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
}
