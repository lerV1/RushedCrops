using UnityEngine;

// Briefly colours this object when its Health takes damage, then puts its look back.
// It only changes how the object looks; Health does the counting.
// The colour goes on with a MaterialPropertyBlock, so the shared materials, and every
// other object that uses them, are never changed.
//
// Example: call flash.Flash() to flash without any damage.
public class HitFlash : MonoBehaviour
{
    // Colour property names: URP's Lit shader uses _BaseColor, most other shaders use _Color.
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Tooltip("The Health to watch. If empty, the Health on this object is used.")]
    [SerializeField] private Health health;
    [Tooltip("Renderers to flash. If empty, every renderer on this object and its children.")]
    [SerializeField] private Renderer[] renderers;
    [SerializeField] private Color flashColor = Color.white;
    [Tooltip("How long, in seconds, the flash lasts.")]
    [SerializeField, Min(0.01f)] private float flashDuration = 0.12f;

    // Each renderer's own property block from before the flash, put back when it ends.
    private MaterialPropertyBlock[] originals;
    private MaterialPropertyBlock flashBlock;
    private float timeLeft;

    public bool IsFlashing => timeLeft > 0f;

    private void Awake()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>();
        }

        originals = new MaterialPropertyBlock[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            originals[i] = new MaterialPropertyBlock();
        }
        flashBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.Damaged += OnDamaged;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
        }
        // Never leave the object stuck in the flash colour.
        StopFlash();
    }

    private void Update() => Advance(Time.deltaTime);

    // Moves the flash timer on by this many seconds and ends the flash when time is up.
    // Update calls this every frame.
    public void Advance(float seconds)
    {
        if (!IsFlashing)
        {
            return;
        }
        // End the flash before the timer reaches zero, while StopFlash still sees it flashing.
        if (seconds >= timeLeft)
        {
            StopFlash();
        }
        else
        {
            timeLeft -= seconds;
        }
    }

    private void OnDamaged(int amount) => Flash();

    public void Flash()
    {
        // Save the look only when a new flash starts. Saving during a flash would save the flash colour.
        if (!IsFlashing)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].GetPropertyBlock(originals[i]);
                }
            }
        }

        flashBlock.Clear();
        flashBlock.SetColor(BaseColorId, flashColor);
        flashBlock.SetColor(ColorId, flashColor);
        foreach (Renderer part in renderers)
        {
            if (part != null)
            {
                part.SetPropertyBlock(flashBlock);
            }
        }
        timeLeft = flashDuration;
    }

    // Ends a flash now and restores the look from before it.
    public void StopFlash()
    {
        if (!IsFlashing)
        {
            return;
        }
        timeLeft = 0f;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                // An empty block means the renderer had none, so remove it completely.
                renderers[i].SetPropertyBlock(originals[i].isEmpty ? null : originals[i]);
            }
        }
    }
}
