using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

// The browser page calls these methods when the mouse is captured or released.
// It installs itself in Web builds, so no scene or prefab setup is needed.
public class WebBridge : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void RushedCropsSetMenu(int isMenu);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        GameObject bridge = new GameObject("WebBridge");
        DontDestroyOnLoad(bridge);
        bridge.AddComponent<WebBridge>().Pause();
        Application.targetFrameRate = 60;
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // The title needs a free pointer; the farm uses the existing capture overlay.
        RushedCropsSetMenu(scene.name == "SC_Menu" ? 1 : 0);
    }
#endif

    [UnityEngine.Scripting.Preserve]
    public void Resume()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLInput.captureAllKeyboardInput = true;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        bool inMenu = SceneManager.GetActiveScene().name == "SC_Menu";
        Cursor.lockState = inMenu ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = inMenu;
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
