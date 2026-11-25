using UnityEngine;

// Holds one captured boss and stores its seeds for the player to collect.
public class BossTube : MonoBehaviour
{
    [Tooltip("Seed type used only for occupied tubes from older saves. New captures supply their own species.")]
    [SerializeField] private ResourceType seedOutput = ResourceType.CarrotKingSeed;
    [Tooltip("Seconds between seeds while a boss is in the tube.")]
    [SerializeField, Min(0.1f)] private float secondsPerSeed = 10f;
    [Tooltip("The most seeds this tube can hold before collection.")]
    [SerializeField, Min(1)] private int seedCapacity = 99;
    [Tooltip("The resident model's mount. The crop catalog supplies the captured species' model.")]
    [SerializeField] private GameObject bossVisual;
    [SerializeField] private FarmPlot farmPlot;
    [SerializeField] private Renderer tubeRenderer;
    [SerializeField] private Color tubeTint = new Color(0.38f, 0.72f, 0.82f);
    [SerializeField] private Color bossTint = new Color(0.88f, 0.25f, 0.18f);

    private Inventory inventory;
    private int tubeIndex;
    private bool isUnlocked;
    private bool placed;
    private int storedSeeds;
    private float nextSeedTimer;
    private GameObject residentModel;
    private ResourceType shownSeed = (ResourceType)(-1);

    public bool isPlaced => placed;
    public float secondsToNextSeed => placed && storedSeeds < seedCapacity ? Mathf.Max(0f, nextSeedTimer) : 0f;
    public int StoredSeeds => storedSeeds;
    public bool IsUnlocked => isUnlocked;
    public ResourceType SeedOutput => seedOutput;

    public float SeedTimer => nextSeedTimer;

    /// <summary>Restores the resident boss species, accumulated seeds, and countdown.</summary>
    public void RestoreState(bool savedPlaced, int savedSeeds, float savedTimer, ResourceType savedSeed = (ResourceType)(-1))
    {
        if ((int)savedSeed >= 0)
        {
            if ((int)ResourceTypes.CapturedBossForSeed(savedSeed) >= 0)
                seedOutput = savedSeed;
            else
                savedPlaced = false;
        }
        placed = savedPlaced;
        storedSeeds = placed ? Mathf.Clamp(savedSeeds, 0, seedCapacity) : 0;
        nextSeedTimer = !placed ? 0f : storedSeeds >= seedCapacity
            ? Mathf.Max(0.1f, secondsPerSeed) : Mathf.Clamp(savedTimer, 0f, secondsPerSeed);
        UpdateVisual();
    }

    private void Start()
    {
        if (tubeRenderer != null)
        {
            tubeRenderer.material.color = tubeTint;
        }
        UpdateVisual();
    }

    private void Update()
    {
        if (!isUnlocked || !placed || storedSeeds >= seedCapacity)
        {
            return;
        }

        nextSeedTimer -= Time.deltaTime;
        if (nextSeedTimer <= 0f)
        {
            // Add to the existing stack and preserve time beyond the last tick.
            float interval = Mathf.Max(0.1f, secondsPerSeed);
            int generated = Mathf.Min(seedCapacity - storedSeeds, 1 + Mathf.FloorToInt(-nextSeedTimer / interval));
            storedSeeds += generated;
            nextSeedTimer = storedSeeds >= seedCapacity ? interval : nextSeedTimer + generated * interval;
        }
    }

    public bool CanInteract => isUnlocked && inventory != null &&
        (!placed ? TryFindCapturedBoss(out _) :
            storedSeeds > 0 && inventory.CanAdd(seedOutput, storedSeeds));

    public string GetPrompt()
    {
        if (!isUnlocked)
        {
            return $"Tube {tubeIndex + 1} locked — repair the barn";
        }

        if (!placed)
        {
            return TryFindCapturedBoss(out ResourceType captured)
                ? $"Place {ResourceTypes.DisplayName(captured).ToLower()}"
                : "Tube empty — needs a captured boss";
        }

        if (storedSeeds > 0)
        {
            return inventory != null && inventory.CanAdd(seedOutput, storedSeeds)
                ? $"Collect {storedSeeds} {ResourceTypes.DisplayName(seedOutput)}"
                : "Seeds ready — clear inventory space";
        }

        return $"Growing {ResourceTypes.DisplayName(seedOutput)} ({Mathf.CeilToInt(secondsToNextSeed)}s)";
    }

    public void Interact(PlayerInteractor player)
    {
        if (!CanInteract)
        {
            return;
        }

        if (!placed)
        {
            if (!TryFindCapturedBoss(out ResourceType captured) || !inventory.TrySpend(captured, 1))
            {
                return;
            }

            seedOutput = ResourceTypes.SeedForCapturedBoss(captured);
            placed = true;
            nextSeedTimer = Mathf.Max(0.1f, secondsPerSeed);
            storedSeeds = 0;
            UpdateVisual();
            GameEffects.Play(Effect.Sparkle, transform.position + Vector3.up, 1f);
            return;
        }

        if (inventory.Add(seedOutput, storedSeeds))
        {
            storedSeeds = 0;
            GameEffects.Play(Effect.Sparkle, transform.position + Vector3.up, 1f);
        }
    }

    public void Configure(Inventory sharedInventory, int index, bool unlocked)
    {
        inventory = sharedInventory;
        tubeIndex = index;
        SetUnlocked(unlocked);
    }

    private bool TryFindCapturedBoss(out ResourceType captured)
    {
        captured = default;
        if (inventory == null) return false;
        // A selected capture wins when the player carries several species.
        if (inventory.TryGetSelectedItem(out captured) && ResourceTypes.IsCapturedBoss(captured)) return true;
        for (int slot = 0; slot < Inventory.SlotCount; slot++)
        {
            if (!inventory.IsSlotEmpty(slot) && ResourceTypes.IsCapturedBoss(inventory.GetSlotItem(slot)))
            {
                captured = inventory.GetSlotItem(slot);
                return true;
            }
        }
        return false;
    }

    public void SetUnlocked(bool unlocked)
    {
        isUnlocked = unlocked;
        // Hide the whole tube, including its frame, contents, and colliders.
        gameObject.SetActive(unlocked);
    }

    private void UpdateVisual()
    {
        if (bossVisual != null)
        {
            bossVisual.SetActive(placed);
            if (placed && Application.isPlaying && farmPlot != null && shownSeed != seedOutput)
            {
                ShowResidentModel();
            }
            if (residentModel != null) return;
            Renderer visualRenderer = bossVisual.GetComponentInChildren<Renderer>();
            if (visualRenderer != null)
            {
                visualRenderer.material.color = bossTint;
            }
        }
    }

    private void ShowResidentModel()
    {
        CropData crop = farmPlot.FindCrop(seedOutput);
        Transform source = crop != null && crop.GrownBoss != null ? crop.GrownBoss.VisualModel : null;
        if (source == null) return;
        foreach (Transform child in bossVisual.transform) child.gameObject.SetActive(false);
        if (residentModel != null) Destroy(residentModel);
        residentModel = Instantiate(source.gameObject, bossVisual.transform);
        residentModel.name = ResourceTypes.DisplayName(ResourceTypes.CapturedBossForSeed(seedOutput));
        residentModel.transform.localPosition = Vector3.zero;
        residentModel.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        foreach (Collider collider in residentModel.GetComponentsInChildren<Collider>()) collider.enabled = false;
        Renderer[] renderers = residentModel.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (size > 0.0001f) residentModel.transform.localScale *= 0.75f / size;
            bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            residentModel.transform.position += bossVisual.transform.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }
        shownSeed = seedOutput;
    }
}
