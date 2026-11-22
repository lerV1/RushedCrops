using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A giant angry crop that climbs out of the crop field when a boss seed finishes growing (see FarmTile).
// The hatchet hurts it and BossCombat makes it fight back. When it dies it sinks, throws out seeds (ItemDrop)
// and GameManager counts the boss as beaten. If the player dies first, it heals back to full.
[RequireComponent(typeof(Health))]
public class CropBoss : MonoBehaviour, IInteractable
{
    [Serializable]
    public struct SeedDropEntry
    {
        public CropData crop;
        [Min(1)] public int amount;
    }

    [SerializeField] private string displayName = "Carrot King";

    [Tooltip("The model, scaled for the hit wobble and sunk into the ground on death.")]
    [SerializeField] private Transform model;

    [Header("Loot")]
    [Tooltip("The scene's shared Inventory. Picked-up seeds go into it.")]
    [SerializeField] private Inventory inventory;
    [Tooltip("The drop that is thrown out for each entry below.")]
    [SerializeField] private ItemDrop dropPrefab;
    [SerializeField] private SeedDropEntry[] drops = Array.Empty<SeedDropEntry>();
    [Tooltip("How far from the boss, in metres, the bags land.")]
    [SerializeField, Min(0.5f)] private float dropRadius = 2.2f;

    [Header("Feel")]
    [Tooltip("How long, in seconds, the boss takes to sink into the ground when it dies.")]
    [SerializeField, Min(0.05f)] private float sinkTime = 0.8f;

    [Header("Progress")]
    [Tooltip("On for the bosses that must be beaten to win (Carrot King, Tomato Brute, Onion Overlord). Each counts once, by name.")]
    [SerializeField] private bool countsTowardVictory = true;
    [Tooltip("On for bosses grown from boss seeds: once it has sunk, the whole boss is removed from the scene. Map bosses stay.")]
    [SerializeField] private bool removeAfterDeath;

    // Every living boss in the scene, for the boss health bar.
    public static readonly List<CropBoss> All = new List<CropBoss>();

    private Health health;
    private Vector3 modelScale;
    private Vector3 modelPosition;
    private Quaternion modelRotation;
    private float wobbleLeft;
    private const float WobbleTime = 0.25f;

    public SeedDropEntry[] Drops => drops;
    public string DisplayName => displayName;
    public Health Health => health;
    public bool IsDead => health.IsDead;

    // How BossCombat poses the model while it winds up and attacks, on top of the hit wobble.
    public Vector3 PoseScale { get; set; } = Vector3.one;
    public Vector3 PoseOffset { get; set; }
    public Quaternion PoseTilt { get; set; } = Quaternion.identity;

    private void Awake()
    {
        health = GetComponent<Health>();
        if (model == null)
        {
            model = transform;
        }
        modelScale = model.localScale;
        modelPosition = model.localPosition;
        modelRotation = model.localRotation;
    }

    private void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
        All.Add(this);
    }

    private void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
        All.Remove(this);
    }

    public bool CountsTowardVictory => countsTowardVictory;

    // A boss grown from a boss seed is made from a prefab, which can't point at the scene's Inventory,
    // so the farm square that grew it hands the Inventory over here.
    public void Setup(Inventory sharedInventory)
    {
        inventory = sharedInventory;
    }

    public bool CanInteract => false;

    public string GetPrompt() => health.IsDead ? null : $"{displayName} ({health.CurrentHealth}/{health.MaxHealth} HP)";

    public void Interact(PlayerInteractor player) { }

    private void OnDamaged(int amount) => wobbleLeft = WobbleTime;

    private void Update()
    {
        if (health.IsDead)
        {
            return;
        }
        wobbleLeft = Mathf.Max(wobbleLeft - Time.deltaTime, 0f);
        // Squash down and bulge out on a hit, then spring back.
        float squash = Mathf.Sin(wobbleLeft / WobbleTime * Mathf.PI) * 0.12f;
        model.localScale = Vector3.Scale(Vector3.Scale(modelScale, PoseScale), new Vector3(1f + squash, 1f - squash, 1f + squash));
        model.localPosition = modelPosition + PoseOffset;
        model.localRotation = modelRotation * PoseTilt;
    }

    // Back to full health and its resting pose, for when the player dies mid-fight.
    public void ResetFight()
    {
        if (health.IsDead)
        {
            return;
        }
        health.RestoreFullHealth();
        PoseScale = Vector3.one;
        PoseOffset = Vector3.zero;
        PoseTilt = Quaternion.identity;
    }

    private void OnDied()
    {
        // Nothing can hit it or look at it while it sinks.
        foreach (Collider part in GetComponentsInChildren<Collider>())
        {
            part.enabled = false;
        }
        model.localScale = modelScale;
        model.localRotation = modelRotation;
        DropSeeds();
        // Every boss unlocks the barn's last repair; only the required ones count toward winning.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RecordBossDefeat(displayName, countsTowardVictory);
        }
        StartCoroutine(Sink());
    }

    public void DropSeeds()
    {
        if (dropPrefab == null || inventory == null)
        {
            Debug.LogError($"{name} needs a drop prefab and the Inventory to drop seeds.", this);
            return;
        }

        float start = UnityEngine.Random.Range(0f, 360f);
        for (int i = 0; i < drops.Length; i++)
        {
            if (drops[i].crop == null)
            {
                continue;
            }
            float angle = (start + 360f * i / drops.Length) * Mathf.Deg2Rad;
            Vector3 landing = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dropRadius;
            ItemDrop bag = Instantiate(dropPrefab, transform.position + Vector3.up * 2f, Quaternion.identity, transform.parent);
            bag.Launch(inventory, drops[i].crop.SeedResource, drops[i].amount, landing);
        }
    }

    private IEnumerator Sink()
    {
        Vector3 from = model.localPosition;
        Vector3 to = modelPosition + Vector3.down * 4.5f;
        for (float t = 0f; t < sinkTime; t += Time.deltaTime)
        {
            float k = t / sinkTime;
            model.localPosition = Vector3.Lerp(from, to, k * k);
            yield return null;
        }
        model.gameObject.SetActive(false);
        model.localPosition = modelPosition;

        // A grown boss can be planted again and again, so don't leave dead ones behind. Destroying it also
        // takes it out of All (OnDisable) and removes its warning patches (BossCombat.OnDestroy). The seeds it
        // dropped are not its children, so they stay.
        if (removeAfterDeath)
        {
            Destroy(gameObject);
        }
    }
}
