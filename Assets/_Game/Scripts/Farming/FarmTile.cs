using UnityEngine;

// One tilled square of the crop field: plant it (Interact), water it (Use Tool) and harvest it when ripe.
// A crop that needs two waterings grows half way, dries out and waits for the second.
public class FarmTile : MonoBehaviour, IInteractable, IToolTarget
{
    public enum TileState { Untilled, Tilled, Growing, Ripe }

    [Tooltip("The dug soil mound. Hidden until the square is tilled.")]
    [SerializeField] private GameObject soil;

    [Tooltip("Height of the plant's base above the square, the top of the soil mound.")]
    [SerializeField] private float plantHeight = 0.06f;

    private FarmPlot plot;
    private GameObject plant;
    private Renderer soilRenderer;
    private Material drySoil;
    private float growTime;
    private int shownStage = -1;
    private int waterings;

    public TileState State { get; private set; } = TileState.Untilled;

    public CropData Crop { get; private set; }

    // 0 just planted, 1 ripe.
    public float Progress => Crop == null ? 0f : Mathf.Clamp01(growTime / Crop.GrowSeconds);

    public bool IsWet { get; private set; }

    public bool NeedsWater => State == TileState.Growing && !IsWet;

    private void Awake()
    {
        plot = GetComponentInParent<FarmPlot>();
        if (plot == null || soil == null)
        {
            Debug.LogError($"{name} needs a FarmPlot on a parent and its soil assigned.", this);
            enabled = false;
            return;
        }
        soilRenderer = soil.GetComponentInChildren<Renderer>(true);
        drySoil = soilRenderer != null ? soilRenderer.sharedMaterial : null;
        soil.SetActive(false);
    }

    private void Update()
    {
        if (State != TileState.Growing || !IsWet)
        {
            return;
        }

        growTime += Time.deltaTime * plot.GrowthSpeed;

        // Each watering lets the crop grow an equal share of its grow time, then the soil dries.
        float wateredUntil = Crop.GrowSeconds * waterings / Crop.WaterNeeded;
        if (growTime >= wateredUntil)
        {
            growTime = wateredUntil;
            SetWet(false);
        }

        int lastStage = Crop.StageCount - 1;
        if (Progress >= 1f)
        {
            State = TileState.Ripe;
            ShowStage(lastStage);
            return;
        }

        int stage = Mathf.Clamp(Mathf.FloorToInt(Progress * lastStage), 0, Mathf.Max(lastStage - 1, 0));
        if (stage != shownStage)
        {
            ShowStage(stage);
        }
    }

    public bool CanInteract
    {
        get
        {
            if (State == TileState.Ripe)
            {
                return plot.Inventory.CanAdd(Crop.HarvestResource, Crop.HarvestAmount);
            }
            if (State != TileState.Tilled)
            {
                return false;
            }
            CropData selected = plot.SelectedCrop();
            return selected != null && plot.Inventory.GetCount(selected.SeedResource) > 0;
        }
    }

    public string GetPrompt()
    {
        switch (State)
        {
            case TileState.Untilled:
                return "Till the soil with a hoe first";
            case TileState.Tilled:
                CropData selected = plot.SelectedCrop();
                if (selected == null)
                {
                    return "Hold seeds to plant (keys 1-5)";
                }
                int seeds = plot.Inventory.GetCount(selected.SeedResource);
                string crop = selected.DisplayName.ToLower();
                return seeds > 0 ? $"Plant {crop} seed ({seeds} left)" : $"No {crop} seeds left";
            case TileState.Growing:
                return IsWet
                    ? $"{Crop.DisplayName} growing: {Mathf.FloorToInt(Progress * 100f)}%"
                    : $"{Crop.DisplayName} needs water";
            default:
                return plot.Inventory.CanAdd(Crop.HarvestResource, Crop.HarvestAmount)
                    ? $"Harvest {Crop.DisplayName.ToLower()}"
                    : "Hotbar is full";
        }
    }

    public void Interact(PlayerInteractor player)
    {
        if (State == TileState.Tilled)
        {
            TryPlant(plot.SelectedCrop());
        }
        else if (State == TileState.Ripe)
        {
            Harvest();
        }
    }

    public string GetToolPrompt(PlayerTools tools)
    {
        if (tools.Equipped == ToolType.Hoe && State == TileState.Untilled)
        {
            return "Till soil";
        }
        if (tools.Equipped == ToolType.WateringCan && NeedsWater)
        {
            return tools.WaterLeft > 0
                ? $"Water {Crop.DisplayName.ToLower()} ({tools.WaterLeft} left)"
                : "Watering can is empty. Fill it at the river";
        }
        return null;
    }

    public bool CanUseTool(PlayerTools tools)
    {
        switch (tools.Equipped)
        {
            case ToolType.Hoe:
                return State == TileState.Untilled;
            case ToolType.WateringCan:
                return NeedsWater && tools.WaterLeft > 0;
            default:
                return false;
        }
    }

    public void UseTool(PlayerTools tools)
    {
        if (tools.Equipped == ToolType.Hoe)
        {
            Till();
        }
        else if (tools.Equipped == ToolType.WateringCan && NeedsWater && tools.UseWater())
        {
            Water();
        }
    }

    public bool Till()
    {
        if (State != TileState.Untilled)
        {
            return false;
        }

        State = TileState.Tilled;
        soil.SetActive(true);
        return true;
    }

    public bool TryPlant(CropData crop)
    {
        if (State != TileState.Tilled || !plot.CanGrow(crop) || !plot.Inventory.TrySpend(crop.SeedResource, 1))
        {
            return false;
        }

        Crop = crop;
        growTime = 0f;
        waterings = 0;
        SetWet(false); // a new seed waits for its first watering
        State = TileState.Growing;
        ShowStage(0);
        return true;
    }

    public bool Water()
    {
        if (!NeedsWater)
        {
            return false;
        }

        waterings++;
        SetWet(true);
        return true;
    }

    public bool Harvest()
    {
        if (State != TileState.Ripe || !plot.Inventory.Add(Crop.HarvestResource, Crop.HarvestAmount))
        {
            return false;
        }

        RemovePlant();
        Crop = null;
        growTime = 0f;
        waterings = 0;
        SetWet(false);
        State = TileState.Tilled;
        return true;
    }

    private void SetWet(bool wet)
    {
        IsWet = wet;
        if (soilRenderer != null && plot.WetSoil != null)
        {
            soilRenderer.sharedMaterial = wet ? plot.WetSoil : drySoil;
        }
    }

    private void ShowStage(int stage)
    {
        RemovePlant();
        shownStage = stage;
        // A fixed turn per square, so neighbours don't look copy-pasted but a plant keeps its turn as it grows.
        Quaternion turn = Quaternion.Euler(0f, (transform.GetSiblingIndex() * 137.5f) % 360f, 0f);
        plant = Instantiate(Crop.GetStageModel(stage), transform.position + Vector3.up * plantHeight, turn, transform);
    }

    private void RemovePlant()
    {
        if (plant != null)
        {
            Destroy(plant);
            plant = null;
        }
        shownStage = -1;
    }
}
