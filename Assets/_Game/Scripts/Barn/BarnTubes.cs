using UnityEngine;

// Unlocks one additional boss tube for each completed barn repair.
public class BarnTubes : MonoBehaviour
{
    [SerializeField] private BarnRepair barnRepair;
    [SerializeField] private Inventory inventory;
    [SerializeField] private BossTube[] tubes;
    [SerializeField, Min(1)] private int startingUnlockedTubeCount = 1;
    [SerializeField, Min(1)] private int tubesPerRepair = 1;

    private int lastRepairStage = -1;

    public int unlockedTubeCount { get; private set; }

    public BossTube[] Tubes => tubes;

    public void RestoreUnlockedTubeCount(int count, int repairStage)
    {
        // Repairs own unlocks. Older saves may contain a stale tube count.
        ApplyUnlocks(repairStage);
    }

    private void Start()
    {
        if (barnRepair == null || inventory == null || tubes == null || tubes.Length == 0)
        {
            Debug.LogError("BarnTubes needs the BarnRepair, shared Inventory, and tube references assigned.", this);
            enabled = false;
            return;
        }

        RefreshUnlocks();
    }

    private void Update()
    {
        if (enabled && barnRepair != null && barnRepair.JobsDone != lastRepairStage)
        {
            RefreshUnlocks();
        }
    }

    private void RefreshUnlocks()
    {
        ApplyUnlocks(barnRepair.JobsDone);
    }

    private void ApplyUnlocks(int repairStage)
    {
        lastRepairStage = Mathf.Max(0, repairStage);
        unlockedTubeCount = Mathf.Clamp(startingUnlockedTubeCount + lastRepairStage * tubesPerRepair, 1, tubes.Length);

        for (int i = 0; i < tubes.Length; i++)
        {
            if (tubes[i] != null)
            {
                tubes[i].Configure(inventory, i, i < unlockedTubeCount);
            }
        }
    }
}
