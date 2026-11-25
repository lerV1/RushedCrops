using UnityEngine;

// Plays the game's sounds. GameAudio.Play(Sfx.Chop, position) plays a sound out in the world (it fades with
// distance); Play2D plays one at the player's ear (swings, coins, the shop). The assigned audio sources
// are reused. It also runs the ambience (birds by day, crickets by night, a breeze always) and plays the
// player's own sounds: footsteps, jumps, landings, getting hurt, switching tools and winning.
// With no active GameAudio in the scene, calls do nothing.
public class GameAudio : MonoBehaviour
{
    [SerializeField] private SoundBank bank;

    [Header("Reusable sound sources")]
    [SerializeField] private AudioSource[] worldVoices;
    [SerializeField] private AudioSource[] earVoices;

    [Header("Ambience")]
    [SerializeField] private AudioSource birdsLoop;
    [SerializeField] private AudioSource cricketsLoop;
    [SerializeField] private AudioSource windLoop;
    [SerializeField] private EnvironmentCycle cycle;
    [SerializeField, Range(0f, 1f)] private float birdsVolume = 0.45f;
    [SerializeField, Range(0f, 1f)] private float cricketsVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float windVolume = 0.18f;

    [Header("Player")]
    [SerializeField] private PlayerTools tools;
    [SerializeField] private Health playerHealth;
    [SerializeField] private CameraFeel feel;
    [SerializeField] private Rigidbody playerBody;
    [SerializeField] private GameManager gameManager;

    private static event System.Action<Sfx, Vector3, bool> SoundRequested;
    private int nextWorld, nextEar;
    private float night;
    private ToolType lastTool;
    private bool wasGrounded = true;

    private void Awake()
    {
        if (bank == null || !SourcesAssigned(worldVoices) || !SourcesAssigned(earVoices) ||
            birdsLoop == null || cricketsLoop == null || windLoop == null || cycle == null ||
            playerBody == null || gameManager == null)
        {
            Debug.LogError("GameAudio needs its sound bank, audio sources, environment cycle, player Rigidbody and GameManager assigned.", this);
            enabled = false;
            return;
        }

        nextWorld = 0;
        nextEar = 0;
        birdsLoop.volume = 0f;
        cricketsLoop.volume = 0f;
        windLoop.volume = 0f;
        if (tools != null) lastTool = tools.Equipped;
    }

    private void OnEnable()
    {
        SoundRequested += OnSoundRequested;
        if (!birdsLoop.isPlaying) birdsLoop.Play();
        if (!cricketsLoop.isPlaying) cricketsLoop.Play();
        if (!windLoop.isPlaying) windLoop.Play();
        if (playerHealth != null) playerHealth.Damaged += OnPlayerHurt;
        if (feel != null) { feel.Stepped += OnStep; feel.Landed += OnLanded; }
        gameManager.VictoryReached += OnVictory;
    }

    private void OnDisable()
    {
        SoundRequested -= OnSoundRequested;
        if (playerHealth != null) playerHealth.Damaged -= OnPlayerHurt;
        if (feel != null) { feel.Stepped -= OnStep; feel.Landed -= OnLanded; }
        if (gameManager != null) gameManager.VictoryReached -= OnVictory;
    }

    private void Update()
    {
        // Birds fade out and crickets in over a few seconds as night falls, and back at dawn.
        bool isNight = cycle != null && cycle.IsNight;
        night = Mathf.MoveTowards(night, isNight ? 1f : 0f, Time.deltaTime / 5f);
        if (birdsLoop != null) birdsLoop.volume = birdsVolume * (1f - night);
        if (cricketsLoop != null) cricketsLoop.volume = cricketsVolume * night;
        if (windLoop != null) windLoop.volume = windVolume * (0.7f + 0.3f * night);

        if (tools != null && tools.Equipped != lastTool)
        {
            lastTool = tools.Equipped;
            Play2D(Sfx.ToolSwitch);
        }
        if (feel != null)
        {
            // Leaving the ground going up is a jump; a fall off an edge makes no sound until it lands.
            if (wasGrounded && !feel.Grounded && playerBody.linearVelocity.y > 2f) Play2D(Sfx.Jump);
            wasGrounded = feel.Grounded;
        }
    }

    // ---------- Playing ----------

    public static void Play(Sfx sound, Vector3 position)
    {
        SoundRequested?.Invoke(sound, position, true);
    }

    public static void Play2D(Sfx sound)
    {
        SoundRequested?.Invoke(sound, Vector3.zero, false);
    }

    private void OnSoundRequested(Sfx sound, Vector3 position, bool spatial)
    {
        if (spatial)
        {
            PlayOn(NextWorldVoice(), sound, position);
            return;
        }

        AudioSource voice = earVoices[nextEar];
        nextEar = (nextEar + 1) % earVoices.Length;
        PlayOn(voice, sound, Vector3.zero);
    }

    private void PlayOn(AudioSource voice, Sfx sound, Vector3 position)
    {
        SoundBank.Entry entry = bank != null ? bank.Get(sound) : null;
        if (entry == null || entry.clips.Length == 0) return;
        voice.transform.position = position;
        voice.clip = entry.clips[Random.Range(0, entry.clips.Length)];
        voice.volume = entry.volume;
        voice.pitch = Random.Range(entry.pitch.x, entry.pitch.y);
        voice.Play();
    }

    // A free voice if there is one, otherwise the one that started longest ago.
    private AudioSource NextWorldVoice()
    {
        for (int i = 0; i < worldVoices.Length; i++)
        {
            AudioSource voice = worldVoices[(nextWorld + i) % worldVoices.Length];
            if (!voice.isPlaying)
            {
                nextWorld = (nextWorld + i + 1) % worldVoices.Length;
                return voice;
            }
        }
        AudioSource oldest = worldVoices[nextWorld];
        nextWorld = (nextWorld + 1) % worldVoices.Length;
        return oldest;
    }

    private static bool SourcesAssigned(AudioSource[] sources)
    {
        if (sources == null || sources.Length == 0) return false;
        foreach (AudioSource source in sources)
        {
            if (source == null) return false;
        }
        return true;
    }

    // ---------- The player's own sounds ----------

    private void OnPlayerHurt(int amount) => Play2D(Sfx.PlayerHurt);
    private void OnStep() => Play2D(Sfx.Footstep);
    private void OnLanded(float speed) => Play2D(Sfx.Land);
    private void OnVictory() => Play2D(Sfx.Victory);
}
