using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Plays a model's Blender animations straight from its FBX, with no Animator Controller: one clip at a time,
// blending from the last over a short fade. A clip that doesn't loop can name the clip that follows it, so a
// swing blends back into the idle once it ends.
[RequireComponent(typeof(Animator))]
public class ClipPlayer : MonoBehaviour
{
    [Tooltip("Every clip this model can play, taken from its FBX.")]
    [SerializeField] private AnimationClip[] clips = new AnimationClip[0];
    [Tooltip("The looping clip it starts in.")]
    [SerializeField] private string startClip = "Idle";

    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private AnimationClipPlayable[] players;
    private float[] weights;
    private int current = -1;
    private float fadeSpeed;
    private int following = -1;
    private float followingFade;

    // The clip playing now (or being blended to).
    public string Current => current >= 0 ? clips[current].name : null;
    // True while one clip is still blending into the next.
    public bool IsBlending { get; private set; }
    public IReadOnlyList<AnimationClip> Clips => clips;

    private void Awake()
    {
        var animator = GetComponent<Animator>();
        graph = PlayableGraph.Create(name + " clips");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        mixer = AnimationMixerPlayable.Create(graph, clips.Length);
        players = new AnimationClipPlayable[clips.Length];
        weights = new float[clips.Length];
        for (int i = 0; i < clips.Length; i++)
        {
            players[i] = AnimationClipPlayable.Create(graph, clips[i]);
            graph.Connect(players[i], 0, mixer, i);
        }
        AnimationPlayableOutput.Create(graph, "Animation", animator).SetSourcePlayable(mixer);
        Play(startClip, 0f);
        graph.Play();
    }

    private void OnDestroy()
    {
        if (graph.IsValid()) graph.Destroy();
    }

    // Starts a clip from its beginning, blending in over fade seconds. thenClip, if given, follows once it ends.
    public void Play(string clip, float fade, string thenClip = null, float thenFade = 0.1f)
    {
        int index = IndexOf(clip);
        if (index < 0)
        {
            Debug.LogWarning($"{name} has no animation called {clip}.", this);
            return;
        }
        players[index].SetTime(0);
        current = index;
        fadeSpeed = fade > 0f ? 1f / fade : float.PositiveInfinity;
        following = thenClip != null ? IndexOf(thenClip) : -1;
        followingFade = thenFade;
        if (float.IsPositiveInfinity(fadeSpeed)) Blend(0f);
    }

    private void Update()
    {
        if (current < 0) return;
        // A move that has played to its end hands over to the clip that follows it.
        if (following >= 0 && players[current].GetTime() >= clips[current].length)
        {
            Play(clips[following].name, followingFade);
        }
        Blend(Time.deltaTime);
    }

    private void Blend(float deltaTime)
    {
        float total = 0f;
        bool blending = false;
        for (int i = 0; i < weights.Length; i++)
        {
            float target = i == current ? 1f : 0f;
            weights[i] = float.IsPositiveInfinity(fadeSpeed) ? target : Mathf.MoveTowards(weights[i], target, deltaTime * fadeSpeed);
            blending |= !Mathf.Approximately(weights[i], target);
            total += weights[i];
        }
        // Keep the weights adding up to one, even when a new clip starts halfway through a blend.
        for (int i = 0; i < weights.Length; i++)
            mixer.SetInputWeight(i, total > 0f ? weights[i] / total : 0f);
        IsBlending = blending;
    }

    private int IndexOf(string clip)
    {
        for (int i = 0; i < clips.Length; i++)
            if (clips[i] != null && clips[i].name == clip) return i;
        return -1;
    }
}
