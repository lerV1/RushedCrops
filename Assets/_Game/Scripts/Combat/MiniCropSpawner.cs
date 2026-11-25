using System.Collections.Generic;
using UnityEngine;

// Every so often an evil mini crop climbs out of the crop field. Put this on the crop field: the area is
// centred on this object. Only a few can be out at once, and none climbs out right next to the player.
public class MiniCropSpawner : MonoBehaviour
{
    [Tooltip("One prefab per crop. A random one climbs out each time.")]
    [SerializeField] private MiniCrop[] cropPrefabs = new MiniCrop[0];

    [Header("References")]
    [Tooltip("The scene's shared Inventory. Dropped crops go into it.")]
    [SerializeField] private Inventory inventory;
    [Tooltip("The player's Health. The crops chase and bite this.")]
    [SerializeField] private Health player;

    [Header("Where")]
    [Tooltip("Size of the area in metres: x across, y along. Usually the crop field's size.")]
    [SerializeField] private Vector2 areaSize = new Vector2(20f, 16f);
    [Tooltip("What counts as the ground. Crops climb out where a ray down hits it.")]
    [SerializeField] private LayerMask groundLayers = 1 << 6;
    [Tooltip("No crop climbs out closer than this to the player, in metres.")]
    [SerializeField, Min(0f)] private float minPlayerDistance = 5f;

    [Header("When (seconds)")]
    [SerializeField, Min(0f)] private float firstSpawnDelay = 20f;
    [SerializeField, Min(1f)] private float minDelay = 25f;
    [SerializeField, Min(1f)] private float maxDelay = 50f;
    [Tooltip("Most crops out in the field at the same time.")]
    [SerializeField, Min(1)] private int maxAlive = 3;

    private readonly List<MiniCrop> alive = new List<MiniCrop>();
    private float timeLeft;

    public int AliveCount
    {
        get
        {
            RemoveDead();
            return alive.Count;
        }
    }

    public MiniCrop[] CropPrefabs => cropPrefabs;

    private void Start()
    {
        if (cropPrefabs.Length == 0 || inventory == null || player == null)
        {
            Debug.LogError($"{name} needs crop prefabs, the Inventory and the player's Health.", this);
            enabled = false;
            return;
        }
        timeLeft = firstSpawnDelay;
    }

    private void Update()
    {
        timeLeft -= Time.deltaTime;
        if (timeLeft > 0f)
        {
            return;
        }
        timeLeft = Random.Range(minDelay, maxDelay);
        if (!player.IsDead)
        {
            SpawnRandom();
        }
    }

    // Makes one random crop climb out now, if there is room. Returns null if none could.
    [ContextMenu("Spawn one now")]
    public MiniCrop SpawnRandom()
    {
        if (cropPrefabs.Length == 0)
        {
            return null;
        }
        return Spawn(cropPrefabs[Random.Range(0, cropPrefabs.Length)]);
    }

    public MiniCrop Spawn(MiniCrop prefab)
    {
        if (prefab == null || AliveCount >= maxAlive)
        {
            return null;
        }

        // Try a few random spots; skip any too close to the player or with no ground under it.
        for (int tries = 0; tries < 10; tries++)
        {
            Vector3 local = new Vector3(Random.Range(-0.5f, 0.5f) * areaSize.x, 0f, Random.Range(-0.5f, 0.5f) * areaSize.y);
            Vector3 spot = transform.TransformPoint(local);
            if (player != null && Vector3.Distance(spot, player.transform.position) < minPlayerDistance)
            {
                continue;
            }
            if (!Physics.Raycast(spot + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 30f, groundLayers, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            Quaternion facing = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            MiniCrop crop = Instantiate(prefab, hit.point, facing, transform);
            crop.Setup(inventory, player);
            alive.Add(crop);
            return crop;
        }
        return null;
    }

    // Destroyed crops show up as null in the list.
    private void RemoveDead()
    {
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            if (alive[i] == null)
            {
                alive.RemoveAt(i);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.3f, 0.2f, 0.8f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(areaSize.x, 0.1f, areaSize.y));
    }
}
