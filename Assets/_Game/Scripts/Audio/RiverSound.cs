using UnityEngine;

// The river's rush. One looping sound slides along the river to the point nearest the listener, so it is
// heard all along the banks and fades away inland. The map builder gives it the river's course.
[RequireComponent(typeof(AudioSource))]
public class RiverSound : MonoBehaviour
{
    [SerializeField] private Vector3[] course = new Vector3[0];

    private AudioSource source;
    private Transform listener;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
    }

    private void LateUpdate()
    {
        if (listener == null)
        {
            var found = FindAnyObjectByType<AudioListener>();
            if (found == null) return;
            listener = found.transform;
        }
        if (course.Length < 2) return;
        Vector3 at = listener.position, nearest = course[0];
        float best = float.MaxValue;
        for (int i = 0; i < course.Length - 1; i++)
        {
            Vector3 a = course[i], segment = course[i + 1] - a;
            float t = Mathf.Clamp01(Vector3.Dot(at - a, segment) / segment.sqrMagnitude);
            Vector3 p = a + segment * t;
            float d = (p - at).sqrMagnitude;
            if (d < best) { best = d; nearest = p; }
        }
        transform.position = nearest;
    }

    public void Configure(Vector3[] riverCourse) => course = riverCourse;
}
