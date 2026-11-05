using UnityEngine;

// The browser page calls these methods when the mouse is captured or released.
// It installs itself in Web builds, so no scene or prefab setup is needed.
public class WebBridge : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        GameObject bridge = new GameObject("WebBridge");
        DontDestroyOnLoad(bridge);
        bridge.AddComponent<WebBridge>().Pause();
        Application.targetFrameRate = 60;
    }
#endif

    [UnityEngine.Scripting.Preserve]
    public void Resume()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLInput.captureAllKeyboardInput = true;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
#endif
    }

    [UnityEngine.Scripting.Preserve]
    public void Pause()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Time.timeScale = 0f;
        AudioListener.pause = true;
        WebGLInput.captureAllKeyboardInput = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
#endif
    }
}
