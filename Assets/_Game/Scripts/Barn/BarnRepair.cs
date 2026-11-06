using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The inherited barn. Press Interact on it to pay for the next job: unlock it (coins), patch the roof
// (coins, wood), then straighten the east wall (coins, wood, stone, after the boss). GameManager counts jobs.
public class BarnRepair : MonoBehaviour, IInteractable
{
    // A barn piece that changes when its job is done. Positions and rotations are local to its parent.
    [Serializable]
    public class Part
    {
        public Transform part;
        public bool shownBroken = true;
        public bool shownFixed = true;
        public Vector3 brokenPosition;
        public Vector3 brokenRotation;
        public Vector3 fixedPosition;
        public Vector3 fixedRotation;
        public Vector3 scale = Vector3.one;
    }

    [Serializable]
    public class Job
    {
        [Tooltip("What the prompt calls the job, like \"Patch the roof\".")]
        public string name;

        [Tooltip("Shown for a few seconds after paying, like \"Roof patched\".")]
        public string doneMessage;

        [Tooltip("What the job costs. Every job takes coins.")]
        public ResourceAmount[] cost = Array.Empty<ResourceAmount>();

        [Tooltip("The job can only be done after the carrot boss is beaten.")]
        public bool needsBossBeaten;

        public Part[] parts = Array.Empty<Part>();
    }

    [Tooltip("The scene's shared Inventory. Jobs are paid from it.")]
    [SerializeField] private Inventory inventory;

    [Tooltip("The jobs in the order they are done. GameManager's Barn Repair Stages must match the count.")]
    [SerializeField] private Job[] jobs = Array.Empty<Job>();

    [Tooltip("How long, in seconds, the parts take to move into their fixed place.")]
    [SerializeField, Min(0.1f)] private float moveSeconds = 0.8f;

    [Tooltip("How long, in seconds, the \"Roof patched\" message stays after paying.")]
    [SerializeField, Min(0f)] private float messageSeconds = 3f;

    private string message;
    private float messageUntil = float.NegativeInfinity;
    private bool listening;

    public IReadOnlyList<Job> Jobs => jobs;
    public int JobsDone => GameManager.Instance != null ? GameManager.Instance.BarnRepairs : 0;
    public Job NextJob => JobsDone < jobs.Length ? jobs[JobsDone] : null;
    private bool BossBeaten => GameManager.Instance != null && GameManager.Instance.IsBossDefeated;

    private void Awake()
    {
        if (inventory == null || jobs.Length == 0)
        {
            Debug.LogError("BarnRepair needs the Inventory and its jobs. Run Rushed Crops > Farming > Set up barn repair.", this);
            enabled = false;
        }
    }

    // GameManager's Awake runs first (DefaultExecutionOrder), so it exists by now.
    private void Start()
    {
        if (!enabled)
        {
            return;
        }
        if (GameManager.Instance == null)
        {
            Debug.LogError("BarnRepair needs a GameManager in the scene to count the repairs.", this);
            enabled = false;
            return;
        }
        if (GameManager.Instance.BarnRepairStages != jobs.Length)
        {
            Debug.LogWarning($"The barn has {jobs.Length} jobs but GameManager expects {GameManager.Instance.BarnRepairStages} repairs.", this);
        }

        GameManager.Instance.BarnRepaired += OnBarnRepaired;
        listening = true;
        ShowRepairs(JobsDone);
    }

    private void OnDestroy()
    {
        if (listening && GameManager.Instance != null)
        {
            GameManager.Instance.BarnRepaired -= OnBarnRepaired;
        }
    }

    public bool CanInteract
    {
        get
        {
            Job job = NextJob;
            return enabled && job != null && (!job.needsBossBeaten || BossBeaten) && inventory.CanAfford(job.cost);
        }
    }

    public string GetPrompt()
    {
        if (!enabled)
        {
            return null;
        }
        if (Time.time < messageUntil)
        {
            return message;
        }

        Job job = NextJob;
        if (job == null)
        {
            return "The barn is fully repaired";
        }
        if (job.needsBossBeaten && !BossBeaten)
        {
            return $"{job.name}: beat the carrot boss first";
        }

        string price = $"{job.name} ({CostText(job.cost)})";
        string missing = MissingText(job.cost);
        return missing == null ? price : $"{price}. Need {missing}";
    }

    public void Interact(PlayerInteractor player)
    {
        Job job = NextJob;
        if (!CanInteract || !inventory.TrySpend(job.cost))
        {
            return;
        }

        message = $"{job.doneMessage}! Barn repairs {JobsDone + 1}/{jobs.Length}";
        messageUntil = Time.time + messageSeconds;
        GameManager.Instance.RecordBarnRepair();
    }

    private static string CostText(IReadOnlyList<ResourceAmount> cost)
    {
        var lines = new List<string>();
        foreach (ResourceAmount line in cost)
        {
            lines.Add($"{line.amount} {ResourceTypes.DisplayName(line.type).ToLower()}");
        }
        return string.Join(", ", lines);
    }

    private string MissingText(IReadOnlyList<ResourceAmount> cost)
    {
        var lines = new List<string>();
        foreach (ResourceAmount line in cost)
        {
            int stillNeeded = line.amount - inventory.GetCount(line.type);
            if (stillNeeded > 0)
            {
                lines.Add($"{stillNeeded} more {ResourceTypes.DisplayName(line.type).ToLower()}");
            }
        }
        return lines.Count == 0 ? null : string.Join(" and ", lines);
    }

    private void OnBarnRepaired(int repairsDone)
    {
        int index = repairsDone - 1;
        if (index >= 0 && index < jobs.Length)
        {
            StartCoroutine(MoveToFixed(jobs[index]));
        }
    }

    // Jobs before `done` look fixed, the rest broken. The setup menu uses this to save the barn broken.
    public void ShowRepairs(int done)
    {
        for (int i = 0; i < jobs.Length; i++)
        {
            foreach (Part part in jobs[i].parts)
            {
                if (part.part == null)
                {
                    continue;
                }
                float amount = i < done ? 1f : 0f;
                Pose(part, amount);
                part.part.gameObject.SetActive(i < done ? part.shownFixed : part.shownBroken);
            }
        }
    }

    private IEnumerator MoveToFixed(Job job)
    {
        foreach (Part part in job.parts)
        {
            if (part.part != null && (part.shownBroken || part.shownFixed))
            {
                part.part.gameObject.SetActive(true);
            }
        }

        for (float t = 0f; t < 1f; t += Time.deltaTime / moveSeconds)
        {
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            foreach (Part part in job.parts)
            {
                if (part.part != null)
                {
                    Pose(part, eased);
                }
            }
            yield return null;
        }

        foreach (Part part in job.parts)
        {
            if (part.part != null)
            {
                Pose(part, 1f);
                part.part.gameObject.SetActive(part.shownFixed);
            }
        }
    }

    // 0 is the broken look, 1 the fixed look. Parts that appear or disappear grow or shrink.
    private static void Pose(Part part, float amount)
    {
        part.part.localPosition = Vector3.Lerp(part.brokenPosition, part.fixedPosition, amount);
        part.part.localRotation = Quaternion.Slerp(Quaternion.Euler(part.brokenRotation), Quaternion.Euler(part.fixedRotation), amount);
        float size = Mathf.Lerp(part.shownBroken ? 1f : 0f, part.shownFixed ? 1f : 0f, amount);
        // Never quite zero: physics complains about colliders with no size.
        part.part.localScale = part.scale * Mathf.Max(size, 0.01f);
    }

    // Used by the setup menu.
    public void Configure(Inventory sharedInventory, Job[] barnJobs)
    {
        inventory = sharedInventory;
        jobs = barnJobs;
    }
}
