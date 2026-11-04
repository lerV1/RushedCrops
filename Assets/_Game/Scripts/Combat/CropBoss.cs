using System;
using System.Collections;
using UnityEngine;

// A giant angry carrot planted beside the crop field. It never moves: it stands on its
// dirt mound and takes hits from the hatchet. Its Health counts the damage and HitFlash
// flashes it; this script wobbles it on each hit and, when it dies, sinks it into the
// ground and throws out seeds (an ItemDrop) for each entry in Drops.
// Looking at it shows its name and health. Killing it tells GameManager the boss is beaten.
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

    private Health health;
    private Vector3 modelScale;
    private Vector3 modelPosition;
    private float wobbleLeft;
    private const float WobbleTime = 0.25f;

    public SeedDropEntry[] Drops => drops;

    private void Awake()
    {
        health = GetComponent<Health>();
        if (model == null)
        {
            model = transform;
        }
        modelScale = model.localScale;
        modelPosition = model.localPosition;
    }

    private void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
    }

    // ---------- Look prompt: information only ----------

    public bool CanInteract => false;

    public string GetPrompt() => health.IsDead ? null : $"{displayName} ({health.CurrentHealth}/{health.MaxHealth} HP)";

    public void Interact(PlayerInteractor player) { }

    // ---------- Hits and death ----------

    private void OnDamaged(int amount) => wobbleLeft = WobbleTime;

    private void Update()
    {
        if (wobbleLeft <= 0f || health.IsDead)
        {
            return;
        }
        wobbleLeft = Mathf.Max(wobbleLeft - Time.deltaTime, 0f);
        // Squash down and bulge out, then spring back.
        float t = wobbleLeft / WobbleTime;
        float squash = Mathf.Sin(t * Mathf.PI) * 0.12f;
        model.localScale = Vector3.Scale(modelScale, new Vector3(1f + squash, 1f - squash, 1f + squash));
    }

    private void OnDied()
    {
        // Nothing can hit it or look at it while it sinks.
        foreach (Collider part in GetComponentsInChildren<Collider>())
        {
            part.enabled = false;
        }
        model.localScale = modelScale;
        DropSeeds();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RecordBossDefeat();
        }
        StartCoroutine(Sink());
    }

    // Throws one bag per entry, spread evenly round the boss.
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
    }
}
