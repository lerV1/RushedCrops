using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Rebuilds the small title scene and its overhead photograph from the current farm.</summary>
public static class MainMenuSceneBuilder
{
    private const string FarmPath = "Assets/_Game/Scenes/SC_Farm.unity";
    private const string MenuPath = "Assets/_Game/Scenes/SC_Menu.unity";
    private const string ImagePath = "Assets/_Game/Textures/UI/MenuFarm.png";
    private static readonly Color Cream = new Color(0.98f, 0.94f, 0.81f);
    private static readonly Color Green = new Color(0.17f, 0.28f, 0.20f);
    private static Font font;

    [MenuItem("Rushed Crops/Menu/Rebuild Menu Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
        if (Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
            .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
            throw new System.InvalidOperationException("Save open scene changes before rebuilding the menu.");

        EditorSceneManager.OpenScene(FarmPath);
        CaptureFarm();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Menu Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Green;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        var sun = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var canvas = new GameObject("Menu Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var background = Rect("Overhead farm", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var raw = background.gameObject.AddComponent<RawImage>();
        raw.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath);
        raw.raycastTarget = false;
        var aspect = background.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        aspect.aspectRatio = 16f / 9f;
        var shade = Rect("Shade", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        shade.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.05f, 0.025f, 0.48f);

        var card = Rect("Menu options", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(420f, 420f));
        var menu = canvas.AddComponent<MainMenu>();
        var home = Panel("Home", card);
        var settings = Panel("Settings", card);
        var reset = Panel("New Game Confirmation", card);

        var play = Button(home, "Play", 65f, menu.Play);
        Button(home, "New Game", 145f, menu.NewGame);
        Button(home, "Settings", 225f, menu.ShowSettings);
        Button(home, "Quit", 305f, menu.Quit);

        Label(settings, "Settings", 36, 10f, 55f, FontStyle.Bold);
        Label(settings, "Volume", 23, 110f, 30f);
        var value = Label(settings, "100%", 19, 150f, 24f);
        var slider = CreateSlider(settings, 210f);
        UnityEventTools.AddPersistentListener(slider.onValueChanged, menu.ChangeVolume);
        var toggle = CreateToggle(settings, 260f);
        UnityEventTools.AddPersistentListener(toggle.onValueChanged, menu.ChangeFullscreen);
        var fullscreenHint = Label(settings, "", 13, 310f, 24f);
        Button(settings, "Back", 350f, menu.ShowHome);

        Label(reset, "Delete saved progress?", 30, 80f, 60f, FontStyle.Bold);
        Button(reset, "New Game", 200f, menu.ConfirmNewGame);
        var cancel = Button(reset, "Back", 280f, menu.ShowHome);
        var status = Label(canvas.transform, "", 19, 664f, 36f);

        var serialized = new SerializedObject(menu);
        Assign(serialized, "homePanel", home.gameObject);
        Assign(serialized, "settingsPanel", settings.gameObject);
        Assign(serialized, "newGamePanel", reset.gameObject);
        Assign(serialized, "playButton", play);
        Assign(serialized, "cancelResetButton", cancel);
        Assign(serialized, "status", status);
        Assign(serialized, "volumeSlider", slider);
        Assign(serialized, "volumeValue", value);
        Assign(serialized, "fullscreenToggle", toggle);
        Assign(serialized, "fullscreenHint", fullscreenHint);
        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/_Game/Input/IA_PlayerControls.inputactions");
        var references = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(actions))
            .OfType<InputActionReference>().ToArray();
        Assign(serialized, "backAction", references.First(r => r.action.id == actions.FindAction("UI/Cancel", true).id));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        settings.gameObject.SetActive(false);
        reset.gameObject.SetActive(false);
        var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var input = events.GetComponent<InputSystemUIInputModule>();
        input.actionsAsset = actions;
        input.point = references.First(r => r.action.name == "Point");
        input.leftClick = references.First(r => r.action.name == "Click");
        input.rightClick = references.First(r => r.action.name == "RightClick");
        input.middleClick = references.First(r => r.action.name == "MiddleClick");
        input.scrollWheel = references.First(r => r.action.name == "ScrollWheel");
        input.move = references.First(r => r.action.name == "Navigate");
        input.submit = references.First(r => r.action.name == "Submit");
        input.cancel = references.First(r => r.action.name == "Cancel");
        input.trackedDevicePosition = references.First(r => r.action.name == "TrackedDevicePosition");
        input.trackedDeviceOrientation = references.First(r => r.action.name == "TrackedDeviceOrientation");
        events.GetComponent<EventSystem>().firstSelectedGameObject = play.gameObject;
        EditorSceneManager.SaveScene(scene, MenuPath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MenuPath, true),
            new EditorBuildSettingsScene(FarmPath, true) }
            .Concat(EditorBuildSettings.scenes.Where(s => s.path != MenuPath && s.path != FarmPath)).ToArray();
        AssetDatabase.SaveAssets();
    }

    private static void CaptureFarm()
    {
        var camera = new GameObject("Temporary overhead capture", typeof(Camera)).GetComponent<Camera>();
        camera.transform.position = new Vector3(-45f, 125f, -68f);
        camera.transform.rotation = Quaternion.Euler(65f, 0f, 0f);
        camera.orthographic = true;
        camera.orthographicSize = 49f;
        camera.farClipPlane = 400f;
        camera.clearFlags = CameraClearFlags.Skybox;
        var target = new RenderTexture(1920, 1080, 24);
        var previous = RenderTexture.active;
        bool fog = RenderSettings.fog;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            RenderSettings.fog = false;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new UnityEngine.Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            File.WriteAllBytes(ImagePath, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderSettings.fog = fog;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(camera.gameObject);
        }
        AssetDatabase.ImportAsset(ImagePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(ImagePath);
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    private static void Assign(SerializedObject target, string field, Object value) =>
        target.FindProperty(field).objectReferenceValue = value;

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static RectTransform Panel(string name, Transform parent) =>
        Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

    private static Text Label(Transform parent, string words, int size, float top, float height, FontStyle style = FontStyle.Normal)
    {
        var rect = Rect(words.Length > 0 ? words.Replace('\n', ' ') : "Status", parent,
            new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -top - height / 2f), new Vector2(-48f, height));
        var text = rect.gameObject.AddComponent<Text>();
        var shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        shadow.effectDistance = new Vector2(1f, -2f);
        text.font = font;
        text.text = words;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Cream;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        return text;
    }

    private static Button Button(Transform parent, string title, float top, UnityAction click)
    {
        var rect = Rect(title + " Button", parent, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0f, -top - 27f), new Vector2(-56f, 54f));
        var image = rect.gameObject.AddComponent<Image>();
        image.color = Color.clear;
        var button = rect.gameObject.AddComponent<Button>();
        var text = Label(rect, title, 34, 0f, 54f, FontStyle.Bold);
        button.targetGraphic = text;
        var colors = button.colors;
        colors.normalColor = Cream;
        colors.highlightedColor = new Color(1f, 0.79f, 0.37f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.82f, 0.59f, 0.23f);
        colors.fadeDuration = 0.12f;
        button.colors = colors;
        UnityEventTools.AddPersistentListener(button.onClick, click);
        return button;
    }

    private static Slider CreateSlider(Transform parent, float top)
    {
        var root = Rect("Master volume slider", parent, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0, -top), new Vector2(-72, 36));
        var track = Rect("Track", root, new Vector2(0f, .4f), new Vector2(1f, .6f), Vector2.zero, Vector2.zero);
        track.gameObject.AddComponent<Image>().color = Green;
        var area = Rect("Handle area", root, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-24, 0));
        var handle = Rect("Handle", area, new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.zero, new Vector2(24, 36));
        var image = handle.gameObject.AddComponent<Image>();
        image.color = Cream;
        var slider = root.gameObject.AddComponent<Slider>();
        slider.handleRect = handle;
        slider.targetGraphic = image;
        slider.minValue = 0;
        slider.maxValue = 1;
        slider.value = 1;
        return slider;
    }

    private static Toggle CreateToggle(Transform parent, float top)
    {
        var root = Rect("Fullscreen", parent, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0f, -top - 20f), new Vector2(-64, 40));
        var box = Rect("Box", root, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(18, 0), new Vector2(32, 32));
        var image = box.gameObject.AddComponent<Image>();
        image.color = Cream;
        var check = Rect("Check", box, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(18, 18));
        var mark = check.gameObject.AddComponent<Image>();
        mark.color = Green;
        var toggle = root.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = image;
        toggle.graphic = mark;
        var text = Label(root, "Fullscreen", 19, 0, 40);
        text.rectTransform.offsetMin = new Vector2(50, text.rectTransform.offsetMin.y);
        text.alignment = TextAnchor.MiddleLeft;
        return toggle;
    }
}
