using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace RushedCrops.Editor
{
    // Makes a picture of every item from its 3D model, so the hotbar can
    // show what an item looks like instead of only its name. Each model is rendered alone in an empty
    // scene, against a see-through background, and saved as a sprite in Textures/UI/Icons.
    // Seeds show the seed sack with a small picture of their crop in the corner. DA_ItemIcons lists the results.
    // Run it again after adding an item or changing a model.
    public static class ItemIconRenderer
    {
        private const string IconFolder = "Assets/_Game/Textures/UI/Icons";
        private const string IconsAsset = "Assets/_Game/Data/DA_ItemIcons.asset";
        internal const string SeedSack = "Assets/_Game/Models/Farm/SeedSack.fbx";
        private const string Pack = "Assets/Holotna/Medieval Rural/Prefabs/";
        private const int Size = 128;

        // The model for each item, how to turn it so it reads well in a small square, and how much of
        // it to show: 1 fits the whole model, less zooms in on its top (the hoe's long handle would
        // otherwise shrink the blade to a few pixels).
        internal static readonly (ResourceType item, string model, Vector3 turn, float zoom)[] Models =
        {
            (ResourceType.Carrots, "Assets/_Game/Prefabs/Crops/PFB_Carrot01.prefab", new Vector3(0f, 60f, 25f), 1f),
            (ResourceType.Tomatoes, "Assets/_Game/Models/Crops/Tomato01.fbx", new Vector3(0f, 0f, 0f), 1f),
            (ResourceType.Beets, "Assets/_Game/Prefabs/Crops/PFB_Beet01.prefab", new Vector3(0f, 30f, 0f), 1f),
            (ResourceType.Cabbages, "Assets/_Game/Prefabs/Crops/PFB_Cabbage01.prefab", new Vector3(0f, 30f, 0f), 1f),
            (ResourceType.Onions, "Assets/_Game/Prefabs/Crops/PFB_Onion01.prefab", new Vector3(0f, 30f, 0f), 1f),
            (ResourceType.Wheat, "Assets/_Game/Prefabs/Crops/PFB_Wheat01.prefab", new Vector3(0f, 30f, 20f), 1f),
            (ResourceType.Wood, Pack + "Wood/Log01.prefab", new Vector3(0f, 35f, 0f), 1f),
            (ResourceType.Stone, Pack + "Rocks/Stone01.prefab", new Vector3(0f, 30f, 0f), 1f),
            (ResourceType.Hoe, "Assets/_Game/Prefabs/Tools/PFB_Hoe01.prefab", new Vector3(0f, 90f, -40f), 0.6f),
            (ResourceType.WateringCan, "Assets/_Game/Models/Tools/WateringCan.fbx", new Vector3(0f, -60f, 0f), 1f),
            (ResourceType.Hatchet, "Assets/_Game/Prefabs/Tools/PFB_Hatchet01.prefab", new Vector3(0f, 90f, -40f), 1f),
        };

        // Seeds show the sack with the crop they grow into.
        internal static readonly (ResourceType seeds, ResourceType crop)[] Seeds =
        {
            (ResourceType.CarrotSeeds, ResourceType.Carrots), (ResourceType.TomatoSeeds, ResourceType.Tomatoes),
            (ResourceType.BeetSeeds, ResourceType.Beets), (ResourceType.CabbageSeeds, ResourceType.Cabbages),
            (ResourceType.OnionSeeds, ResourceType.Onions), (ResourceType.WheatSeeds, ResourceType.Wheat),
        };

        [MenuItem("Rushed Crops/UI/Render item icons")]
        public static void RenderAll()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Render the icons outside Play.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previousScene = EditorSceneManager.GetActiveScene().path;
            Directory.CreateDirectory(IconFolder);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = SetUpStudio();
            var pictures = new List<(ResourceType item, Texture2D picture)>();
            foreach (var (item, model, turn, zoom) in Models)
                pictures.Add((item, Shoot(camera, zoom, () => Place(model, turn, 1f))));
            // Every seed shows the same sack, told apart by a small picture of its crop in the corner.
            Texture2D sack = Shoot(camera, 1f, () => Place(SeedSack, new Vector3(0f, 20f, 0f), 0f));
            foreach (var (seeds, crop) in Seeds)
                pictures.Add((seeds, WithBadge(sack, pictures.Find(p => p.item == crop).picture)));
            UnityEngine.Object.DestroyImmediate(sack);

            var made = new List<(ResourceType, string)>();
            foreach (var (item, picture) in pictures)
            {
                string file = $"{IconFolder}/ICON_{item}.png";
                File.WriteAllBytes(file, picture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(picture);
                made.Add((item, file));
            }

            AssetDatabase.Refresh();
            var icons = AssetDatabase.LoadAssetAtPath<ItemIcons>(IconsAsset);
            if (icons == null)
            {
                icons = ScriptableObject.CreateInstance<ItemIcons>();
                AssetDatabase.CreateAsset(icons, IconsAsset);
            }
            var settings = new SerializedObject(icons);
            SerializedProperty list = settings.FindProperty("icons");
            list.arraySize = made.Count;
            for (int i = 0; i < made.Count; i++)
            {
                string path = made[i].Item2;
                MakeSprite(path);
                list.GetArrayElementAtIndex(i).FindPropertyRelative("item").enumValueIndex = (int)made[i].Item1;
                list.GetArrayElementAtIndex(i).FindPropertyRelative("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            if (!string.IsNullOrEmpty(previousScene)) EditorSceneManager.OpenScene(previousScene);
            Debug.Log($"Rendered {made.Count} item icons into {IconFolder}.");
        }

        // A camera with a see-through background, a key light and soft fill, in the empty scene.
        private static Camera SetUpStudio()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.66f);
            var light = new GameObject("Key light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            var camera = new GameObject("Icon camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.fieldOfView = 22f;
            camera.nearClipPlane = 0.01f;
            var urp = camera.GetUniversalAdditionalCameraData();
            urp.renderPostProcessing = false; // keeps the background see-through
            urp.antialiasing = AntialiasingMode.None;
            return camera;
        }

        // Builds a model, takes its picture and removes it again.
        private static Texture2D Shoot(Camera camera, float zoom, Func<GameObject> build)
        {
            GameObject subject = build();
            Bounds bounds = BoundsOf(subject);
            // Look from the front, a little above and to the right, far enough to fit the whole model.
            // Zooming in moves closer and aims toward the model's highest point, so a tool's head stays in the picture.
            Vector3 view = new Vector3(0.45f, 0.5f, 1f).normalized;
            float distance = bounds.extents.magnitude / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.92f * zoom;
            Vector3 aim = zoom < 1f ? Vector3.Lerp(bounds.center, HighestPoint(subject), (1f - zoom) * 1.4f) : bounds.center;
            camera.transform.position = aim + view * distance;
            camera.transform.LookAt(aim);
            camera.farClipPlane = distance * 3f;

            var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.Render();
            camera.Render(); // the first batch render can miss textures
            RenderTexture.active = target;
            var picture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            picture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            picture.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(subject);
            return picture;
        }

        // A copy of the sack's picture with a smaller copy of the crop's picture over its bottom-left corner
        // (the hotbar writes the count in the bottom-right).
        private static Texture2D WithBadge(Texture2D sack, Texture2D crop)
        {
            const float BadgeShare = 0.55f; // how much of the picture's width the crop covers
            int side = Mathf.RoundToInt(Size * BadgeShare);
            var result = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Color under = sack.GetPixel(x, y);
                    // Texture rows start at the bottom, so y < side is the bottom of the picture.
                    Color over = x < side && y < side
                        ? crop.GetPixelBilinear((x + 0.5f) / side, (y + 0.5f) / side)
                        : Color.clear;
                    // Lay the crop over the sack, letting the sack show where the crop is see-through.
                    float alpha = over.a + under.a * (1f - over.a);
                    Color mixed = alpha > 0f ? (over * over.a + under * under.a * (1f - over.a)) / alpha : Color.clear;
                    mixed.a = alpha;
                    result.SetPixel(x, y, mixed);
                }
            }
            result.Apply();
            return result;
        }

        private static GameObject Place(string model, Vector3 turn, float fit)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(model);
            if (source == null) throw new InvalidOperationException("Missing model for an icon: " + model);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(source);
            go.transform.rotation = Quaternion.Euler(turn) * go.transform.rotation;
            // Show only the most detailed version of models that switch detail with distance.
            foreach (LODGroup lod in go.GetComponentsInChildren<LODGroup>(true)) lod.enabled = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                r.enabled = !r.name.Contains("_LOD") || r.name.EndsWith("_LOD0");
            if (fit > 0f) FitInside(go, fit);
            return go;
        }

        // Scales an object so its biggest side is this many metres.
        private static void FitInside(GameObject go, float size)
        {
            Bounds b = BoundsOf(go);
            float biggest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (biggest > 0f) go.transform.localScale *= size / biggest;
        }

        // The highest corner of the visible meshes, such as the tip of a hoe's blade.
        private static Vector3 HighestPoint(GameObject go)
        {
            Vector3 best = go.transform.position;
            float bestY = float.NegativeInfinity;
            foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<Renderer>();
                if (filter.sharedMesh == null || renderer == null || !renderer.enabled) continue;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 world = filter.transform.TransformPoint(vertex);
                    if (world.y > bestY) { bestY = world.y; best = world; }
                }
            }
            return best;
        }

        private static Bounds BoundsOf(GameObject go)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            bool first = true;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        private static void MakeSprite(string path)
        {
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        // ---------- Hotbar ----------

        // Gives every hotbar slot an icon image behind its count and links DA_ItemIcons to the HUD.
        [MenuItem("Rushed Crops/UI/Add item icons to the hotbar")]
        public static void AddIconsToHotbar()
        {
            var hud = UnityEngine.Object.FindAnyObjectByType<InventoryHud>(FindObjectsInactive.Include);
            if (hud == null) throw new InvalidOperationException("Open SC_Farm: it has no InventoryHud.");
            var settings = new SerializedObject(hud);
            settings.FindProperty("icons").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemIcons>(IconsAsset);
            SerializedProperty slots = settings.FindProperty("slots");
            for (int i = 0; i < slots.arraySize; i++)
            {
                var background = slots.GetArrayElementAtIndex(i).FindPropertyRelative("background").objectReferenceValue as Image;
                if (background == null) continue;
                Transform old = background.transform.Find("Icon");
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

                var rt = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                Undo.RegisterCreatedObjectUndo(rt.gameObject, "Add hotbar icon");
                rt.SetParent(background.transform, false);
                rt.SetAsFirstSibling(); // behind the count
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(10f, 10f);
                rt.offsetMax = new Vector2(-10f, -10f);
                var image = rt.GetComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.enabled = false;
                slots.GetArrayElementAtIndex(i).FindPropertyRelative("icon").objectReferenceValue = image;
            }
            settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            Debug.Log("Hotbar slots now show item icons.");
        }
    }
}
