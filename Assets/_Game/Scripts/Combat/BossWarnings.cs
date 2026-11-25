using UnityEngine;

// Saved warning shapes. Combat only moves, scales and shows them.
public class BossWarnings : MonoBehaviour
{
    [SerializeField] private Transform fillPatch;
    [SerializeField] private Transform edgeRing;
    [SerializeField] private Transform stripe;
    [SerializeField] private Transform[] spots;

    public void Detach()
    {
        // Ground warnings must not turn or scale with the boss.
        transform.SetParent(transform.parent.parent, true);
        HideAll();
    }

    public void ShowFill(Vector3 centre, float radius) => Show(fillPatch, centre, radius);
    public void ShowEdge(Vector3 centre, float radius) => Show(edgeRing, centre, radius);

    public void ShowSpot(int index, Vector3 centre, float radius)
    {
        if (index >= 0 && index < spots.Length) Show(spots[index], centre, radius);
    }

    private static void Show(Transform marker, Vector3 centre, float radius)
    {
        marker.gameObject.SetActive(true);
        marker.SetPositionAndRotation(centre + Vector3.up * 0.06f, Quaternion.identity);
        marker.localScale = new Vector3(Mathf.Max(radius, 0.01f), 1f, Mathf.Max(radius, 0.01f));
    }

    public void ShowLane(Vector3 start, Vector3 direction, float length, float width)
    {
        stripe.gameObject.SetActive(true);
        stripe.SetPositionAndRotation(start + Vector3.up * 0.07f, Quaternion.LookRotation(direction));
        stripe.localScale = new Vector3(width, 1f, Mathf.Max(length, 0.01f));
    }

    public void HideAll()
    {
        foreach (Transform marker in transform) marker.gameObject.SetActive(false);
    }
}
