using UnityEngine;

public class GuideArrow : MonoBehaviour
{
    [Tooltip("The camera that shows the target.")]
    [SerializeField] private Camera viewCamera;

    [Tooltip("How far the arrow stays from the edge of the screen.")]
    [SerializeField, Min(16f)] private float edgePadding = 52f;

    [Tooltip("How far above an on-screen target the arrow sits.")]
    [SerializeField, Min(0f)] private float targetOffset = 42f;

    private Transform target;
    private GUIStyle arrowStyle;

    public void SetTarget(Transform nextTarget)
    {
        target = nextTarget;
    }

    public void Clear()
    {
        target = null;
    }

    private void Awake()
    {
        if (viewCamera == null)
        {
            viewCamera = Camera.main;
        }
    }

    private void OnGUI()
    {
        if (target == null || viewCamera == null)
        {
            return;
        }

        Vector3 screen = viewCamera.WorldToScreenPoint(target.position + Vector3.up);
        Vector2 targetPoint = new Vector2(screen.x, Screen.height - screen.y);
        Vector2 centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        bool onScreen = screen.z > 0f
            && screen.x >= edgePadding && screen.x <= Screen.width - edgePadding
            && screen.y >= edgePadding && screen.y <= Screen.height - edgePadding;

        Vector2 arrowPoint;
        Vector2 direction;
        if (onScreen)
        {
            arrowPoint = targetPoint + Vector2.down * targetOffset;
            direction = targetPoint - arrowPoint;
        }
        else
        {
            direction = targetPoint - centre;
            if (screen.z < 0f)
            {
                direction = -direction;
            }
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = Vector2.up;
            }

            float halfWidth = Mathf.Max(1f, Screen.width * 0.5f - edgePadding);
            float halfHeight = Mathf.Max(1f, Screen.height * 0.5f - edgePadding);
            float xScale = Mathf.Abs(direction.x) > 0.001f ? halfWidth / Mathf.Abs(direction.x) : float.PositiveInfinity;
            float yScale = Mathf.Abs(direction.y) > 0.001f ? halfHeight / Mathf.Abs(direction.y) : float.PositiveInfinity;
            arrowPoint = centre + direction * Mathf.Min(xScale, yScale);
            direction = targetPoint - arrowPoint;
            if (screen.z < 0f)
            {
                direction = -direction;
            }
        }

        if (direction.sqrMagnitude < 0.001f)
        {
            direction = Vector2.up;
        }

        DrawArrow(arrowPoint, direction);
    }

    private void DrawArrow(Vector2 point, Vector2 direction)
    {
        if (arrowStyle == null)
        {
            arrowStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 34,
                fontStyle = FontStyle.Bold
            };
            arrowStyle.normal.textColor = new Color(1f, 0.78f, 0.12f);
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
        Matrix4x4 previousMatrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, point);
        GUI.Label(new Rect(point.x - 24f, point.y - 24f, 48f, 48f), "▲", arrowStyle);
        GUI.matrix = previousMatrix;
    }
}
