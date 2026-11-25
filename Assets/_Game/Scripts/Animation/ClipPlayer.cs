using UnityEngine;

// Plays clips by name through an Animator Controller. Each state in the controller
// uses the same name as its AnimationClip.
[RequireComponent(typeof(Animator))]
public class ClipPlayer : MonoBehaviour
{
    [Tooltip("Every clip this model can play, taken from its FBX.")]
    [SerializeField] private AnimationClip[] clips = new AnimationClip[0];
    [Tooltip("The looping clip it starts in.")]
    [SerializeField] private string startClip = "Idle";

    private Animator animator;
    private int current = -1;
    private int following = -1;
    private float clipEndsAt;
    private float followingFade;
    private float blendEndsAt;

    // The clip playing now (or being blended to).
    public string Current => current >= 0 ? clips[current].name : null;
    // True while one clip is still blending into the next.
    public bool IsBlending { get; private set; }
    public AnimationClip[] Clips => clips;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        Play(startClip, 0f);
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

        bool sameClip = current == index;
        bool wasBlending = IsBlending;
        current = index;
        following = thenClip != null ? IndexOf(thenClip) : -1;
        followingFade = thenFade;
        clipEndsAt = Time.time + clips[index].length;

        if (fade > 0f)
        {
            animator.CrossFadeInFixedTime(clip, fade, 0, 0f);
            IsBlending = !sameClip || wasBlending;
            blendEndsAt = Time.time + fade;
        }
        else
        {
            animator.Play(clip, 0, 0f);
            IsBlending = false;
            blendEndsAt = Time.time;
        }
    }

    private void Update()
    {
        if (current < 0)
        {
            return;
        }

        if (IsBlending && Time.time >= blendEndsAt)
        {
            IsBlending = false;
        }

        if (following >= 0 && Time.time >= clipEndsAt)
        {
            int next = following;
            float fade = followingFade;
            following = -1;
            Play(clips[next].name, fade);
        }
    }

    private int IndexOf(string clip)
    {
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && clips[i].name == clip)
            {
                return i;
            }
        }
        return -1;
    }
}
