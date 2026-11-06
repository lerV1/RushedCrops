using System;
using UnityEngine;

// The model shown for each item lying on the ground (DA_ItemModels).
[CreateAssetMenu(fileName = "DA_ItemModels", menuName = "Rushed Crops/Item Models")]
public class ItemModels : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public ResourceType item;
        public GameObject model;
        [Tooltip("How the model is turned on the ground, in degrees.")]
        public Vector3 turn;
    }

    [SerializeField] private Entry[] models = new Entry[0];

    public GameObject Get(ResourceType item) => Find(item).model;

    public Vector3 TurnFor(ResourceType item) => Find(item).turn;

    private Entry Find(ResourceType item)
    {
        foreach (Entry entry in models)
        {
            if (entry.item == item)
            {
                return entry;
            }
        }
        return default;
    }
}
