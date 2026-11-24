using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A tree to chop with the hatchet, or a rock to mine with the pickaxe. Each hit shakes it; after a few hits it
// breaks, throws out its wood or stone to pick up, and leaves a stump or rubble that grows back after a while.
public class ResourceNode : MonoBehaviour, IToolTarget, IInteractable
{
    [Tooltip("The scene's shared Inventory. Picked-up wood and stone go into it.")]
    [SerializeField] private Inventory inventory;
    [Tooltip("What gets thrown out when it breaks (PFB_ItemDrop).")]
    [SerializeField] private ItemDrop dropPrefab;

    [Header("What it is")]
    [Tooltip("What the prompt calls it, like \"tree\" or \"rock\".")]
    [SerializeField] private string displayName = "tree";
    [Tooltip("The word for using the tool on it, like \"Chop\" or \"Mine\".")]
    [SerializeField] private string verb = "Chop";
    [SerializeField] private ToolType tool = ToolType.Hatchet;
    [SerializeField] private ResourceType item = ResourceType.Wood;
    [Tooltip("How many it throws out when it breaks.")]
    [SerializeField, Min(1)] private int amount = 3;
    [Tooltip("How many hits it takes to break.")]
    [SerializeField, Min(1)] private int hitsToBreak = 3;
    [Tooltip("Seconds before it grows back.")]
    [SerializeField, Min(1f)] private float regrowSeconds = 120f;

    [Header("Looks")]
    [Tooltip("Shown while it can be gathered.")]
    [SerializeField] private Transform whole;
    [Tooltip("Shown after it breaks until it grows back, like a stump or rubble.")]
    [SerializeField] private GameObject broken;
    [Tooltip("Trees topple over when they break; rocks crumble.")]
    [SerializeField] private bool topples = true;

    private int hits;
    private float regrowAt;
    private Vector3 wholeScale;
    private Quaternion wholeRotation;
    private readonly List<Collider> solidParts = new List<Collider>();
    private Coroutine motion;

    public bool IsBroken { get; private set; }
    public ToolType Tool => tool;
    public ResourceType Item => item;
    public int HitsToBreak => hitsToBreak;

    private void Awake()
    {
        if (inventory == null || dropPrefab == null || whole == null || broken == null)
        {
            Debug.LogError($"{name} needs the Inventory, the drop prefab and its whole and broken looks.", this);
            enabled = false;
            return;
        }
        wholeScale = whole.localScale;
        wholeRotation = whole.localRotation;
        foreach (Collider part in whole.GetComponentsInChildren<Collider>(true))
            if (part.enabled && !part.isTrigger) solidParts.Add(part);
        broken.SetActive(false);
    }

    private void Update()
    {
        if (IsBroken && Time.time >= regrowAt && !PlayerStandsHere())
        {
            Regrow();
        }
    }

    // ---------- Use Tool (left click) ----------

    public string GetToolPrompt(PlayerTools tools)
    {
        if (!enabled || IsBroken)
        {
            return null;
        }
        if (tools.Equipped == tool)
        {
            return hits == 0 ? $"{verb} {displayName}" : $"{verb} {displayName} ({hitsToBreak - hits} more)";
        }
        // The other gathering tool says which one this needs; the hoe and the can say nothing here.
        return tools.Equipped == ToolType.Hatchet || tools.Equipped == ToolType.Pickaxe ? $"Needs the {ToolName}" : null;
    }

    public bool CanUseTool(PlayerTools tools) => enabled && !IsBroken && tools.Equipped == tool;

    public void UseTool(PlayerTools tools) => Hit(tools.transform.position);

    // ---------- Interact (E): only says what it is ----------

    public bool CanInteract => false;

    public string GetPrompt()
    {
        if (!enabled)
        {
            return null;
        }
        return IsBroken
            ? $"This {displayName} grows back soon"
            : $"{char.ToUpper(displayName[0]) + displayName.Substring(1)}: {verb.ToLower()} it with the {ToolName}";
    }

    public void Interact(PlayerInteractor player) { }

    private string ToolName => tool.ToString().ToLower();

    // One hit from someone standing at `from`. The last hit breaks it, falling away from them.
    public void Hit(Vector3 from)
    {
        if (IsBroken)
        {
            return;
        }
        hits++;
        // Chips fly from the side facing the player.
        Vector3 toward = from - transform.position;
        toward.y = 0f;
        toward = toward.sqrMagnitude > 0.01f ? toward.normalized : transform.forward;
        GameEffects.Play(topples ? Effect.WoodChips : Effect.StoneChips, transform.position + Vector3.up * (topples ? 1.1f : 0.6f) + toward * 0.5f);
        if (hits < hitsToBreak)
        {
            Play(Shake());
            return;
        }

        IsBroken = true;
        regrowAt = Time.time + regrowSeconds;
        SetSolid(false);
        broken.SetActive(true);
        Vector3 away = transform.position - from;
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
        whole.localRotation = wholeRotation;
        Play(topples ? Topple(away) : Crumble());
        if (topples)
        {
            GameEffects.Play(Effect.Leaves, transform.position + Vector3.up * 2.8f + away * 1.2f, 1.5f);
        }
        else
        {
            GameEffects.Play(Effect.StoneChips, transform.position + Vector3.up * 0.5f, 2.5f);
            GameEffects.Play(Effect.Dust, transform.position + Vector3.up * 0.3f, 1.2f);
        }
        ThrowDrops(-away);
    }

    private void ThrowDrops(Vector3 towardPlayer)
    {
        for (int i = 0; i < amount; i++)
        {
            // Fan out on the player's side, so the drops land in the open and not behind the stump.
            float angle = amount == 1 ? 0f : Mathf.Lerp(-50f, 50f, i / (amount - 1f));
            Vector3 landing = Ground(transform.position + Quaternion.Euler(0f, angle, 0f) * towardPlayer * 1.6f);
            ItemDrop drop = Instantiate(dropPrefab, transform.position + Vector3.up * 1.5f, Quaternion.identity, transform.parent);
            drop.Launch(inventory, item, 1, landing);
        }
    }

    private void Regrow()
    {
        IsBroken = false;
        hits = 0;
        broken.SetActive(false);
        whole.localRotation = wholeRotation;
        whole.gameObject.SetActive(true);
        SetSolid(true);
        Play(Grow());
    }

    // Waits to grow back while the player stands where it would appear.
    private bool PlayerStandsHere()
    {
        foreach (Collider other in Physics.OverlapSphere(transform.position + Vector3.up, 1.5f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (other.GetComponentInParent<PlayerController>() != null)
            {
                return true;
            }
        }
        return false;
    }

    private void SetSolid(bool solid)
    {
        foreach (Collider part in solidParts)
        {
            part.enabled = solid;
        }
    }

    private static Vector3 Ground(Vector3 at) =>
        Physics.Raycast(at + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, 1 << 6, QueryTriggerInteraction.Ignore) ? hit.point : at;

    // ---------- Movement ----------

    private void Play(IEnumerator routine)
    {
        if (motion != null)
        {
            StopCoroutine(motion);
        }
        motion = StartCoroutine(routine);
    }

    private IEnumerator Shake()
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
        {
            float strength = 1f - t;
            whole.localRotation = wholeRotation * Quaternion.Euler(Mathf.Sin(t * 50f) * 4f * strength, 0f, Mathf.Cos(t * 43f) * 3f * strength);
            yield return null;
        }
        whole.localRotation = wholeRotation;
    }

    // Tips over around its base, speeding up as it falls, then disappears.
    private IEnumerator Topple(Vector3 away)
    {
        Vector3 axis = Vector3.Cross(Vector3.up, away);
        Quaternion start = whole.rotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.9f)
        {
            whole.rotation = Quaternion.AngleAxis(85f * t * t, axis) * start;
            yield return null;
        }
        whole.rotation = Quaternion.AngleAxis(85f, axis) * start;
        GameEffects.Play(Effect.Dust, transform.position + away * 2.5f + Vector3.up * 0.3f, 1.4f);
        GameEffects.Play(Effect.Leaves, transform.position + away * 3.5f + Vector3.up * 0.6f);
        yield return Shrink(0.3f);
    }

    private IEnumerator Crumble()
    {
        yield return Shake();
        yield return Shrink(0.35f);
    }

    private IEnumerator Shrink(float seconds)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
        {
            whole.localScale = wholeScale * (1f - t);
            yield return null;
        }
        whole.gameObject.SetActive(false);
        whole.localScale = wholeScale;
        whole.localRotation = wholeRotation;
    }

    private IEnumerator Grow()
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f)
        {
            float k = 1f - (1f - t) * (1f - t);
            whole.localScale = wholeScale * Mathf.Max(0.05f, k);
            yield return null;
        }
        whole.localScale = wholeScale;
    }

    // Used by the setup menu.
    public void Configure(Inventory sharedInventory, ItemDrop drop, Transform wholeLook, GameObject brokenLook)
    {
        inventory = sharedInventory;
        dropPrefab = drop;
        whole = wholeLook;
        broken = brokenLook;
    }

    // Used by the setup menu.
    public void Describe(string label, string action, ToolType needs, ResourceType gives, int count, int hitCount, float regrow, bool falls)
    {
        displayName = label;
        verb = action;
        tool = needs;
        item = gives;
        amount = count;
        hitsToBreak = hitCount;
        regrowSeconds = regrow;
        topples = falls;
    }
}
