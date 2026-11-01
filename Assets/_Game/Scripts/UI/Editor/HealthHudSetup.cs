using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RushedCrops.Editor
{
    // Builds the player's health HUD inside the player prefab, as its own canvas "HUD_Health":
    //   top-left   a health panel in the same style as the coin counter: heart icon, bar, "5 / 5"
    //   whole view a red screen-edge flash and a floating "-2" when the player is hurt
    // The canvas sorts below the other HUDs, so the flash never covers their text.
    // Safe to run again: it replaces HUD_Health and keeps the rest of the prefab.
    public static class HealthHudSetup
    {
        public const string CanvasName = "HUD_Health";
        private const string PlayerPrefabPath = "Assets/_Game/Prefabs/Player/PFB_Player.prefab";
        private const string VignettePath = "Assets/_Game/Textures/UI/T_DamageVignette.png";

        private static readonly Color PanelColor = new Color(0.08f, 0.07f, 0.05f, 0.72f);
        private static readonly Color TrackColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color RecentDamageColor = new Color(0.95f, 0.62f, 0.28f, 0.9f);
        private static readonly Color HeartColor = new Color(0.92f, 0.26f, 0.24f);
        private static readonly Color FlashColor = new Color(0.85f, 0.04f, 0.02f, 0f);
        private static readonly Color DamageNumberColor = new Color(1f, 0.38f, 0.30f);

        [MenuItem("Rushed Crops/UI/Set up health HUD")]
        public static void SetUp()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Set up the health HUD outside Play.");
            Sprite vignette = VignetteSprite();

            GameObject prefab = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Health health = prefab.GetComponent<Health>();
                if (health == null) throw new InvalidOperationException("The player has no Health. Run Rushed Crops > Player > Set up health and respawn first.");

                Transform old = prefab.transform.Find(CanvasName);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                Build(prefab.transform, health, vignette);
                PrefabUtility.SaveAsPrefabAsset(prefab, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
            Debug.Log("Health HUD set up on the player: health bar and damage flash.");
        }

        private static void Build(Transform player, Health health, Sprite vignette)
        {
            var canvasObject = new GameObject(CanvasName, typeof(RectTransform));
            canvasObject.layer = 5; // UI
            canvasObject.transform.SetParent(player, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -1;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasObject.transform;

            // Screen-edge flash, first so everything else draws over it.
            Image flash = MakeImage("Damage flash", root, vignette, FlashColor);
            Stretch(flash.rectTransform, Vector2.zero, Vector2.zero);
            flash.enabled = false;

            // Health panel, top-left, matching the coin counter in the top-right.
            Image panel = MakeImage("Health", root, BuiltinSprite("UISprite"), PanelColor);
            panel.type = Image.Type.Sliced;
            Place(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -32f), new Vector2(400f, 72f));

            Image heart = MakeImage("HeartIcon", panel.transform, BuiltinSprite("Knob"), HeartColor);
            Place(heart.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(44f, 0f), new Vector2(44f, 44f));

            Image track = MakeImage("Bar", panel.transform, null, TrackColor);
            RectTransform trackRect = track.rectTransform;
            trackRect.anchorMin = new Vector2(0f, 0.5f);
            trackRect.anchorMax = new Vector2(1f, 0.5f);
            trackRect.pivot = new Vector2(0.5f, 0.5f);
            trackRect.offsetMin = new Vector2(84f, -16f);
            trackRect.offsetMax = new Vector2(-20f, 16f);

            Image recent = MakeImage("Recent damage", track.transform, null, RecentDamageColor);
            Stretch(recent.rectTransform, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            Image fill = MakeImage("Fill", track.transform, null, Color.white);
            Stretch(fill.rectTransform, new Vector2(3f, 3f), new Vector2(-3f, -3f));

            Text amount = MakeText("Amount", track.transform, 24, TextAnchor.MiddleCenter, Color.white);
            Stretch(amount.rectTransform, Vector2.zero, Vector2.zero);
            amount.text = "100 / 100";

            Text number = MakeText("Damage number", panel.transform, 40, TextAnchor.MiddleLeft, DamageNumberColor);
            number.fontStyle = FontStyle.Bold;
            Place(number.rectTransform, new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(120f, 50f));
            number.text = "-1";
            number.enabled = false;

            var bar = new SerializedObject(canvasObject.AddComponent<HealthBarHud>());
            bar.FindProperty("health").objectReferenceValue = health;
            bar.FindProperty("fill").objectReferenceValue = fill;
            bar.FindProperty("recentDamage").objectReferenceValue = recent;
            bar.FindProperty("label").objectReferenceValue = amount;
            bar.ApplyModifiedPropertiesWithoutUndo();

            var indicator = new SerializedObject(canvasObject.AddComponent<DamageIndicatorHud>());
            indicator.FindProperty("health").objectReferenceValue = health;
            indicator.FindProperty("flash").objectReferenceValue = flash;
            indicator.FindProperty("damageText").objectReferenceValue = number;
            indicator.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Image MakeImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text MakeText(string name, Transform parent, int size, TextAnchor alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            go.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static Sprite BuiltinSprite(string name)
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/" + name + ".psd");
        }

        // A white square that is clear in the middle and solid at the edges; the Image tints it red.
        private static Sprite VignetteSprite()
        {
            if (!File.Exists(VignettePath))
            {
                const int size = 256;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 fromCentre = new Vector2(x + 0.5f, y + 0.5f) / size * 2f - Vector2.one;
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1.35f, fromCentre.magnitude));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                File.WriteAllBytes(VignettePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(VignettePath);

                var importer = (TextureImporter)AssetImporter.GetAtPath(VignettePath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(VignettePath);
        }
    }
}
