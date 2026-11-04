using System;
using UnityEngine;

// The 3D model for each item, shown when the item lies on the ground (ItemDrop).
// One asset (DA_ItemModels) holds them all. Rushed Crops > Items > Set up item drops fills it.
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

    /// <summary>The model for an item, or null if it has none.</summary>
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
