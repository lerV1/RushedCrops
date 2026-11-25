using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Contextual field notes: explain the mechanic at hand without prescribing a playthrough.
[DefaultExecutionOrder(1100)] // Read restored game state after SaveSystem.Start.
public class ObjectiveGuide : MonoBehaviour
{
    private const string CompletedKey = "RushedCrops.ObjectiveGuide.Completed";
    private const string LessonsKey = "RushedCrops.ObjectiveGuide.Lessons";
    private const string LessonKeyPrefix = "RushedCrops.ObjectiveGuide.Lesson.";

    private enum Lesson
    {
        None = 0, HoePickup = 1, CanPickup = 2, HatchetPickup = 4,
        PickaxePickup = 8, SwordPickup = 16, JarPickup = 32,
        Chop = 64, Mine = 128, Till = 256, Plant = 512,
        Refill = 1024, Grow = 2048, Harvest = 4096, Trade = 8192,
        Food = 16384, Fight = 32768, BossSeed = 65536, Capture = 131072,
        Repair = 262144, Tube = 524288, Ground = 1048576, Space = 2097152
    }

    /// <summary>Lets a new farm introduce each mechanic again.</summary>
    public static void ClearSavedCompletion()
    {
        PlayerPrefs.DeleteKey(CompletedKey);
        PlayerPrefs.DeleteKey(LessonsKey);
        foreach (Lesson lesson in System.Enum.GetValues(typeof(Lesson)))
        {
            if (lesson != Lesson.None) PlayerPrefs.DeleteKey(LessonKey(lesson));
        }
        PlayerPrefs.Save();
    }

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
    private readonly List<Lesson> completedLessons = new List<Lesson>();
    private Lesson visibleLesson;
    private FarmPlot plot;
    private FoodEater foodEater;
    private ResourceNode[] resourceNodes;
    private bool initialized;
    private bool listening;

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
        plot = farmField != null ? farmField.GetComponent<FarmPlot>() : null;
        foodEater = player.GetComponent<FoodEater>();
        resourceNodes = gatheringRoot != null ? gatheringRoot.GetComponentsInChildren<ResourceNode>() : new ResourceNode[0];
        // Completion of the old quest chain must not suppress contextual help on saved farms.
    }

    private void Start()
    {
        LoadCompletedLessons();
        initialized = true;
        Subscribe();
        RefreshObjective();
    }

    private void OnEnable()
    {
        if (initialized) Subscribe();
    }

    private void Subscribe()
    {
        if (listening) return;
        listening = true;
        inventory.Changed += OnInventoryChanged;
        ItemDrop.Collected += OnGroundItemCollected;
        if (plot != null) plot.Harvested += OnHarvested;
        if (merchant != null) merchant.Traded += OnTraded;
        if (foodEater != null) foodEater.AteFood += OnAteFood;
    }

    private void OnDisable()
    {
        if (listening)
        {
            inventory.Changed -= OnInventoryChanged;
            ItemDrop.Collected -= OnGroundItemCollected;
            if (plot != null) plot.Harvested -= OnHarvested;
            if (merchant != null) merchant.Traded -= OnTraded;
            if (foodEater != null) foodEater.AteFood -= OnAteFood;
            listening = false;
        }
        if (objectiveText != null) objectiveText.text = "";
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
        RecordProgress();
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
        Show("", "");
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
            if (IsComplete(PickupLesson(tool))) { Show("", ""); return; }
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
                || IsComplete(PickupLesson(pickup.Tool))
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
            Vector3 point = source.transform.position;
            if (area != null)
            {
                MeshCollider mesh = area as MeshCollider;
                point = mesh != null && !mesh.convex
                    ? area.bounds.ClosestPoint(player.position)
                    : area.ClosestPoint(player.position);
            }
            float candidate = (point - player.position).sqrMagnitude;
            if (candidate < distance) { nearest = source; distance = candidate; }
        }
        return nearest;
    }

    private void Show(string title, string description, Transform target = null)
    {
        Lesson lesson = LessonForTitle(title);
        if (description == SpaceHint()) lesson = Lesson.Space;
        visibleLesson = lesson;
        if (IsComplete(lesson))
        {
            objectiveText.text = "";
            arrow.Clear();
            return;
        }
        objectiveText.text = string.IsNullOrEmpty(title) ? "" : title + "\n" + description;
        if (target != null) arrow.SetTarget(target);
        else arrow.Clear();
    }

    private bool IsComplete(Lesson lesson) => lesson != Lesson.None && completedLessons.Contains(lesson);

    private void Complete(Lesson lesson)
    {
        if (lesson == Lesson.None || IsComplete(lesson)) return;
        completedLessons.Add(lesson);
        PlayerPrefs.SetInt(LessonKey(lesson), 1);
        PlayerPrefs.Save();
        if (IsComplete(visibleLesson))
        {
            objectiveText.text = "";
            arrow.Clear();
        }
    }

    private void RecordProgress()
    {
        foreach (ToolType tool in System.Enum.GetValues(typeof(ToolType)))
            if (tool != ToolType.None && Owns(tool)) Complete(PickupLesson(tool));
        foreach (ResourceNode node in resourceNodes)
            if (node != null && node.IsBroken) Complete(node.Item == ResourceType.Wood ? Lesson.Chop : Lesson.Mine);
        if (tools != null && tools.WaterLeft > 0) Complete(Lesson.Refill);
        if (farmField != null)
        {
            foreach (FarmTile tile in farmField.GetComponentsInChildren<FarmTile>())
            {
                if (tile.State != FarmTile.TileState.Untilled) Complete(Lesson.Till);
                if (tile.State == FarmTile.TileState.Growing || tile.State == FarmTile.TileState.Ripe) Complete(Lesson.Plant);
                if (tile.IsWet || tile.Progress > 0f || tile.State == FarmTile.TileState.Ripe) Complete(Lesson.Grow);
                if (tile.Crop != null && tile.Crop.IsBossSeed) Complete(Lesson.BossSeed);
            }
        }
        if (!IsComplete(Lesson.Fight))
            foreach (MiniCrop crop in FindObjectsByType<MiniCrop>())
                if (crop.CurrentState == MiniCrop.State.Dead) Complete(Lesson.Fight);
        if (gameManager != null && gameManager.IsBossDefeated) Complete(Lesson.Fight);
        for (int slot = 0; slot < Inventory.SlotCount; slot++)
            if (!inventory.IsSlotEmpty(slot) && ResourceTypes.IsCapturedBoss(inventory.GetSlotItem(slot))) Complete(Lesson.Capture);
        if (barnRepair != null && barnRepair.JobsDone > 0) Complete(Lesson.Repair);
        foreach (BossTube tube in FindObjectsByType<BossTube>())
        {
            if (tube.isPlaced)
            {
                Complete(Lesson.Capture);
                Complete(Lesson.Tube);
            }
        }
        if (visibleLesson == Lesson.Space)
            for (int slot = 0; slot < Inventory.SlotCount; slot++)
                if (inventory.IsSlotEmpty(slot)) { Complete(Lesson.Space); break; }
    }

    private void OnInventoryChanged() => RecordProgress();
    private void OnGroundItemCollected() => Complete(Lesson.Ground);
    private void OnHarvested()
    {
        Complete(Lesson.Till);
        Complete(Lesson.Plant);
        Complete(Lesson.Grow);
        Complete(Lesson.Harvest);
    }
    private void OnTraded(ResourceType item, int amount, bool bought) => Complete(Lesson.Trade);
    private void OnAteFood() => Complete(Lesson.Food);

    private void LoadCompletedLessons()
    {
        int oldLessons = PlayerPrefs.GetInt(LessonsKey, 0);
        foreach (Lesson lesson in System.Enum.GetValues(typeof(Lesson)))
        {
            if (lesson == Lesson.None) continue;

            string key = LessonKey(lesson);
            bool completed = PlayerPrefs.GetInt(key, 0) != 0;
            int oldValue = (int)lesson;
            if (!completed && oldLessons > 0 && oldValue > 0)
            {
                completed = oldLessons / oldValue % 2 == 1;
            }
            if (completed)
            {
                completedLessons.Add(lesson);
                PlayerPrefs.SetInt(key, 1);
            }
        }

        if (PlayerPrefs.HasKey(LessonsKey))
        {
            PlayerPrefs.DeleteKey(LessonsKey);
            PlayerPrefs.Save();
        }
    }

    private static string LessonKey(Lesson lesson) => LessonKeyPrefix + lesson;

    private static Lesson PickupLesson(ToolType tool)
    {
        switch (tool)
        {
            case ToolType.Hoe: return Lesson.HoePickup;
            case ToolType.WateringCan: return Lesson.CanPickup;
            case ToolType.Hatchet: return Lesson.HatchetPickup;
            case ToolType.Pickaxe: return Lesson.PickaxePickup;
            case ToolType.Sword: return Lesson.SwordPickup;
            case ToolType.CaptureJar: return Lesson.JarPickup;
            default: return Lesson.None;
        }
    }

    private static Lesson LessonForTitle(string title)
    {
        foreach (ToolType tool in System.Enum.GetValues(typeof(ToolType)))
        {
            if (tool == ToolType.None) continue;
            if (title == ToolName(tool).ToUpper() + " PICKUP") return PickupLesson(tool);
        }
        if (title == ToolName(ToolType.Hoe).ToUpper()) return Lesson.Till;
        if (title == ToolName(ToolType.Hatchet).ToUpper()) return Lesson.Chop;
        if (title == ToolName(ToolType.Pickaxe).ToUpper()) return Lesson.Mine;
        if (title == ToolName(ToolType.WateringCan).ToUpper()) return Lesson.Grow;
        if (title == ToolName(ToolType.Sword).ToUpper()) return Lesson.Fight;
        if (title == ToolName(ToolType.CaptureJar).ToUpper()) return Lesson.Capture;
        switch (title)
        {
            case "WOOD & HATCHET": return Lesson.Chop;
            case "STONE & PICKAXE": return Lesson.Mine;
            case "SOIL & SEEDS": return Lesson.Till;
            case "PLANTING": return Lesson.Plant;
            case "CROP GROWTH": return Lesson.Grow;
            case "HARVESTS": return Lesson.Harvest;
            case "REFILLING": case "EMPTY WATERING CAN": return Lesson.Refill;
            case "TRADING": return Lesson.Trade;
            case "BARN REPAIRS": return Lesson.Repair;
            case "SEED TUBES": return Lesson.Tube;
            case "BOSS CAPTURE": return Lesson.Capture;
            case "EVIL CROPS": return Lesson.Fight;
            case "BOSS SEEDS": return Lesson.BossSeed;
            case "FOOD & HEALTH": return Lesson.Food;
            case "GROUND ITEMS": return Lesson.Ground;
            default: return Lesson.None;
        }
    }

    private static string SpaceHint() => $"Only {Inventory.SlotCount} hotbar slots. Sell items or drop a selected stack (Shift + Backspace) to make room. Dropped tools can be recovered.";
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
