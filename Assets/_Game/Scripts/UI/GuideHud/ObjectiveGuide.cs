using UnityEngine;
using UnityEngine.UI;

// Contextual field notes: explain the mechanic at hand without prescribing a playthrough.
public class ObjectiveGuide : MonoBehaviour
{
    private const string CompletedKey = "RushedCrops.ObjectiveGuide.Completed";

    /// <summary>Clears the legacy walkthrough flag when starting a new farm.</summary>
    public static void ClearSavedCompletion() => PlayerPrefs.DeleteKey(CompletedKey);

    [Header("Progress")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Transform player;
    [SerializeField] private Transform gatheringRoot;
    [SerializeField] private FarmField farmField;
    [SerializeField] private MiniCropSpawner combatTarget;
    [SerializeField] private BarnRepair barnRepair;
    [SerializeField] private BossTube bossTube;

    [Header("Display")]
    [SerializeField] private GuideArrow arrow;
    [SerializeField] private Text objectiveText;
    [SerializeField, Min(0.05f)] private float refreshSeconds = 0.2f;
    [SerializeField, Min(1f)] private float discoveryRadius = 8f;

    private PlayerInteractor interactor;
    private PlayerTools tools;
    private Health health;
    private ToolPickup[] pickups;
    private WaterSource[] waterSources;
    private Merchant merchant;
    private float nextRefresh;

    private void Awake()
    {
        if (inventory == null || player == null || arrow == null || objectiveText == null)
        {
            Debug.LogError("ObjectiveGuide needs its Inventory, player, GuideArrow and text assigned.", this);
            enabled = false;
            return;
        }

        interactor = player.GetComponent<PlayerInteractor>();
        tools = player.GetComponent<PlayerTools>();
        health = player.GetComponent<Health>();
        pickups = FindObjectsByType<ToolPickup>();
        waterSources = FindObjectsByType<WaterSource>();
        merchant = FindAnyObjectByType<Merchant>();
        // Completion of the old quest chain must not suppress contextual help on saved farms.
    }

    private void OnDisable()
    {
        if (arrow != null) arrow.Clear();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + refreshSeconds;
        RefreshObjective();
    }

    private void RefreshObjective()
    {
        if (health != null && health.IsDead) { Show("", ""); return; }
        if (interactor != null)
        {
            Component focus = interactor.Target as Component;
            if (focus == null) focus = interactor.ToolTarget as Component;
            if (Explain(focus)) return;
        }

        // Nearby discoveries do not demand a trip across the map before farming is allowed.
        ToolPickup nearby = NearestPickup(ToolType.None, discoveryRadius);
        if (nearby != null) { ExplainPickup(nearby); return; }
        if (health != null && health.CurrentHealth < health.MaxHealth)
        {
            Show("FOOD & HEALTH", "Harvested crops also heal you. Eating uses one crop; the food hint shows which one.");
            return;
        }
        if (inventory.TryGetSelectedItem(out ResourceType item))
        {
            ToolType selected = ResourceTypes.ToTool(item);
            if (selected != ToolType.None) { ExplainTool(selected); return; }
            if (IsBossSeed(item))
            {
                Show("BOSS SEEDS", "These grow into a live boss, not food. Plant and water them like crops; prepare before they ripen.");
                return;
            }
        }

        // Make the easily missed gathering tools discoverable even from the starting area.
        ToolPickup gatheringTool = NearestGatheringPickup();
        if (gatheringTool != null) { ExplainPickup(gatheringTool); return; }
        Show("FARM NOTES", "Explore at your pace. Look at tools, soil, trees, rocks or the merchant to learn how they work.");
    }

    private bool Explain(Component focus)
    {
        if (focus == null) return false;
        if (focus is ToolPickup pickup) { ExplainPickup(pickup); return true; }
        if (focus is ItemDrop drop)
        {
            Show("GROUND ITEMS", inventory.CanAdd(drop.Item, drop.Amount)
                ? "Loose items enter your hotbar when you walk over them or interact. Tools on stands need interaction."
                : SpaceHint(), drop.transform);
        }
        else if (focus is ResourceNode node)
        {
            if (!Owns(node.Tool)) { ExplainTool(node.Tool); return true; }
            Show(node.Item == ResourceType.Wood ? "WOOD & HATCHET" : "STONE & PICKAXE",
                "Select your " + ToolName(node.Tool) + ". Repeated hits break this into loose resources; collect the drops. It regrows later.", node.transform);
        }
        else if (focus is FarmField field) ExplainSoil(field.TileAt(field.AimedSquare), field.transform);
        else if (focus is FarmTile tile) ExplainSoil(tile, tile.transform);
        else if (focus is WaterSource) Show("REFILLING", "The watering can starts empty. Hold it and use it on the river to refill; watering crops spends water.");
        else if (focus is Merchant)
            Show("TRADING", "Sell crops for coins; buy seeds and supplies here. Prices change each day. Selling also frees hotbar space.");
        else if (focus is BarnRepair)
            Show("BARN REPAIRS", "Repairs spend coins and materials and unlock seed tubes. The repair prompt shows the next cost and any boss requirement.");
        else if (focus is BossTube tube)
            Show("SEED TUBES", !tube.IsUnlocked ? "Barn repairs unlock tubes. A captured boss placed inside produces seeds over time."
                : tube.isPlaced ? "This boss produces seeds over time. Collect stored seeds to keep production running."
                : "Place a captured boss here to produce seeds. Weaken a live boss, then use a capture jar on it.");
        else if (focus is CropBoss)
            Show("BOSS CAPTURE", "A jar catches a living boss at one-third health or less. Stop attacking before it dies; leave a hotbar slot for the capture.");
        else if (focus is MiniCrop)
            Show("EVIL CROPS", "Tools can fight as well as work. MiniCrops can drop rare boss seeds. Harvested food restores health after a fight.");
        else return false;
        return true;
    }

    private void ExplainSoil(FarmTile tile, Transform target)
    {
        if (tile == null || tile.State == FarmTile.TileState.Untilled)
        {
            if (!Owns(ToolType.Hoe)) { ExplainTool(ToolType.Hoe); return; }
            Show("SOIL & SEEDS", "A hoe prepares soil. Select seeds and interact with tilled soil to plant. Each planting consumes one seed.", target);
        }
        else if (tile.State == FarmTile.TileState.Tilled)
        {
            Show("PLANTING", "Select seeds in your hotbar, then interact with this soil. Buy more at the merchant; boss seeds grow enemies.", target);
        }
        else if (tile.State == FarmTile.TileState.Ripe)
        {
            Show("HARVESTS", tile.CanInteract ? "Interact to collect the ripe crop. Crops can be sold for coins or eaten to restore health."
                : SpaceHint(), target);
        }
        else if (tile.NeedsWater)
        {
            if (!Owns(ToolType.WateringCan)) { ExplainTool(ToolType.WateringCan); return; }
            if (tools != null && tools.WaterLeft == 0) { ExplainTool(ToolType.WateringCan); return; }
            Show("CROP GROWTH", "Dry crops pause growth. Use the watering can on this crop. Some crops dry again and need another watering.", target);
        }
        else
        {
            Show("CROP GROWTH", tile.Crop != null && tile.Crop.IsBossSeed
                ? "This watered boss seed is growing into an enemy. Be ready when it reaches full growth."
                : "Wet crops grow on their own. You can explore while waiting; return if they dry out or become ripe.", target);
        }
    }

    private void ExplainPickup(ToolPickup pickup)
    {
        if (Owns(pickup.Tool)) { ExplainTool(pickup.Tool); return; }
        ResourceType item = ResourceTypes.FromTool(pickup.Tool);
        Show(ToolName(pickup.Tool).ToUpper() + " PICKUP",
            !inventory.CanAdd(item, 1) ? SpaceHint()
            : "Pick up this tool by interacting. " + ToolPurpose(pickup.Tool) + " Select its hotbar slot to equip it.", pickup.transform);
    }

    private void ExplainTool(ToolType tool)
    {
        if (!Owns(tool))
        {
            ToolPickup pickup = NearestPickup(tool);
            if (pickup != null) { ExplainPickup(pickup); return; }
            foreach (ItemDrop drop in FindObjectsByType<ItemDrop>())
            {
                if (drop.Item == ResourceTypes.FromTool(tool) && drop.Amount > 0)
                {
                    Show("RECOVER YOUR TOOL", inventory.CanAdd(drop.Item, drop.Amount)
                        ? "Your " + ToolName(tool) + " is on the ground. Walk over it or interact to pick it up."
                        : SpaceHint(), drop.transform);
                    return;
                }
            }
            Show("TOOLS", "A " + ToolName(tool) + " is needed here. The merchant sells replacements.", merchant != null ? merchant.transform : null);
            return;
        }
        if (tool == ToolType.WateringCan && tools != null && tools.WaterLeft == 0)
        {
            WaterSource source = NearestWater();
            Show("EMPTY WATERING CAN", "Fill the can at the river before watering crops. Hold the can and use it on the water.", source != null ? source.transform : null);
            return;
        }
        Show(ToolName(tool).ToUpper(), ToolPurpose(tool) + " Select its hotbar slot to equip it; use it on the matching target.");
    }

    private bool Owns(ToolType tool) => inventory.GetCount(ResourceTypes.FromTool(tool)) > 0;

    private ToolPickup NearestPickup(ToolType tool, float radius = float.PositiveInfinity)
    {
        ToolPickup nearest = null;
        float distance = radius * radius;
        foreach (ToolPickup pickup in pickups)
        {
            if (pickup == null || !pickup.isActiveAndEnabled || Owns(pickup.Tool)
                || (tool != ToolType.None && pickup.Tool != tool)) continue;
            float candidate = (pickup.transform.position - player.position).sqrMagnitude;
            if (candidate < distance) { nearest = pickup; distance = candidate; }
        }
        return nearest;
    }

    private ToolPickup NearestGatheringPickup()
    {
        ToolPickup hatchet = NearestPickup(ToolType.Hatchet);
        ToolPickup pickaxe = NearestPickup(ToolType.Pickaxe);
        if (hatchet == null) return pickaxe;
        if (pickaxe == null) return hatchet;
        return (hatchet.transform.position - player.position).sqrMagnitude
            < (pickaxe.transform.position - player.position).sqrMagnitude ? hatchet : pickaxe;
    }

    private WaterSource NearestWater()
    {
        WaterSource nearest = null;
        float distance = float.PositiveInfinity;
        foreach (WaterSource source in waterSources)
        {
            if (source == null || !source.isActiveAndEnabled) continue;
            Collider area = source.GetComponent<Collider>();
            Vector3 point = area != null ? area.ClosestPoint(player.position) : source.transform.position;
            float candidate = (point - player.position).sqrMagnitude;
            if (candidate < distance) { nearest = source; distance = candidate; }
        }
        return nearest;
    }

    private void Show(string title, string description, Transform target = null)
    {
        objectiveText.text = string.IsNullOrEmpty(title) ? "" : title + "\n" + description;
        if (target != null) arrow.SetTarget(target);
        else arrow.Clear();
    }

    private static string SpaceHint() => "Only five hotbar slots. Sell items or drop a selected stack (Shift + Backspace) to make room. Dropped tools can be recovered.";
    private static bool IsBossSeed(ResourceType item) => item >= ResourceType.CarrotKingSeed && item <= ResourceType.WheatWarlordSeed;
    private static string ToolName(ToolType tool) => ResourceTypes.DisplayName(ResourceTypes.FromTool(tool));
    private static string ToolPurpose(ToolType tool)
    {
        switch (tool)
        {
            case ToolType.Hatchet: return "Hatchets chop trees into wood for repairs.";
            case ToolType.Pickaxe: return "Pickaxes break rocks into stone for repairs.";
            case ToolType.Hoe: return "Hoes prepare soil for planting seeds.";
            case ToolType.WateringCan: return "Watering cans keep crops growing; refill at the river.";
            case ToolType.Sword: return "Swords fight enemies; work tools can also deal damage.";
            case ToolType.CaptureJar: return "Jars capture living bosses at one-third health or less.";
            default: return "Tools do different jobs around the farm.";
        }
    }
}
