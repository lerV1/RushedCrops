using UnityEngine;

// Holds one captured boss and stores its seeds for the player to collect.
public class BossTube : MonoBehaviour, IInteractable
{
    [Tooltip("The seed type this tube produces.")]
    [SerializeField] private ResourceType seedOutput = ResourceType.CarrotKingSeed;
    [Tooltip("Seconds between seeds while a boss is in the tube.")]
    [SerializeField, Min(0.1f)] private float secondsPerSeed = 30f;
    [Tooltip("The most seeds this tube can hold before collection.")]
    [SerializeField, Min(1)] private int seedCapacity = 10;
    [Tooltip("A simple tinted placeholder shown while the boss is inside.")]
    [SerializeField] private GameObject bossVisual;
    [SerializeField] private Renderer tubeRenderer;
    [SerializeField] private Color tubeTint = new Color(0.38f, 0.72f, 0.82f);
    [SerializeField] private Color bossTint = new Color(0.88f, 0.25f, 0.18f);

    private Inventory inventory;
    private int tubeIndex;
    private bool isUnlocked;
    private bool placed;
    private int storedSeeds;
    private float nextSeedTimer;

    public bool isPlaced => placed;
    public float secondsToNextSeed => placed && storedSeeds < seedCapacity ? Mathf.Max(0f, nextSeedTimer) : 0f;
    public int StoredSeeds => storedSeeds;

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
        if (!placed || storedSeeds >= seedCapacity)
        {
            return;
        }

        nextSeedTimer -= Time.deltaTime;
        if (nextSeedTimer <= 0f)
        {
            storedSeeds++;
            nextSeedTimer = secondsPerSeed;
        }
    }

    public bool CanInteract => isUnlocked && inventory != null &&
        (!placed ? inventory.GetCount(ResourceType.CapturedBoss) > 0 :
            storedSeeds > 0 && inventory.CanAdd(seedOutput, storedSeeds));

    public string GetPrompt()
    {
        if (!isUnlocked)
        {
            return $"Tube {tubeIndex + 1} locked — repair the barn";
        }

        if (!placed)
        {
            return inventory != null && inventory.GetCount(ResourceType.CapturedBoss) > 0
                ? "Place captured boss"
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
            if (!inventory.TrySpend(ResourceType.CapturedBoss, 1))
            {
                return;
            }

            placed = true;
            nextSeedTimer = secondsPerSeed;
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

    public void SetUnlocked(bool unlocked)
    {
        isUnlocked = unlocked;
    }

    private void UpdateVisual()
    {
        if (bossVisual != null)
        {
            bossVisual.SetActive(placed);
            Renderer visualRenderer = bossVisual.GetComponentInChildren<Renderer>();
            if (visualRenderer != null)
            {
                visualRenderer.material.color = bossTint;
            }
        }
    }
}
