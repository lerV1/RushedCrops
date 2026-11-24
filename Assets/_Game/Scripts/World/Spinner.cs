using UnityEngine;

// Turns something round its hub, like the windmill's sails. The speed drifts a little, as if the wind
// came in gusts.
public class Spinner : MonoBehaviour
{
    [Tooltip("The axis it turns round, in its own space.")]
    [SerializeField] private Vector3 axis = Vector3.forward;
    [Tooltip("The point it turns round, in its own space (the middle of the sails).")]
    [SerializeField] private Vector3 hub = Vector3.zero;
    [Tooltip("Degrees per second on an average breeze.")]
    [SerializeField] private float speed = 40f;
    [Tooltip("How much gusts speed it up or slow it down, as a share of the speed.")]
    [SerializeField, Range(0f, 1f)] private float gusts = 0.35f;

    private float seed;

    private void Awake() => seed = Random.value * 100f;

    private void Update()
    {
        float gust = 1f + (Mathf.PerlinNoise(seed, Time.time * 0.15f) - 0.5f) * 2f * gusts;
        transform.RotateAround(transform.TransformPoint(hub), transform.TransformDirection(axis), speed * gust * Time.deltaTime);
    }

    public void Configure(Vector3 localAxis, Vector3 localHub, float degreesPerSecond)
    {
        axis = localAxis;
        hub = localHub;
        speed = degreesPerSecond;
    }
}
