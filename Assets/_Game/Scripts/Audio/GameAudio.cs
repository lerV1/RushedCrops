using UnityEngine;

// Plays the game's sounds. GameAudio.Play(Sfx.Chop, position) plays a sound out in the world (it fades with
// distance); Play2D plays one at the player's ear (swings, coins, the shop). A small pool of audio sources
// is reused. It also runs the ambience (birds by day, crickets by night, a breeze always) and plays the
// player's own sounds: footsteps, jumps, landings, getting hurt, switching tools and winning.
// With no GameAudio in the scene (as in some tests) every call does nothing.
public class GameAudio : MonoBehaviour
{
    [SerializeField] private SoundBank bank;

    [Header("Ambience")]
    [SerializeField] private AudioClip birds;
    [SerializeField] private AudioClip crickets;
    [SerializeField] private AudioClip wind;
    [SerializeField, Range(0f, 1f)] private float birdsVolume = 0.45f;
    [SerializeField, Range(0f, 1f)] private float cricketsVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float windVolume = 0.18f;

    [Header("Player")]
    [SerializeField] private PlayerTools tools;
    [SerializeField] private Health playerHealth;
    [SerializeField] private CameraFeel feel;

    private const int WorldVoices = 16, EarVoices = 6;
    private static GameAudio instance;
    private AudioSource[] worldVoices, earVoices;
    private int nextWorld, nextEar;
    private AudioSource birdsLoop, cricketsLoop, windLoop;
    private EnvironmentCycle cycle;
    private float night;
    private ToolType lastTool;
    private bool wasGrounded = true;

    private void Awake()
    {
        instance = this;
        worldVoices = new AudioSource[WorldVoices];
        for (int i = 0; i < WorldVoices; i++) worldVoices[i] = Voice("World voice", true);
        earVoices = new AudioSource[EarVoices];
        for (int i = 0; i < EarVoices; i++) earVoices[i] = Voice("Ear voice", false);
        birdsLoop = Loop("Birds", birds);
        cricketsLoop = Loop("Crickets", crickets);
        windLoop = Loop("Wind", wind);
        cycle = FindAnyObjectByType<EnvironmentCycle>();
        if (tools != null) lastTool = tools.Equipped;
    }

    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.Damaged += OnPlayerHurt;
        if (feel != null) { feel.Stepped += OnStep; feel.Landed += OnLanded; }
        if (GameManager.Instance != null) GameManager.Instance.VictoryReached += OnVictory;
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.Damaged -= OnPlayerHurt;
        if (feel != null) { feel.Stepped -= OnStep; feel.Landed -= OnLanded; }
        if (GameManager.Instance != null) GameManager.Instance.VictoryReached -= OnVictory;
        if (instance == this) instance = null;
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
            Rigidbody body = tools != null ? tools.GetComponent<Rigidbody>() : null;
            if (wasGrounded && !feel.Grounded && body != null && body.linearVelocity.y > 2f) Play2D(Sfx.Jump);
            wasGrounded = feel.Grounded;
        }
    }

    // ---------- Playing ----------

    public static void Play(Sfx sound, Vector3 position)
    {
        if (instance == null) return;
        instance.PlayOn(instance.NextWorldVoice(), sound, position);
    }

    public static void Play2D(Sfx sound)
    {
        if (instance == null) return;
        AudioSource voice = instance.earVoices[instance.nextEar];
        instance.nextEar = (instance.nextEar + 1) % EarVoices;
        instance.PlayOn(voice, sound, Vector3.zero);
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
        for (int i = 0; i < WorldVoices; i++)
        {
            AudioSource voice = worldVoices[(nextWorld + i) % WorldVoices];
            if (!voice.isPlaying)
            {
                nextWorld = (nextWorld + i + 1) % WorldVoices;
                return voice;
            }
        }
        AudioSource oldest = worldVoices[nextWorld];
        nextWorld = (nextWorld + 1) % WorldVoices;
        return oldest;
    }

    private AudioSource Voice(string label, bool inWorld)
    {
        var go = new GameObject(label);
        go.transform.SetParent(transform, false);
        var voice = go.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = inWorld ? 1f : 0f;
        voice.rolloffMode = AudioRolloffMode.Linear;
        voice.minDistance = 3f;
        voice.maxDistance = 38f;
        voice.dopplerLevel = 0f;
        return voice;
    }

    private AudioSource Loop(string label, AudioClip clip)
    {
        if (clip == null) return null;
        var voice = Voice(label + " ambience", false);
        voice.clip = clip;
        voice.loop = true;
        voice.volume = 0f;
        voice.Play();
        return voice;
    }

    // ---------- The player's own sounds ----------

    private void OnPlayerHurt(int amount) => Play2D(Sfx.PlayerHurt);
    private void OnStep() => Play2D(Sfx.Footstep);
    private void OnLanded(float speed) => Play2D(Sfx.Land);
    private void OnVictory() => Play2D(Sfx.Victory);

    public void Configure(SoundBank sounds, AudioClip birdsClip, AudioClip cricketsClip, AudioClip windClip,
        PlayerTools playerTools, Health health, CameraFeel cameraFeel)
    {
        bank = sounds;
        birds = birdsClip;
        crickets = cricketsClip;
        wind = windClip;
        tools = playerTools;
        playerHealth = health;
        feel = cameraFeel;
    }
}
