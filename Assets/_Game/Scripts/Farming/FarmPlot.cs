using System;
using UnityEngine;

// Settings every square of the crop field shares: the Inventory, the crops and the growth speed.
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

    public event Action Harvested;

    [SerializeField] private Health playerHealth;
    [SerializeField] private GameManager gameManager;

    public Inventory Inventory => inventory;
    public Health PlayerHealth => playerHealth;
    public GameManager GameManager => gameManager;
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

    // Only FarmTile calls this after a successful harvest, so buying produce does not count.
    public void RecordHarvest()
    {
        Harvested?.Invoke();
    }

    public bool CanGrow(CropData crop) => crop != null && Array.IndexOf(crops, crop) >= 0;

    public CropData FindCrop(ResourceType seedResource)
    {
        foreach (CropData crop in crops)
        {
            if (crop != null && crop.SeedResource == seedResource)
            {
                return crop;
            }
        }
        return null;
    }

    // The crop whose seeds are in hand, or null.
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
