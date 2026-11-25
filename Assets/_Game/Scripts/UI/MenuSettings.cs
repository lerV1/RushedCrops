using UnityEngine;

/// <summary>Applies the menu's settings when either scene starts.</summary>
public static class MenuSettings
{
    private const string VolumeKey = "RushedCrops.Settings.Volume";
    private const string FullscreenKey = "RushedCrops.Settings.Fullscreen";

    public static float Volume => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        AudioListener.volume = Volume;
#if !UNITY_EDITOR && !UNITY_WEBGL
        if (PlayerPrefs.HasKey(FullscreenKey)) Screen.fullScreen = PlayerPrefs.GetInt(FullscreenKey) == 1;
#endif
    }

    /// <summary>Sets and remembers the master volume for every scene.</summary>
    public static void SetVolume(float value)
    {
        value = Mathf.Clamp01(value);
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(VolumeKey, value);
    }

    /// <summary>Sets and remembers fullscreen mode on standalone players.</summary>
    public static void SetFullscreen(bool value)
    {
        Screen.fullScreen = value;
        PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
    }
}
