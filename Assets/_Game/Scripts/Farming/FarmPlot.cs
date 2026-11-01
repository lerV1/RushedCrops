using System;
using UnityEngine;

// One soil bed in the garden: a grid of FarmTile squares that share these settings.
// Each square is tilled, planted, grown and harvested on its own (see FarmTile).
[DisallowMultipleComponent]
public class FarmPlot : MonoBehaviour
{
    [Tooltip("The scene's shared Inventory. Seeds come from it and harvests go into it.")]
    [SerializeField] private Inventory inventory;

    [Tooltip("Every crop this bed can grow. The seeds in the selected hotbar slot pick one.")]
    [SerializeField] private CropData[] crops;

    [Tooltip("1 is normal speed, 2 grows twice as fast. Upgrades can raise this later.")]
    [SerializeField, Min(0.1f)] private float growthSpeed = 1f;

    [Tooltip("The soil colour of a watered square.")]
    [SerializeField] private Material wetSoil;

    public Inventory Inventory => inventory;
    public Material WetSoil => wetSoil;

    public float GrowthSpeed
    {
        get => growthSpeed;
        set => growthSpeed = Mathf.Max(0.1f, value);
    }

    private void Awake()
    {
        if (inventory == null)
        {
            Debug.LogError($"{name} needs the Inventory dragged into its Inventory field.", this);
            enabled = false;
        }
    }

    public bool CanGrow(CropData crop) => crop != null && Array.IndexOf(crops, crop) >= 0;

    // The crop whose seeds are in the selected hotbar slot, if this bed can grow it.
    // Null when the player isn't holding seeds.
    public CropData SelectedCrop()
    {
        if (!inventory.TryGetSelectedItem(out ResourceType held))
        {
            return null;
        }
        foreach (CropData crop in crops)
        {
            if (crop != null && crop.SeedResource == held)
            {
                return crop;
            }
        }
        return null;
    }
}
