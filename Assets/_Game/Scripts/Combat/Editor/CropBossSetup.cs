using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RushedCrops.Editor
{
    // Puts the crop boss (a giant angry carrot, modelled in Blender) just outside the east
    // fence of the crop field, looking at the soil. It stands still, and killing it throws
    // out a bag of seeds for every crop.
    // Also makes its flat cartoon materials and the seed bag prefab it drops.
    // The map builder calls Place, and it can be run again from the menu.
    public static class CropBossSetup
    {
        public const string RootName = "Crop boss";
        public const string ModelPath = "Assets/_Game/Models/Boss/CropBoss.fbx";
        public const string SeedBagPath = "Assets/_Game/Prefabs/Boss/PFB_SeedBag.prefab";
        private const string MaterialFolder = "Assets/_Game/Materials/Boss";
        private const string Cartoon = "Assets/_Game/Materials/Cartoon/";

        // Open grass east of the garden fence, halfway along it.
        private static readonly Vector3 Spot = new Vector3(-10.5f, 0f, -31f);
        // Turns the model's face (+Z) west, toward the crop field.
        private const float FacingYaw = -90f;
        private const int MaxHealth = 75; // 3 hatchet hits
        private const int SeedsPerCrop = 3;

        // The Blender material slots and their colours.
        private static readonly Dictionary<string, Color> Colours = new Dictionary<string, Color>
        {
            { "Boss_Orange", new Color(0.95f, 0.45f, 0.1f) },
            { "Boss_OrangeDark", new Color(0.7f, 0.28f, 0.06f) },
            { "Boss_Leaf", new Color(0.35f, 0.7f, 0.2f) },
            { "Boss_LeafDark", new Color(0.18f, 0.48f, 0.12f) },
            { "Boss_EyeWhite", new Color(0.97f, 0.95f, 0.86f) },
            { "Boss_Pupil", new Color(0.75f, 0.08f, 0.05f) },
            { "Boss_Dark", new Color(0.12f, 0.07f, 0.05f) },
            { "Boss_Dirt", new Color(0.4f, 0.26f, 0.14f) },
        };

        [MenuItem("Rushed Crops/Combat/Place crop boss")]
        public static void PlaceInScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Place the boss outside Play.");
            var farm = GameObject.Find(FarmWorldBuilder.RootName)?.transform.Find(FarmWorldBuilder.FarmGroup);
            if (farm == null) throw new InvalidOperationException("Open SC_Farm with the farm map built first.");
            Place(farm);
            EditorSceneManager.MarkSceneDirty(farm.gameObject.scene);
            Debug.Log("Placed the crop boss beside the crop field.");
        }

        public static GameObject Place(Transform parent)
        {
            Transform old = parent.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            SetUpModelMaterials();
            SeedDrop bag = SeedBag();
            var inventory = UnityEngine.Object.FindAnyObjectByType<Inventory>();
            if (inventory == null) Debug.LogWarning("No Inventory in the scene yet. Assign it to the CropBoss.");

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Place crop boss");
            root.transform.SetParent(parent, false);
            root.transform.position = ToolStumpSetup.Ground(Spot);
            root.transform.rotation = Quaternion.Euler(0f, FacingYaw, 0f);

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (asset == null) throw new InvalidOperationException("Missing model " + ModelPath);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // The carrot's body and the mound it is planted in. No Rigidbody: it never moves.
            var body = root.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0f, 1.9f, 0f);
            body.radius = 0.95f;
            body.height = 3.4f;
            var mound = root.AddComponent<BoxCollider>();
            mound.center = new Vector3(0f, 0.15f, 0f);
            mound.size = new Vector3(1.8f, 0.3f, 1.8f);

            var health = root.AddComponent<Health>();
            var healthSettings = new SerializedObject(health);
            healthSettings.FindProperty("maxHealth").intValue = MaxHealth;
            healthSettings.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<HitFlash>();

            var boss = root.AddComponent<CropBoss>();
            var settings = new SerializedObject(boss);
            settings.FindProperty("model").objectReferenceValue = model.transform;
            settings.FindProperty("inventory").objectReferenceValue = inventory;
            settings.FindProperty("seedBagPrefab").objectReferenceValue = bag;
            SerializedProperty drops = settings.FindProperty("drops");
            drops.arraySize = FarmPlotSetup.CropAssets.Length;
            for (int i = 0; i < FarmPlotSetup.CropAssets.Length; i++)
            {
                var crop = AssetDatabase.LoadAssetAtPath<CropData>(FarmPlotSetup.CropAssets[i]);
                if (crop == null) throw new InvalidOperationException("Missing crop asset: " + FarmPlotSetup.CropAssets[i]);
                SerializedProperty entry = drops.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("crop").objectReferenceValue = crop;
                entry.FindPropertyRelative("amount").intValue = SeedsPerCrop;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // Gives the FBX the project's flat cartoon look: one material per Blender slot,
        // made from the Straw material so it uses the same shader.
        public static void SetUpModelMaterials()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing model " + ModelPath);
            var template = AssetDatabase.LoadAssetAtPath<Material>(Cartoon + "Straw.mat");
            if (template == null) throw new InvalidOperationException("Missing material " + Cartoon + "Straw.mat");
            EnsureFolder(MaterialFolder);

            bool changed = false;
            foreach (var pair in Colours)
            {
                string path = $"{MaterialFolder}/{pair.Key}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(template) { name = pair.Key };
                    AssetDatabase.CreateAsset(material, path);
                }
                material.SetColor("_BaseColor", pair.Value);
                if (material.HasProperty("_Color")) material.SetColor("_Color", pair.Value);
                EditorUtility.SetDirty(material);

                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key);
                if (!importer.GetExternalObjectMap().TryGetValue(id, out UnityEngine.Object mapped) || mapped != material)
                {
                    importer.AddRemap(id, material);
                    changed = true;
                }
            }
            if (importer.importAnimation || importer.importCameras || importer.importLights)
            {
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
        }

        // A small tied sack. The bag's look ray target is a trigger, so it blocks nothing.
        public static SeedDrop SeedBag()
        {
            EnsureFolder("Assets/_Game/Prefabs/Boss");
            var root = new GameObject("PFB_SeedBag");
            try
            {
                Material canvas = Load("Canvas"), darkWood = Load("DarkWood");
                Part(root.transform, "Sack", PrimitiveType.Sphere, new Vector3(0f, 0.2f, 0f), new Vector3(0.45f, 0.4f, 0.45f), canvas);
                Part(root.transform, "Tie", PrimitiveType.Cylinder, new Vector3(0f, 0.42f, 0f), new Vector3(0.14f, 0.03f, 0.14f), darkWood);
                Part(root.transform, "Top", PrimitiveType.Sphere, new Vector3(0f, 0.5f, 0f), new Vector3(0.2f, 0.14f, 0.2f), canvas);
                var trigger = root.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.center = new Vector3(0f, 0.28f, 0f);
                trigger.radius = 0.4f;
                root.AddComponent<SeedDrop>();
                return PrefabUtility.SaveAsPrefabAsset(root, SeedBagPath).GetComponent<SeedDrop>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        }

        private static Material Load(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Cartoon + name + ".mat");
            if (material == null) throw new InvalidOperationException("Missing material " + Cartoon + name + ".mat");
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
