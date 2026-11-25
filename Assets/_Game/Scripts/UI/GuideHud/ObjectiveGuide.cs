using UnityEngine;
using UnityEngine.UI;

public class ObjectiveGuide : MonoBehaviour
{
    private const string CompletedKey = "RushedCrops.ObjectiveGuide.Completed";

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

    private int step;
    private FarmPlot plot;
    private float nextRefresh;

    private void Awake()
    {
        if (PlayerPrefs.GetInt(CompletedKey, 0) != 0)
        {
            gameObject.SetActive(false);
            return;
        }

        if (inventory == null || player == null || gatheringRoot == null || farmField == null
            || combatTarget == null || barnRepair == null || bossTube == null || arrow == null || objectiveText == null)
        {
            Debug.LogError("ObjectiveGuide needs its scene targets and GuideArrow assigned.", this);
            enabled = false;
            return;
        }

        plot = farmField.GetComponent<FarmPlot>();
        if (plot == null)
        {
            Debug.LogError("ObjectiveGuide needs a FarmPlot on its FarmField.", this);
            enabled = false;
            return;
        }
        RefreshObjective();
    }

    private void OnEnable()
    {
        if (plot != null) plot.Harvested += OnHarvested;
    }

    private void OnDisable()
    {
        if (plot != null) plot.Harvested -= OnHarvested;
        if (arrow != null) arrow.Clear();
    }

    private void OnHarvested()
    {
        if (step != 1) return;
        step = 2;
        RefreshObjective();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh)
        {
            return;
        }

        nextRefresh = Time.unscaledTime + refreshSeconds;
        RefreshObjective();
    }

    private void RefreshObjective()
    {
        if (step == 0 && HasWoodAndStone())
        {
            step = 1;
        }
        else if (step == 2 && (HasDefeatedMiniCrop()
            || (gameManager != null && gameManager.IsBossDefeated)))
        {
            step = 3;
        }
        else if (step == 3 && barnRepair.JobsDone >= barnRepair.Jobs.Count)
        {
            step = 4;
        }
        else if (step == 4 && bossTube.isPlaced)
        {
            FinishGuide();
            return;
        }

        string objective;
        Transform target;
        switch (step)
        {
            case 0:
                objective = "Gather one wood and one stone. Chop a tree or mine a rock.";
                target = NearestGatherTarget();
                break;
            case 1:
                objective = GetFarmObjective(out target);
                break;
            case 2:
                objective = "Defeat a MiniCrop or boss.";
                target = combatTarget.transform;
                break;
            case 3:
                objective = barnRepair.NextJob != null
                    ? "Repair the barn: " + barnRepair.NextJob.name + "."
                    : "Finish repairing the barn.";
                target = barnRepair.transform;
                break;
            default:
                objective = "Place your captured boss in the barn's tube.";
                target = bossTube.transform;
                break;
        }

        objectiveText.text = objective;
        arrow.SetTarget(target);
    }

    private bool HasWoodAndStone()
    {
        return inventory.GetCount(ResourceType.Wood) > 0
            && inventory.GetCount(ResourceType.Stone) > 0;
    }

    private Transform NearestGatherTarget()
    {
        bool needWood = inventory.GetCount(ResourceType.Wood) == 0;
        bool needStone = inventory.GetCount(ResourceType.Stone) == 0;
        ResourceNode[] nodes = gatheringRoot.GetComponentsInChildren<ResourceNode>();
        ResourceNode nearest = null;
        float nearestDistance = float.PositiveInfinity;

        foreach (ResourceNode node in nodes)
        {
            if (!node.enabled || node.IsBroken)
            {
                continue;
            }

            bool needed = (needWood && node.Item == ResourceType.Wood)
                || (needStone && node.Item == ResourceType.Stone);
            if (!needed)
            {
                continue;
            }

            float distance = (node.transform.position - player.position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearest = node;
                nearestDistance = distance;
            }
        }

        return nearest != null ? nearest.transform : gatheringRoot;
    }

    private string GetFarmObjective(out Transform target)
    {
        FarmTile[] tiles = farmField.GetComponentsInChildren<FarmTile>();
        FarmTile nearestTile = null;
        int priority = int.MaxValue;
        float nearestDistance = float.PositiveInfinity;
        string objective = "Till the soil with your hoe.";

        foreach (FarmTile tile in tiles)
        {
            int tilePriority;
            string tileObjective;
            if (tile.State == FarmTile.TileState.Ripe)
            {
                tilePriority = 0;
                tileObjective = tile.CanInteract ? "Harvest your ripe crop." : "Make room, then harvest your ripe crop.";
            }
            else if (tile.State == FarmTile.TileState.Growing && tile.NeedsWater)
            {
                tilePriority = 1;
                tileObjective = "Water your growing crop.";
            }
            else if (tile.State == FarmTile.TileState.Growing)
            {
                tilePriority = 2;
                tileObjective = "Let your crop grow, then water it again.";
            }
            else if (tile.State == FarmTile.TileState.Tilled)
            {
                tilePriority = 3;
                tileObjective = "Select a seed and plant it in the tilled plot.";
            }
            else
            {
                continue;
            }

            float distance = (tile.transform.position - player.position).sqrMagnitude;
            if (tilePriority < priority || (tilePriority == priority && distance < nearestDistance))
            {
                priority = tilePriority;
                nearestDistance = distance;
                nearestTile = tile;
                objective = tileObjective;
            }
        }

        target = nearestTile != null ? nearestTile.transform : farmField.transform;
        return objective;
    }

    private static bool HasDefeatedMiniCrop()
    {
        foreach (MiniCrop crop in FindObjectsByType<MiniCrop>(FindObjectsSortMode.None))
        {
            if (crop.CurrentState == MiniCrop.State.Dead)
            {
                return true;
            }
        }
        return false;
    }

    private void FinishGuide()
    {
        PlayerPrefs.SetInt(CompletedKey, 1);
        PlayerPrefs.Save();
        arrow.Clear();
        gameObject.SetActive(false);
    }

}
