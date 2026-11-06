using System;
using UnityEngine;

// The picture for each item (DA_ItemIcons). Rushed Crops > UI > Render item icons makes them.
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
