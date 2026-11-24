using UnityEngine;

// Moves every child cloud slowly with the wind. A cloud that drifts out past the far side of the sky
// comes back in on the near side, so the sky never empties.
public class CloudDrift : MonoBehaviour
{
    [Tooltip("Wind direction and speed in metres per second (y is ignored).")]
    [SerializeField] private Vector3 wind = new Vector3(1.2f, 0f, 0.4f);
    [Tooltip("Half the width of the square of sky the clouds wrap round in, centred on this object.")]
    [SerializeField, Min(10f)] private float halfSize = 170f;

    private void Update()
    {
        Vector3 step = new Vector3(wind.x, 0f, wind.z) * Time.deltaTime;
        Vector3 centre = transform.position;
        foreach (Transform cloud in transform)
        {
            Vector3 p = cloud.position + step;
            if (p.x - centre.x > halfSize) p.x -= halfSize * 2f;
            if (p.x - centre.x < -halfSize) p.x += halfSize * 2f;
            if (p.z - centre.z > halfSize) p.z -= halfSize * 2f;
            if (p.z - centre.z < -halfSize) p.z += halfSize * 2f;
            cloud.position = p;
        }
    }
}
