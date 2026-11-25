using UnityEngine;

// Background music: a calm farming song while the player works, and a battle song while a mini crop is chasing
// or biting them or a boss is fighting them. The two crossfade; the calm song comes back a few seconds after the
// fight ends, picking up where it left off. Tracks and credits: Assets/_Game/Audio/Music and Audio/CREDITS.md.
public class GameMusic : MonoBehaviour
{
    [SerializeField] private AudioSource peaceful;
    [SerializeField] private AudioSource battle;
    [SerializeField, Range(0f, 1f)] private float volume = 0.32f;
    [Tooltip("Seconds each crossfade takes.")]
    [SerializeField, Min(0.1f)] private float fadeSeconds = 1.8f;
    [Tooltip("Seconds after the last sign of a fight before the calm song returns.")]
    [SerializeField, Min(0f)] private float calmDelay = 4f;
    [Tooltip("How often, in seconds, it checks for a fight.")]
    [SerializeField, Min(0.05f)] private float checkEvery = 0.4f;

    private float lastFightAt = float.NegativeInfinity;
    private float nextCheckAt;
    private float battleMix;

    public bool InBattle => Time.unscaledTime - lastFightAt < calmDelay;

    private void Start()
    {
        if (peaceful == null || battle == null)
        {
            Debug.LogError("GameMusic needs its peaceful and battle AudioSources.", this);
            enabled = false;
            return;
        }
        peaceful.loop = battle.loop = true;
        peaceful.volume = volume;
        battle.volume = 0f;
        peaceful.Play();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextCheckAt)
        {
            nextCheckAt = Time.unscaledTime + checkEvery;
            if (FightGoingOn()) lastFightAt = Time.unscaledTime;
        }

        bool fighting = InBattle;
        if (fighting && !battle.isPlaying)
        {
            battle.time = 0f; // every fight starts the battle song from its opening
            battle.Play();
        }
        battleMix = Mathf.MoveTowards(battleMix, fighting ? 1f : 0f, Time.unscaledDeltaTime / fadeSeconds);
        battle.volume = volume * battleMix;
        peaceful.volume = volume * (1f - battleMix);

        // Pause each song while it is silent, so the calm one resumes mid-tune and the battle one doesn't run on.
        if (battleMix >= 1f && peaceful.isPlaying) peaceful.Pause();
        if (battleMix < 1f && !peaceful.isPlaying) peaceful.UnPause();
        if (battleMix <= 0f && battle.isPlaying) battle.Stop();
    }

    // A boss in the middle of a fight, or a mini crop chasing or biting the player.
    private static bool FightGoingOn()
    {
        foreach (CropBoss boss in CropBoss.All)
        {
            var combat = boss != null ? boss.GetComponent<BossCombat>() : null;
            if (combat != null && combat.IsFighting) return true;
        }
        foreach (MiniCrop crop in FindObjectsByType<MiniCrop>(FindObjectsSortMode.None))
        {
            MiniCrop.State state = crop.CurrentState;
            if (state == MiniCrop.State.Chase || state == MiniCrop.State.Attack) return true;
        }
        return false;
    }
}
