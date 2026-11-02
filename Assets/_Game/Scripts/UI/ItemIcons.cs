using System;
using UnityEngine;

// The picture for each item, which the hotbar shows instead of just a name.
// One asset (DA_ItemIcons) holds them all. Rushed Crops > UI > Render item icons makes the pictures
// from the item models and fills this list.
[CreateAssetMenu(fileName = "DA_ItemIcons", menuName = "Rushed Crops/Item Icons")]
public class ItemIcons : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public ResourceType item;
        public Sprite icon;
    }

    [SerializeField] private Entry[] icons = new Entry[0];

    /// <summary>The picture for an item, or null if it has none.</summary>
    public Sprite Get(ResourceType item)
    {
        foreach (Entry entry in icons)
        {
            if (entry.item == item)
            {
                return entry.icon;
            }
        }
        return null;
    }
}
