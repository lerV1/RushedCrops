using System;
using System.Collections.Generic;
using UnityEngine;

// One soil bed in the garden. Look at it and press Interact to plant the selected seed,
// wait while it grows through its stages, then press Interact again to harvest.
//
// Planting uses one seed from the Inventory and fills every crop spot in the bed.
// Harvesting adds the crop's harvest amount to the Inventory and empties the bed.
[DisallowMultipleComponent]
public class FarmPlot : MonoBehaviour, IInteractable
{
    public enum PlotState { Empty, Growing, Ripe }

    [Tooltip("The scene's shared Inventory. Seeds come from it and harvests go into it.")]
    [SerializeField] private Inventory inventory;

    [Tooltip("Every crop this plot can grow. The GameManager's selected crop picks one.")]
    [SerializeField] private CropData[] crops;

    [Tooltip("Where the plants appear, usually on top of the soil ridges.")]
    [SerializeField] private Transform[] spots;

    [Tooltip("1 is normal speed, 2 grows twice as fast. Upgrades can raise this later.")]
    [SerializeField, Min(0.1f)] private float growthSpeed = 1f;

    private readonly List<GameObject> plants = new List<GameObject>();
    private float growTime;
    private int shownStage = -1;

    public PlotState State { get; private set; } = PlotState.Empty;

    // The crop in the bed, or null when it is empty.
    public CropData Crop { get; private set; }

    // 0 just planted, 1 ripe.
    public float Progress => Crop == null ? 0f : Mathf.Clamp01(growTime / Crop.GrowSeconds);

    public float GrowthSpeed
    {
        get => growthSpeed;
        set => growthSpeed = Mathf.Max(0.1f, value);
    }

    // Raised after planting, each new growth stage, ripening and harvest.
    public event Action<FarmPlot> Changed;

    private void Awake()
    {
        if (inventory == null)
        {
            Debug.LogError($"{name} needs the Inventory dragged into its Inventory field.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (State != PlotState.Growing)
        {
            return;
        }

        growTime += Time.deltaTime * growthSpeed;

        // Split the grow time evenly between the stages; the last stage is the ripe plant.
        int lastStage = Crop.StageCount - 1;
        int stage = Mathf.Clamp(Mathf.FloorToInt(Progress * lastStage), 0, Mathf.Max(lastStage - 1, 0));

        if (Progress >= 1f)
        {
            State = PlotState.Ripe;
            ShowStage(lastStage);
            Changed?.Invoke(this);
        }
        else if (stage != shownStage)
        {
            ShowStage(stage);
            Changed?.Invoke(this);
        }
    }

    // ---------- IInteractable ----------

    public bool CanInteract
    {
        get
        {
            switch (State)
            {
                case PlotState.Empty:
                    CropData selected = SelectedCrop();
                    return selected != null && inventory.GetCount(selected.SeedResource) > 0;
                case PlotState.Ripe:
                    return true;
                default:
                    return false;
            }
        }
    }

    public string GetPrompt()
    {
        switch (State)
        {
            case PlotState.Empty:
                CropData selected = SelectedCrop();
                if (selected == null)
                {
                    return null;
                }
                int seeds = inventory.GetCount(selected.SeedResource);
                string crop = selected.DisplayName.ToLower();
                return seeds > 0 ? $"Plant {crop} seed ({seeds} left)" : $"No {crop} seeds left";
            case PlotState.Growing:
                return $"{Crop.DisplayName}s growing: {Mathf.FloorToInt(Progress * 100f)}%";
            default:
                return $"Harvest {Crop.DisplayName.ToLower()}s";
        }
    }

    public void Interact(PlayerInteractor player)
    {
        if (State == PlotState.Empty)
        {
            TryPlant(SelectedCrop());
        }
        else if (State == PlotState.Ripe)
        {
            Harvest();
        }
    }

    // ---------- Farming ----------

    // Plants one seed of this crop. Returns false and changes nothing if the bed is not
    // empty, the crop can't grow here or the player has no seeds.
    public bool TryPlant(CropData crop)
    {
        if (State != PlotState.Empty || crop == null || Array.IndexOf(crops, crop) < 0)
        {
            return false;
        }
        if (!inventory.TrySpend(crop.SeedResource, 1))
        {
            return false;
        }

        Crop = crop;
        growTime = 0f;
        State = PlotState.Growing;
        ShowStage(0);
        Changed?.Invoke(this);
        return true;
    }

    // Picks a ripe crop. Returns false if nothing is ripe.
    public bool Harvest()
    {
        if (State != PlotState.Ripe)
        {
            return false;
        }

        inventory.Add(Crop.HarvestResource, Crop.HarvestAmount);
        ClearPlants();
        Crop = null;
        growTime = 0f;
        State = PlotState.Empty;
        Changed?.Invoke(this);
        return true;
    }

    // The crop matching the GameManager's selected crop type, if this plot can grow it.
    private CropData SelectedCrop()
    {
        CropType selected = GameManager.Instance != null ? GameManager.Instance.SelectedCrop : CropType.Carrot;
        foreach (CropData crop in crops)
        {
            if (crop != null && crop.CropType == selected)
            {
                return crop;
            }
        }
        return null;
    }

    // Swaps every plant in the bed for this stage's model.
    private void ShowStage(int stage)
    {
        ClearPlants();
        shownStage = stage;
        GameObject model = Crop.GetStageModel(stage);
        for (int i = 0; i < spots.Length; i++)
        {
            if (spots[i] == null)
            {
                continue;
            }
            // A fixed turn per spot so the rows don't look copy-pasted but stay the same every stage.
            Quaternion turn = Quaternion.Euler(0f, (i * 137.5f) % 360f, 0f);
            plants.Add(Instantiate(model, spots[i].position, turn, spots[i]));
        }
    }

    private void ClearPlants()
    {
        foreach (GameObject plant in plants)
        {
            Destroy(plant);
        }
        plants.Clear();
        shownStage = -1;
    }
}
