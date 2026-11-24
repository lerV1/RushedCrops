using UnityEngine;

// Springs something into view: it starts small, overshoots a little and settles at its full size.
// Added to each new growth stage of a crop and to dropped items as they land.
public class PopIn : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float duration = 0.35f;
    [SerializeField, Range(0f, 1f)] private float startScale = 0.35f;

    private Vector3 fullScale;
    private float time;

    private void Awake() => fullScale = transform.localScale;

    private void OnEnable()
    {
        time = 0f;
        transform.localScale = fullScale * startScale;
    }

    private void Update()
    {
        time += Time.deltaTime;
        float t = Mathf.Clamp01(time / duration);
        // Eases out past 1 and back: a small bounce.
        float s = 1f + 2.2f * Mathf.Pow(t - 1f, 3f) + 1.2f * Mathf.Pow(t - 1f, 2f);
        transform.localScale = fullScale * Mathf.LerpUnclamped(startScale, 1f, s);
        if (t >= 1f)
        {
            transform.localScale = fullScale;
            enabled = false;
        }
    }

    public static void On(GameObject target, float seconds = 0.35f)
    {
        if (target == null) return;
        var pop = target.AddComponent<PopIn>();
        pop.duration = seconds;
    }
}
