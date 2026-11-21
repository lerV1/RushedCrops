using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RushedCrops.Editor
{
    // Builds the boss health bar inside the player prefab as its own canvas, "HUD_Boss": the boss's name over a
    // wide bar at the top of the screen, in the style of the other HUD panels. Safe to run again.
    public static class BossHudSetup
    {
        public const string CanvasName = "HUD_Boss";
        private const string PlayerPrefabPath = "Assets/_Game/Prefabs/Player/PFB_Player.prefab";
        private static readonly Color PanelColor = new Color(0.08f, 0.07f, 0.05f, 0.72f);
        private static readonly Color TrackColor = new Color(0f, 0f, 0f, 0.55f);

        [MenuItem("Rushed Crops/UI/Set up boss health bar")]
        public static void SetUp()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Set up the boss bar outside Play.");
            GameObject prefab = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Transform old = prefab.transform.Find(CanvasName);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                Build(prefab.transform);
                PrefabUtility.SaveAsPrefabAsset(prefab, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        private static void Build(Transform player)
        {
            var canvasObject = new GameObject(CanvasName, typeof(RectTransform));
            canvasObject.layer = 5; // UI
            canvasObject.transform.SetParent(player, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Image panel = MakeImage("Boss", canvasObject.transform, PanelColor);
            panel.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            panel.type = Image.Type.Sliced;
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -28f);
            panelRect.sizeDelta = new Vector2(760f, 92f);

            Text name = MakeText("Name", panel.transform, 30, TextAnchor.MiddleCenter);
            name.fontStyle = FontStyle.Bold;
            Stretch(name.rectTransform, new Vector2(0f, 0.5f), Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, -4f));
            name.text = "Boss";

            Image track = MakeImage("Bar", panel.transform, TrackColor);
            Stretch(track.rectTransform, Vector2.zero, new Vector2(1f, 0.5f), new Vector2(20f, 12f), new Vector2(-20f, -2f));
            Image fill = MakeImage("Fill", track.transform, Color.white);
            Stretch(fill.rectTransform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            Text amount = MakeText("Amount", track.transform, 22, TextAnchor.MiddleCenter);
            Stretch(amount.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var hud = new SerializedObject(canvasObject.AddComponent<BossHealthHud>());
            hud.FindProperty("panel").objectReferenceValue = panel.gameObject;
            hud.FindProperty("nameText").objectReferenceValue = name;
            hud.FindProperty("fill").objectReferenceValue = fill;
            hud.FindProperty("amount").objectReferenceValue = amount;
            hud.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
        }

        private static Image MakeImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text MakeText(string name, Transform parent, int size, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            go.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
