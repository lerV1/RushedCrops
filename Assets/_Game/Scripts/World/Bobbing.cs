using UnityEngine;

// Rocks something gently on the water: a slow rise and fall with a little roll and pitch.
public class Bobbing : MonoBehaviour
{
    [SerializeField, Min(0f)] private float height = 0.06f;
    [SerializeField, Min(0f)] private float roll = 3f;
    [SerializeField, Min(0.01f)] private float period = 3.2f;

    private Vector3 restPosition;
    private Quaternion restRotation;
    private float offset;

    private void Awake()
    {
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        offset = Random.value * 10f;
    }

    private void Update()
    {
        float t = (Time.time + offset) * Mathf.PI * 2f / period;
        transform.localPosition = restPosition + Vector3.up * Mathf.Sin(t) * height;
        transform.localRotation = restRotation * Quaternion.Euler(Mathf.Sin(t * 0.7f) * roll * 0.5f, 0f, Mathf.Sin(t * 0.9f + 1f) * roll);
    }
}
