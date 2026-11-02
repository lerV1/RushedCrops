using System.Collections.Generic;
using UnityEngine;

// The garden's crop field: a stretch of soil the player can farm anywhere, on a grid of squares.
// Aim at the soil and an outline snaps to the square under the crosshair. Hold the hoe and use it
// to till that square; a tilled square (FarmTile) is then planted, watered and harvested on its own.
//
// The field has no collider: it is an IAreaTarget, so PlayerInteractor hands it the point the look
// ray hit on the ground and the field works out the square. Squares only exist once tilled.
// Shared settings (Inventory, crops, growth speed, wet soil) are on the FarmPlot next to this.
[RequireComponent(typeof(FarmPlot))]
public class FarmField : MonoBehaviour, IAreaTarget, IInteractable, IToolTarget, IHighlightable
{
    [Tooltip("Size of the field in metres: x across, y along. The field's position is its centre.")]
    [SerializeField] private Vector2 size = new Vector2(20f, 16f);

    [Tooltip("Width of one square in metres.")]
    [SerializeField, Min(0.5f)] private float cellSize = 1f;

    [Tooltip("Copied for every square the player tills: a FarmTile with its soil mound.")]
    [SerializeField] private FarmTile tilePrefab;

    [Tooltip("The outline that follows the square under the crosshair.")]
    [SerializeField] private GameObject cursor;

    [Tooltip("What counts as the ground. Squares sit on it.")]
    [SerializeField] private LayerMask groundLayers = 1 << 6;

    private readonly Dictionary<Vector2Int, FarmTile> tiles = new Dictionary<Vector2Int, FarmTile>();
    private Vector2Int aimed;
    private bool highlighted;

    public Vector2Int GridSize => new Vector2Int(Mathf.Max(1, Mathf.FloorToInt(size.x / cellSize)), Mathf.Max(1, Mathf.FloorToInt(size.y / cellSize)));

    // The square the player is aiming at.
    public Vector2Int AimedSquare => aimed;

    // The tilled square at a grid position, or null.
    public FarmTile TileAt(Vector2Int square) => tiles.TryGetValue(square, out FarmTile tile) ? tile : null;

    private void Awake()
    {
        if (tilePrefab == null || cursor == null)
        {
            Debug.LogError($"{name} needs its tile prefab and cursor assigned.", this);
            enabled = false;
            return;
        }
        cursor.SetActive(false);
    }

    private void OnEnable() => AreaTargets.All.Add(this);

    private void OnDisable() => AreaTargets.All.Remove(this);

    // ---------- Aiming ----------

    public bool TryAim(Vector3 point)
    {
        Vector3 local = transform.InverseTransformPoint(point);
        if (Mathf.Abs(local.x) > size.x * 0.5f || Mathf.Abs(local.z) > size.y * 0.5f)
        {
            return false;
        }

        Vector2Int grid = GridSize;
        aimed = new Vector2Int(
            Mathf.Clamp(Mathf.FloorToInt((local.x + size.x * 0.5f) / cellSize), 0, grid.x - 1),
            Mathf.Clamp(Mathf.FloorToInt((local.z + size.y * 0.5f) / cellSize), 0, grid.y - 1));
        if (highlighted)
        {
            PlaceCursor();
        }
        return true;
    }

    public void SetHighlighted(bool on)
    {
        highlighted = on;
        cursor.SetActive(on);
        if (on)
        {
            PlaceCursor();
        }
    }

    private void PlaceCursor()
    {
        cursor.transform.SetPositionAndRotation(SquareCentre(aimed), transform.rotation);
    }

    // The middle of a square, on the ground.
    public Vector3 SquareCentre(Vector2Int square)
    {
        Vector3 local = new Vector3(
            -size.x * 0.5f + (square.x + 0.5f) * cellSize, 0f,
            -size.y * 0.5f + (square.y + 0.5f) * cellSize);
        Vector3 centre = transform.TransformPoint(local);
        return Physics.Raycast(centre + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f, groundLayers)
            ? hit.point
            : centre;
    }

    // ---------- Interact (E): handed to the tilled square ----------

    public bool CanInteract => TileAt(aimed) is FarmTile tile && tile.CanInteract;

    public string GetPrompt()
    {
        FarmTile tile = TileAt(aimed);
        return tile != null ? tile.GetPrompt() : "Till the soil with a hoe first";
    }

    public void Interact(PlayerInteractor player)
    {
        TileAt(aimed)?.Interact(player);
    }

    // ---------- Use Tool (left click): till here, or hand it to the tilled square ----------

    public string GetToolPrompt(PlayerTools tools)
    {
        FarmTile tile = TileAt(aimed);
        if (tile != null)
        {
            return tile.GetToolPrompt(tools);
        }
        if (tools.Equipped != ToolType.Hoe)
        {
            return null;
        }
        return IsBlocked(aimed) ? "Something is in the way" : "Till soil";
    }

    public bool CanUseTool(PlayerTools tools)
    {
        FarmTile tile = TileAt(aimed);
        if (tile != null)
        {
            return tile.CanUseTool(tools);
        }
        return tools.Equipped == ToolType.Hoe && !IsBlocked(aimed);
    }

    public void UseTool(PlayerTools tools)
    {
        FarmTile tile = TileAt(aimed);
        if (tile != null)
        {
            tile.UseTool(tools);
        }
        else if (tools.Equipped == ToolType.Hoe)
        {
            Till(aimed);
        }
    }

    // Digs a new square. Returns the square, or null if it is already tilled or something stands on it.
    public FarmTile Till(Vector2Int square)
    {
        if (tiles.ContainsKey(square) || IsBlocked(square))
        {
            return null;
        }

        FarmTile tile = Instantiate(tilePrefab, SquareCentre(square), transform.rotation, transform);
        tile.name = $"Tile {square.x},{square.y}";
        tiles[square] = tile;
        tile.Till();
        return tile;
    }

    // True when something solid (a crate, a fence post) stands on the square. The player doesn't count.
    private bool IsBlocked(Vector2Int square)
    {
        Vector3 centre = SquareCentre(square) + Vector3.up * 0.4f;
        Vector3 half = new Vector3(cellSize * 0.45f, 0.35f, cellSize * 0.45f);
        foreach (Collider other in Physics.OverlapBox(centre, half, transform.rotation, ~groundLayers, QueryTriggerInteraction.Ignore))
        {
            if (other.GetComponentInParent<PlayerController>() == null)
            {
                return true;
            }
        }
        return false;
    }
}
