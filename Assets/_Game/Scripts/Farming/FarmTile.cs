using UnityEngine;

// One square of a farm bed. Each square is farmed on its own:
//   1. Till it with the hoe (Use Tool).            Untilled -> Tilled
//   2. Plant the selected seed (Interact).          Tilled   -> Growing
//   3. Water it with the watering can (Use Tool).   A dry crop doesn't grow.
//      A crop that needs water twice grows half way, dries out and waits for the second watering.
//   4. Harvest it when ripe (Interact). The soil stays tilled, ready for the next seed.
// The bed it belongs to (FarmPlot on a parent) holds the Inventory, the crops and the growth speed.
[RequireComponent(typeof(Collider))]
public class FarmTile : MonoBehaviour, IInteractable, IToolTarget, IHighlightable
{
    public enum TileState { Untilled, Tilled, Growing, Ripe }

    [Tooltip("The dug soil mound. Hidden until the square is tilled.")]
    [SerializeField] private GameObject soil;

    [Tooltip("The outline shown while the player aims at this square.")]
    [SerializeField] private GameObject highlight;

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

    // The crop in the square, or null when nothing is planted.
    public CropData Crop { get; private set; }

    // 0 just planted, 1 ripe.
    public float Progress => Crop == null ? 0f : Mathf.Clamp01(growTime / Crop.GrowSeconds);

    // True while the soil is watered and the crop is growing.
    public bool IsWet { get; private set; }

    // A planted crop waiting for the watering can.
    public bool NeedsWater => State == TileState.Growing && !IsWet;

    private void Awake()
    {
        plot = GetComponentInParent<FarmPlot>();
        if (plot == null || soil == null || highlight == null)
        {
            Debug.LogError($"{name} needs a FarmPlot on a parent and its soil and highlight assigned.", this);
            enabled = false;
            return;
        }
        soilRenderer = soil.GetComponentInChildren<Renderer>(true);
        drySoil = soilRenderer != null ? soilRenderer.sharedMaterial : null;
        soil.SetActive(false);
        highlight.SetActive(false);
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

        // Split the grow time evenly between the stages; the last stage is the ripe plant.
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

    // ---------- Interact (E): plant and harvest ----------

    public bool CanInteract
    {
        get
        {
            if (State == TileState.Ripe)
            {
                return true;
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
                    return null;
                }
                int seeds = plot.Inventory.GetCount(selected.SeedResource);
                string crop = selected.DisplayName.ToLower();
                return seeds > 0 ? $"Plant {crop} seed ({seeds} left)" : $"No {crop} seeds left";
            case TileState.Growing:
                return IsWet
                    ? $"{Crop.DisplayName} growing: {Mathf.FloorToInt(Progress * 100f)}%"
                    : $"{Crop.DisplayName} needs water";
            default:
                return $"Harvest {Crop.DisplayName.ToLower()}";
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

    // ---------- Use Tool (left click): till and water ----------

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

    public void SetHighlighted(bool on)
    {
        if (highlight != null)
        {
            highlight.SetActive(on);
        }
    }

    // ---------- Farming ----------

    // Digs the square so it can be planted. Returns false if it is already tilled.
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

    // Plants one seed of this crop. Returns false and changes nothing if the square isn't
    // tilled and empty, the bed can't grow that crop or the player has no seeds.
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

    // Waters a dry crop so it grows its next share. Returns false if nothing here needs water.
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

    // Picks a ripe crop into the Inventory. The soil stays tilled. Returns false if nothing is ripe.
    public bool Harvest()
    {
        if (State != TileState.Ripe)
        {
            return false;
        }

        plot.Inventory.Add(Crop.HarvestResource, Crop.HarvestAmount);
        RemovePlant();
        Crop = null;
        growTime = 0f;
        waterings = 0;
        SetWet(false);
        State = TileState.Tilled;
        return true;
    }

    // Darker soil while watered.
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
