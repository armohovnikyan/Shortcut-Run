using System;
using UnityEngine;

// Pre-baked list of stack shapes the level can pick from at random.
[CreateAssetMenu(fileName = "BoardStackCatalog", menuName = "Scriptable Objects/Board Stack Catalog")]
public class BoardStackCatalogSO : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public BoardStackShapeSO shape;
        [Tooltip("Relative chance. 2 is picked twice as often as 1. 0 = never.")]
        [Min(0f)] public float weight;
    }

    [SerializeField] private Entry[] stacks;

    public BoardStackShapeSO GetRandom()
    {
        float total = 0f;
        foreach (Entry e in stacks)
            if (e.shape != null) total += e.weight;

        if (total <= 0f) return null;

        float roll = UnityEngine.Random.value * total;
        foreach (Entry e in stacks)
        {
            if (e.shape == null) continue;
            roll -= e.weight;
            if (roll <= 0f) return e.shape;
        }
        return null;
    }
}
