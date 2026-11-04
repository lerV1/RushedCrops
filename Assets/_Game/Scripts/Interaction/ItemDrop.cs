using UnityEngine;

// Some of one item lying on the ground: seeds the crop boss threw out, or something the player
// dropped from the hotbar with Backspace. It flies out in an arc, lands, and then bobs and spins
// until the player takes it: walk into it, or look at it and press Interact.
// With a full hotbar it stays where it is.
// It shows the item's own model from ItemModels, sized to fit a small box.
public class ItemDrop : MonoBehaviour, IInteractable
{
    [Tooltip("The 3D model for each item.")]
    [SerializeField] private ItemModels models;
    [Tooltip("How big, in metres, the shown model is at its longest side.")]
    [SerializeField, Min(0.1f)] private float modelSize = 0.55f;
    [Tooltip("How close, in metres, the player has to walk to pick it up.")]
    [SerializeField, Min(0f)] private float pickupRadius = 1.3f;
    [Tooltip("How long, in seconds, it flies before it lands.")]
    [SerializeField, Min(0.05f)] private float flightTime = 0.6f;
    [SerializeField] private LayerMask groundLayers = 1 << 6;

    private Inventory inventory;
    private ResourceType item;
    private int amount;
    private bool launched;
    private Vector3 from;
    private Vector3 landing;
    private float flown;
    private float walkPickupAt;
    private Transform player;

    public ResourceType Item => item;
    public int Amount => amount;
    public bool HasLanded => launched && flown >= flightTime;

    // Called by whoever drops it: what it is, how many, and where on the ground it lands.
    // walkPickupDelay stops the player who just dropped it from walking straight back into it.
    public void Launch(Inventory target, ResourceType what, int count, Vector3 landingPoint, float walkPickupDelay = 0f)
    {
        inventory = target;
        item = what;
        amount = count;
        from = transform.position;
        landing = Physics.Raycast(landingPoint + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f, groundLayers)
            ? hit.point
            : landingPoint;
        flown = 0f;
        launched = true;
        walkPickupAt = Time.time + flightTime + walkPickupDelay;
        name = $"{ResourceTypes.DisplayName(item)} drop";
        ShowModel();
    }

    private void ShowModel()
    {
        GameObject source = models != null ? models.Get(item) : null;
        if (source == null)
        {
            return;
        }

        var look = Instantiate(source, transform);
        look.name = "Model";
        foreach (Collider part in look.GetComponentsInChildren<Collider>())
        {
            part.enabled = false;
        }
        look.transform.localRotation = Quaternion.Euler(models.TurnFor(item));

        // Shrink or grow it to modelSize, then stand it on the drop's origin.
        Bounds bounds = BoundsOf(look);
        float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (longest > 0.0001f)
        {
            look.transform.localScale *= modelSize / longest;
        }
        bounds = BoundsOf(look);
        look.transform.position += transform.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
    }

    private static Bounds BoundsOf(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(go.transform.position, Vector3.zero);
        }
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer part in renderers)
        {
            bounds.Encapsulate(part.bounds);
        }
        return bounds;
    }

    private void Start()
    {
        var controller = FindAnyObjectByType<PlayerController>();
        player = controller != null ? controller.transform : null;
    }

    private void Update()
    {
        if (!launched)
        {
            return;
        }

        if (!HasLanded)
        {
            flown = Mathf.Min(flown + Time.deltaTime, flightTime);
            float t = flown / flightTime;
            // A straight line to the landing spot, lifted into an arc.
            transform.position = Vector3.Lerp(from, landing, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 1.2f);
            return;
        }

        // Bob and spin, so it is easy to spot on the grass.
        transform.position = landing + Vector3.up * (0.15f + Mathf.Sin(Time.time * 3f) * 0.08f);
        transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);

        if (player != null && Time.time >= walkPickupAt
            && (player.position - transform.position).sqrMagnitude < pickupRadius * pickupRadius)
        {
            TryCollect();
        }
    }

    public bool CanInteract => HasLanded && inventory.CanAdd(item, amount);

    public string GetPrompt()
    {
        if (!launched)
        {
            return null;
        }
        if (!inventory.CanAdd(item, amount))
        {
            return "Hotbar is full";
        }
        string label = ResourceTypes.DisplayName(item).ToLower();
        return amount > 1 ? $"Pick up {amount} {label}" : $"Pick up {label}";
    }

    public void Interact(PlayerInteractor player) => TryCollect();

    // Puts the items in the hotbar and removes the drop. Returns false if there was no room.
    public bool TryCollect()
    {
        if (!HasLanded || !inventory.Add(item, amount))
        {
            return false;
        }
        Destroy(gameObject);
        return true;
    }
}
