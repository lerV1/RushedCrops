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

    public SaveData.CropTileData GetSaveData(Vector2Int square)
    {
        return new SaveData.CropTileData
        {
            x = square.x,
            y = square.y,
            state = (int)State,
            seedResource = Crop != null ? (int)Crop.SeedResource : -1,
            progress = Progress,
            waterings = waterings,
            isWet = IsWet
        };
    }

    public void RestoreState(SaveData.CropTileData saved, FarmPlot ownerPlot)
    {
        plot = ownerPlot;
        State = (TileState)Mathf.Clamp(saved.state, (int)TileState.Tilled, (int)TileState.Ripe);
        soil.SetActive(true);
        Crop = saved.seedResource >= 0 ? plot.FindCrop((ResourceType)saved.seedResource) : null;
        if (State == TileState.Tilled || Crop == null)
        {
            State = TileState.Tilled;
            Crop = null;
            growTime = 0f;
            waterings = 0;
            SetWet(false);
            return;
        }

        growTime = Mathf.Clamp01(saved.progress) * Crop.GrowSeconds;
        waterings = Mathf.Clamp(saved.waterings, 0, Crop.WaterNeeded);
        SetWet(State == TileState.Growing && saved.isWet);
        if (State == TileState.Ripe)
        {
            growTime = Crop.GrowSeconds;
        }
        ShowStage(State == TileState.Ripe ? Crop.StageCount - 1 :
            Mathf.Clamp(Mathf.FloorToInt(Progress * (Crop.StageCount - 1)), 0, Crop.StageCount - 2));
    }

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
            // A boss seed doesn't give a harvest: its boss climbs out of the soil instead.
            if (Crop.IsBossSeed)
            {
                GrowBoss();
                return;
            }
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
                    return "Hold seeds to plant (keys 1-9)";
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
        GameEffects.Play(Effect.Dirt, transform.position + Vector3.up * 0.1f);
        GameAudio.Play(Sfx.Till, transform.position);
        PopIn.On(soil, 0.25f);
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
        GameEffects.Play(Effect.Dirt, transform.position + Vector3.up * 0.1f, 0.5f);
        GameAudio.Play(Sfx.Plant, transform.position);
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
        GameEffects.Play(Effect.Water, transform.position + Vector3.up * 0.15f);
        GameAudio.Play(Sfx.Water, transform.position);
        return true;
    }

    public bool Harvest()
    {
        if (State != TileState.Ripe || !plot.Inventory.Add(Crop.HarvestResource, Crop.HarvestAmount))
        {
            return false;
        }

        GameEffects.Play(Effect.Leaves, transform.position + Vector3.up * 0.4f, 0.8f);
        GameEffects.Play(Effect.Sparkle, transform.position + Vector3.up * 0.5f, 0.6f);
        GameAudio.Play(Sfx.Harvest, transform.position);
        ClearSquare();
        plot.RecordHarvest();
        return true;
    }

    // The boss climbs out where the plant stood, facing a random way, and the square is empty soil again.
    private CropBoss GrowBoss()
    {
        Quaternion facing = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        CropBoss boss = Instantiate(Crop.GrownBoss, transform.position, facing, plot.transform.parent);
        GameEffects.Play(Effect.Dirt, transform.position + Vector3.up * 0.2f, 3f);
        GameEffects.Play(Effect.Dust, transform.position + Vector3.up * 0.3f, 1.5f);
        GameAudio.Play(Sfx.BossEmerge, transform.position + Vector3.up);
        boss.Setup(plot.Inventory, Crop.SeedResource, plot.GameManager);
        boss.GetComponent<BossCombat>().SetPlayer(plot.PlayerHealth);
        ClearSquare();
        return boss;
    }

    // Back to tilled soil with nothing planted.
    private void ClearSquare()
    {
        RemovePlant();
        Crop = null;
        growTime = 0f;
        waterings = 0;
        SetWet(false);
        State = TileState.Tilled;
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
        PopIn.On(plant); // each new stage springs up rather than swapping in
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
