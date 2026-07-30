using System.Collections.Generic;
using UnityEngine;

// Отвечает ТОЛЬКО за сбор досок с дороги и стопку в руках персонажа.
// Ничего не знает про мост, землю, прыжок и т.д. — этим занимается BridgeBuilder.
public class PlankStacker : MonoBehaviour
{
    [Header("Настройки сбора досок")]
    public GameObject plankPrefab;
    public Transform stackPosition;
    public float plankHeight = 0.15f;
    public float plankGap = 0.03f;

    [Header("Теги")]
    public string plankTag = "Plank";

    // Список досок в руках. Публичный, чтобы BridgeBuilder мог их забирать.
    public List<GameObject> CollectedPlanks = new List<GameObject>();
    public int Count => CollectedPlanks.Count;

    private ICharacter MainScript;

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

        float spawnYOffset = CollectedPlanks.Count * (plankHeight + plankGap);
        newPlank.transform.localPosition = new Vector3(0, spawnYOffset, 0);
        newPlank.transform.localRotation = Quaternion.identity;

        CollectedPlanks.Add(newPlank);
        MainScript.CheckPlanks();
    }

    // Забирает верхнюю доску из рук. Возвращает null, если досок не осталось.
    // Дальнейшую судьбу доски (позицию, тег, слой, коллайдер) решает BridgeBuilder.
    public GameObject TakePlank()
    {
        if (CollectedPlanks.Count == 0) return null;

        int lastIndex = CollectedPlanks.Count - 1;
        GameObject plank = CollectedPlanks[lastIndex];
        CollectedPlanks.RemoveAt(lastIndex);

        MainScript.CheckPlanks();
        return plank;
    }

    public void RemoveAllPlanks()
    {
        foreach (GameObject plank in CollectedPlanks)
        {
            Destroy(plank);
        }

        CollectedPlanks.Clear();
        MainScript.CheckPlanks();
    }
}