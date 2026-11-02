using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RushedCrops.Editor
{
    // Puts the merchant behind the market stall counter, builds his trading window (HUD_Shop) and
    // makes sure the scene has an EventSystem so the window's buttons can be clicked.
    // The map builder places the merchant with the stall, and this can be run again from the menu.
    public static class MerchantSetup
    {
        public const string MerchantName = "Merchant";
        public const string ShopName = "HUD_Shop";
        private const string MerchantPrefab = "Assets/_Game/Prefabs/Characters/PFB_Merchant.prefab";
        private static readonly Vector3 BehindCounter = new Vector3(4f, 0f, -13.55f); // between the two crates
        private const int GroundLayer = 6;

        private static readonly Color PanelColour = new Color(0.08f, 0.06f, 0.05f, 0.97f);
        private static readonly Color Gold = new Color(0.96f, 0.80f, 0.40f);
        private static readonly Color ButtonColour = new Color(0.36f, 0.24f, 0.13f);

        [MenuItem("Rushed Crops/Market/Set up merchant")]
        public static void SetUpInScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Set up the merchant outside Play.");
            var market = GameObject.Find(FarmWorldBuilder.RootName)?.transform.Find("04_Market");
            if (market == null) throw new InvalidOperationException("Open SC_Farm with the farm map built first.");

            EnsureEventSystem();
            BuildShopWindow();
            PlaceMerchant(market);
            EditorSceneManager.MarkSceneDirty(market.gameObject.scene);
            Debug.Log("Merchant and trading window set up.");
        }

        // Places (or replaces) the merchant and links him to the Inventory, the day cycle and the window.
        public static GameObject PlaceMerchant(Transform market)
        {
            Transform old = market.Find(MerchantName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var merchant = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MerchantPrefab), market);
            Undo.RegisterCreatedObjectUndo(merchant, "Place merchant");
            merchant.name = MerchantName;
            merchant.transform.position = Ground(BehindCounter);
            merchant.transform.rotation = Quaternion.identity; // facing north, over the counter to the plaza

            var settings = new SerializedObject(merchant.GetComponent<Merchant>());
            settings.FindProperty("inventory").objectReferenceValue = UnityEngine.Object.FindAnyObjectByType<Inventory>();
            settings.FindProperty("dayCycle").objectReferenceValue = UnityEngine.Object.FindAnyObjectByType<EnvironmentCycle>();
            var shop = UnityEngine.Object.FindAnyObjectByType<MerchantShopUI>(FindObjectsInactive.Include);
            settings.FindProperty("shop").objectReferenceValue = shop;
            settings.ApplyModifiedProperties();

            // He watches and greets the player, and talks in the window while it is open.
            var personality = merchant.GetComponent<MerchantPersonality>();
            if (personality != null)
            {
                var mood = new SerializedObject(personality);
                mood.FindProperty("player").objectReferenceValue = GameObject.Find("Player")?.transform;
                mood.FindProperty("shop").objectReferenceValue = shop;
                mood.ApplyModifiedProperties();
            }
            return merchant;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(go, "Add EventSystem");
        }

        // ---------- The trading window ----------

        private static void BuildShopWindow()
        {
            var old = GameObject.Find(ShopName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject(ShopName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(root, "Build shop window");
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10; // above the HUDs
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = Box("Panel", root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 760), PanelColour);
            var title = Label("Title", panel, new Vector2(0.5f, 1f), new Vector2(0, -44), new Vector2(1100, 56), 36, Gold, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            var coins = Label("Coins", panel, new Vector2(1f, 1f), new Vector2(-170, -44), new Vector2(300, 40), 26, Gold, TextAnchor.MiddleRight);
            var remark = Label("Remark", panel, new Vector2(0.5f, 1f), new Vector2(0, -88), new Vector2(1100, 32), 22, new Color(0.90f, 0.85f, 0.75f), TextAnchor.MiddleCenter);
            remark.fontStyle = FontStyle.Italic;
            Label("Sell header", panel, new Vector2(0.5f, 1f), new Vector2(-290, -128), new Vector2(540, 36), 26, Color.white, TextAnchor.MiddleLeft).text = "Sell your crops";
            Label("Buy header", panel, new Vector2(0.5f, 1f), new Vector2(290, -128), new Vector2(540, 36), 26, Color.white, TextAnchor.MiddleLeft).text = "Buy seeds and supplies";
            var sellList = List("Sell list", panel, new Vector2(-290, -156));
            var buyList = List("Buy list", panel, new Vector2(290, -156));
            var message = Label("Message", panel, new Vector2(0.5f, 0f), new Vector2(0, 92), new Vector2(1100, 40), 24, Gold, TextAnchor.MiddleCenter);
            var close = MakeButton("Close", panel, new Vector2(1f, 0f), new Vector2(-130, 44), new Vector2(200, 50), "Close (Esc)");
            var template = Row(panel);

            var shop = root.AddComponent<MerchantShopUI>();
            var settings = new SerializedObject(shop);
            settings.FindProperty("panel").objectReferenceValue = panel.gameObject;
            settings.FindProperty("title").objectReferenceValue = title;
            settings.FindProperty("coins").objectReferenceValue = coins;
            settings.FindProperty("message").objectReferenceValue = message;
            settings.FindProperty("remark").objectReferenceValue = remark;
            settings.FindProperty("closeButton").objectReferenceValue = close;
            settings.FindProperty("sellList").objectReferenceValue = sellList;
            settings.FindProperty("buyList").objectReferenceValue = buyList;
            settings.FindProperty("rowTemplate").objectReferenceValue = template;

            // Everything that reads the player's input stops while the window is open.
            var player = GameObject.Find("Player");
            var paused = new List<Behaviour>();
            if (player != null)
            {
                foreach (Type type in new[] { typeof(PlayerController), typeof(PlayerLook), typeof(PlayerInteractor), typeof(PlayerTools), typeof(MeleeAttack), typeof(HandAnimator) })
                {
                    var behaviour = player.GetComponentInChildren(type, true) as Behaviour;
                    if (behaviour != null) paused.Add(behaviour);
                }
            }
            SerializedProperty list = settings.FindProperty("pauseWhileOpen");
            list.arraySize = paused.Count;
            for (int i = 0; i < paused.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = paused[i];
            settings.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
        }

        private static GameObject Row(RectTransform parent)
        {
            var row = new GameObject("Row template", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            row.sizeDelta = new Vector2(540, 50);
            row.GetComponent<LayoutElement>().preferredHeight = 50;
            // Name 0-160, info 170-340, two buttons 346-438 and 444-536.
            Label("Name", row, new Vector2(0f, 0.5f), new Vector2(80, 0), new Vector2(160, 46), 23, Color.white, TextAnchor.MiddleLeft);
            Label("Info", row, new Vector2(0f, 0.5f), new Vector2(255, 0), new Vector2(170, 46), 18, new Color(0.85f, 0.82f, 0.75f), TextAnchor.MiddleLeft);
            MakeButton("Button1", row, new Vector2(0f, 0.5f), new Vector2(392, 0), new Vector2(92, 42), "Buy 1");
            MakeButton("Button2", row, new Vector2(0f, 0.5f), new Vector2(490, 0), new Vector2(92, 42), "Buy 5");
            row.gameObject.SetActive(false);
            return row.gameObject;
        }

        private static RectTransform List(string name, RectTransform parent, Vector2 topCentre)
        {
            var rt = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = topCentre;
            rt.sizeDelta = new Vector2(540, 470);
            var layout = rt.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rt;
        }

        private static RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color colour)
        {
            var rt = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var image = rt.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = colour;
            return rt;
        }

        private static Text Label(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, Color colour, TextAnchor align)
        {
            var rt = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var text = rt.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = colour;
            text.alignment = align;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }

        private static Button MakeButton(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string caption)
        {
            var rt = Box(name, parent, anchor, pos, size, Color.white);
            var button = rt.gameObject.AddComponent<Button>();
            var colours = button.colors;
            colours.normalColor = ButtonColour;
            colours.highlightedColor = new Color(0.52f, 0.36f, 0.18f);
            colours.selectedColor = new Color(0.52f, 0.36f, 0.18f);
            colours.pressedColor = new Color(0.25f, 0.17f, 0.09f);
            colours.disabledColor = new Color(0.22f, 0.20f, 0.18f, 0.7f);
            button.colors = colours;
            Label("Text", rt, new Vector2(0.5f, 0.5f), Vector2.zero, size, 20, Color.white, TextAnchor.MiddleCenter).text = caption;
            return button;
        }

        private static Vector3 Ground(Vector3 at)
        {
            return Physics.Raycast(at + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 40f, 1 << GroundLayer) ? hit.point : at;
        }
    }
}
