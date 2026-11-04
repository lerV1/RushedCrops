using UnityEngine;

// A bag of one crop's seeds lying on the ground, dropped by the crop boss.
// It flies out in an arc, lands, and then bobs and spins until the player takes it:
// walk into it, or look at it and press Interact. With a full hotbar it stays where it is.
public class SeedDrop : MonoBehaviour, IInteractable
{
    [Tooltip("How close, in metres, the player has to walk to pick the bag up.")]
    [SerializeField, Min(0f)] private float pickupRadius = 1.3f;
    [Tooltip("How long, in seconds, the bag flies before it lands.")]
    [SerializeField, Min(0.05f)] private float flightTime = 0.6f;
    [SerializeField] private LayerMask groundLayers = 1 << 6;

    private Inventory inventory;
    private CropData crop;
    private int amount;
    private Vector3 from;
    private Vector3 landing;
    private float flown;
    private Transform player;

    public CropData Crop => crop;
    public int Amount => amount;
    public bool HasLanded => flown >= flightTime;

    // Called by whoever drops the bag: what is in it, and where on the ground it lands.
    public void Launch(Inventory target, CropData seeds, int count, Vector3 landingPoint)
    {
        inventory = target;
        crop = seeds;
        amount = count;
        from = transform.position;
        landing = Physics.Raycast(landingPoint + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f, groundLayers)
            ? hit.point
            : landingPoint;
        flown = 0f;
        name = $"{crop.DisplayName} seed bag";
    }

    private void Start()
    {
        var controller = FindAnyObjectByType<PlayerController>();
        player = controller != null ? controller.transform : null;
    }

    private void Update()
    {
        if (crop == null)
        {
            return;
        }

        if (!HasLanded)
        {
            flown = Mathf.Min(flown + Time.deltaTime, flightTime);
            float t = flown / flightTime;
            // A straight line from the boss to the landing spot, lifted into an arc.
            transform.position = Vector3.Lerp(from, landing, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 1.5f);
            return;
        }

        // Bob and spin, so it is easy to spot on the grass.
        transform.position = landing + Vector3.up * (0.15f + Mathf.Sin(Time.time * 3f) * 0.08f);
        transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);

        if (player != null && (player.position - transform.position).sqrMagnitude < pickupRadius * pickupRadius)
        {
            TryCollect();
        }
    }

    public bool CanInteract => crop != null && HasLanded && inventory.CanAdd(crop.SeedResource, amount);

    public string GetPrompt()
    {
        if (crop == null)
        {
            return null;
        }
        return inventory.CanAdd(crop.SeedResource, amount)
            ? $"Pick up {amount} {crop.DisplayName.ToLower()} seeds"
            : "Hotbar is full";
    }

    public void Interact(PlayerInteractor player) => TryCollect();

    // Puts the seeds in the hotbar and removes the bag. Returns false if there was no room.
    public bool TryCollect()
    {
        if (crop == null || !HasLanded || !inventory.Add(crop.SeedResource, amount))
        {
            return false;
        }
        Destroy(gameObject);
        return true;
    }
}
