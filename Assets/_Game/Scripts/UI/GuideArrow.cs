using UnityEngine;

// Positions a saved UI arrow above its target, or along the screen edge.
public class GuideArrow : MonoBehaviour
{
    [SerializeField] private Camera viewCamera;
    [SerializeField] private RectTransform arrowGraphic;
    [SerializeField, Min(16f)] private float edgePadding = 52f;
    [SerializeField, Min(0f)] private float targetOffset = 42f;

    private Transform target;

    private void Awake()
    {
        if (viewCamera == null || arrowGraphic == null)
        {
            Debug.LogError("GuideArrow needs its Camera and saved arrow graphic assigned.", this);
            enabled = false;
        }
    }

    public void SetTarget(Transform nextTarget)
    {
        target = nextTarget;
    }

    public void Clear()
    {
        target = null;
        if (arrowGraphic != null) arrowGraphic.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (arrowGraphic != null) arrowGraphic.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (arrowGraphic == null) return;
        bool visible = target != null && viewCamera != null;
        arrowGraphic.gameObject.SetActive(visible);
        if (!visible) return;

        Vector3 screen = viewCamera.WorldToScreenPoint(target.position + Vector3.up);
        Vector2 targetPoint = new Vector2(screen.x, screen.y);
        Vector2 centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        bool onScreen = screen.z > 0f
            && screen.x >= edgePadding && screen.x <= Screen.width - edgePadding
            && screen.y >= edgePadding && screen.y <= Screen.height - edgePadding;

        Vector2 arrowPoint;
        Vector2 direction;
        if (onScreen)
        {
            arrowPoint = targetPoint + Vector2.up * targetOffset;
            direction = targetPoint - arrowPoint;
        }
        else
        {
            direction = targetPoint - centre;
            if (screen.z < 0f) direction = -direction;
            if (direction.sqrMagnitude < 0.001f) direction = Vector2.up;

            float halfWidth = Mathf.Max(1f, Screen.width * 0.5f - edgePadding);
            float halfHeight = Mathf.Max(1f, Screen.height * 0.5f - edgePadding);
            float xScale = Mathf.Abs(direction.x) > 0.001f ? halfWidth / Mathf.Abs(direction.x) : float.PositiveInfinity;
            float yScale = Mathf.Abs(direction.y) > 0.001f ? halfHeight / Mathf.Abs(direction.y) : float.PositiveInfinity;
            arrowPoint = centre + direction * Mathf.Min(xScale, yScale);
            direction = targetPoint - arrowPoint;
            if (screen.z < 0f) direction = -direction;
        }

        if (direction.sqrMagnitude < 0.001f) direction = Vector2.up;
        arrowGraphic.position = new Vector3(arrowPoint.x, arrowPoint.y, 0f);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        arrowGraphic.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}
