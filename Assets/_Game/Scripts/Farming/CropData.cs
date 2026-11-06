using UnityEngine;

// One crop's settings: its seed, grow time, stage models and harvest. One asset per crop (DA_Carrot, ...).
[CreateAssetMenu(fileName = "DA_NewCrop", menuName = "Rushed Crops/Crop Data")]
public class CropData : ScriptableObject
{
    [SerializeField] private CropType cropType;
    [SerializeField] private string displayName = "Crop";

    [Header("Inventory")]
    [Tooltip("Planting uses one of these from the Inventory.")]
    [SerializeField] private ResourceType seedResource;
    [Tooltip("Harvesting adds these to the Inventory.")]
    [SerializeField] private ResourceType harvestResource;
    [SerializeField, Min(1)] private int harvestAmount = 3;

    [Header("Growing")]
    [Tooltip("Seconds from planting to ripe at normal speed.")]
    [SerializeField, Min(1f)] private float growSeconds = 60f;
    [Tooltip("One model per growth stage, smallest first. The last one is the ripe plant.")]
    [SerializeField] private GameObject[] stageModels = new GameObject[3];
    [Tooltip("How many times the crop must be watered. Each watering lets it grow an equal share of the way.")]
    [SerializeField, Min(1)] private int waterNeeded = 1;

    public CropType CropType => cropType;
    public string DisplayName => displayName;
    public ResourceType SeedResource => seedResource;
    public ResourceType HarvestResource => harvestResource;
    public int HarvestAmount => harvestAmount;
    public float GrowSeconds => growSeconds;
    public int StageCount => stageModels.Length;
    public int WaterNeeded => waterNeeded;

    public GameObject GetStageModel(int stage)
    {
        return stageModels[Mathf.Clamp(stage, 0, stageModels.Length - 1)];
    }

    private void OnValidate()
    {
        if (stageModels == null || stageModels.Length == 0)
        {
            Debug.LogWarning($"{name} needs at least one stage model.", this);
        }
    }
}
