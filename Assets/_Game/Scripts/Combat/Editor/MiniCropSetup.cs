using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RushedCrops.Editor
{
    // Builds the evil mini crops (one per crop, modelled in Blender) as prefabs and puts the spawner on the crop
    // field. Aggressive ones (tomato, beet, onion) chase the player on sight; grumpy ones (carrot, cabbage,
    // wheat) only fight back after being hit. How tough each one is lives in the table below.
    public static class MiniCropSetup
    {
        private const string ModelFolder = "Assets/_Game/Models/MiniCrops";
        private const string MaterialFolder = "Assets/_Game/Materials/MiniCrops";
        public const string PrefabFolder = "Assets/_Game/Prefabs/Enemies";
        private const string Template = "Assets/_Game/Materials/Cartoon/Straw.mat";
        // The Blender models are 1 to 1.5 m tall; this makes them knee-to-waist high next to the player.
        private const float ModelScale = 0.7f;

        private static readonly Dictionary<string, Color> Colours = new Dictionary<string, Color>
        {
            { "Mini_Dark", new Color(0.12f, 0.07f, 0.05f) },
            { "Mini_EyeWhite", new Color(0.97f, 0.95f, 0.86f) },
            { "Mini_Pupil", new Color(0.85f, 0.06f, 0.04f) },
            { "Mini_Leaf", new Color(0.35f, 0.7f, 0.2f) },
            { "Mini_LeafDark", new Color(0.18f, 0.48f, 0.12f) },
            { "Mini_Orange", new Color(0.95f, 0.45f, 0.1f) },
            { "Mini_Red", new Color(0.9f, 0.2f, 0.12f) },
            { "Mini_Beet", new Color(0.56f, 0.08f, 0.26f) },
            { "Mini_BeetDark", new Color(0.36f, 0.04f, 0.16f) },
            { "Mini_Cabbage", new Color(0.62f, 0.84f, 0.42f) },
            { "Mini_CabbageDark", new Color(0.36f, 0.62f, 0.26f) },
            { "Mini_Onion", new Color(0.62f, 0.27f, 0.5f) },
            { "Mini_OnionDark", new Color(0.44f, 0.16f, 0.36f) },
            { "Mini_Wheat", new Color(0.95f, 0.78f, 0.32f) },
            { "Mini_WheatDark", new Color(0.72f, 0.52f, 0.18f) },
        };

        private class Spec
        {
            public string model, crop, displayName;
            public MiniCrop.Temper temper;
            public int health, damage;
            public float chaseSpeed, radius, height;
        }

        private static Spec Make(string model, string crop, string displayName, MiniCrop.Temper temper,
            int health, int damage, float chaseSpeed, float radius, float height) => new Spec
        {
            model = model, crop = crop, displayName = displayName, temper = temper,
            health = health, damage = damage, chaseSpeed = chaseSpeed, radius = radius, height = height,
        };

        // The hatchet does 25 damage, so 30-50 health is two hits and 60 is three.
        private static readonly Spec[] Specs =
        {
            Make("MiniCarrot", "DA_Carrot", "Grumpy Carrot", MiniCrop.Temper.Grumpy, 30, 6, 3.4f, 0.3f, 1.3f),
            Make("MiniTomato", "DA_Tomato", "Evil Tomato", MiniCrop.Temper.Aggressive, 40, 8, 2.8f, 0.42f, 1.1f),
            Make("MiniBeet", "DA_Beet", "Evil Beet", MiniCrop.Temper.Aggressive, 35, 7, 3.0f, 0.36f, 1.3f),
            Make("MiniCabbage", "DA_Cabbage", "Grumpy Cabbage", MiniCrop.Temper.Grumpy, 60, 10, 2.2f, 0.45f, 1.1f),
            Make("MiniOnion", "DA_Onion", "Evil Onion", MiniCrop.Temper.Aggressive, 45, 9, 3.2f, 0.4f, 1.3f),
            Make("MiniWheat", "DA_Wheat", "Grumpy Wheat", MiniCrop.Temper.Grumpy, 25, 5, 3.8f, 0.25f, 1.3f),
        };

        public static int VariantCount => Specs.Length;

        [MenuItem("Rushed Crops/Combat/Set up mini crops")]
        public static void SetUpInScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Set up the mini crops outside Play.");
            FarmField field = UnityEngine.Object.FindAnyObjectByType<FarmField>();
            if (field == null) throw new InvalidOperationException("Open SC_Farm with the crop field built first.");
            MiniCrop[] prefabs = MakePrefabs();
            PlaceSpawner(field, prefabs);
            EditorSceneManager.MarkSceneDirty(field.gameObject.scene);
            Debug.Log($"Set up {prefabs.Length} mini crops and their spawner on the crop field.");
        }

        public static MiniCrop[] MakePrefabs()
        {
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);
            var drop = AssetDatabase.LoadAssetAtPath<ItemDrop>(ItemDropSetup.DropPath);
            if (drop == null) drop = ItemDropSetup.DropPrefab();

            var prefabs = new MiniCrop[Specs.Length];
            for (int i = 0; i < Specs.Length; i++)
            {
                prefabs[i] = MakePrefab(Specs[i], drop);
            }
            AssetDatabase.SaveAssets();
            return prefabs;
        }

        // Puts the spawner on a crop field using the prefabs already saved (making them only if one is missing).
        // FarmPlotSetup calls this every time it builds a new crop field, so rebuilding the field or the whole
        // map keeps the mini crops.
        public static void AddSpawner(FarmField field)
        {
            var prefabs = new MiniCrop[Specs.Length];
            for (int i = 0; i < Specs.Length; i++)
            {
                prefabs[i] = AssetDatabase.LoadAssetAtPath<MiniCrop>($"{PrefabFolder}/PFB_{Specs[i].model}.prefab");
                if (prefabs[i] == null)
                {
                    prefabs = MakePrefabs();
                    break;
                }
            }
            PlaceSpawner(field, prefabs);
        }

        private static MiniCrop MakePrefab(Spec spec, ItemDrop drop)
        {
            string modelPath = $"{ModelFolder}/{spec.model}.fbx";
            SetUpModelMaterials(modelPath);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (asset == null) throw new InvalidOperationException("Missing model " + modelPath);
            var crop = AssetDatabase.LoadAssetAtPath<CropData>($"Assets/_Game/Data/{spec.crop}.asset");
            if (crop == null) throw new InvalidOperationException("Missing crop asset " + spec.crop);

            var root = new GameObject(spec.model);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
                model.name = "Model";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * ModelScale;

                var body = root.AddComponent<Rigidbody>();
                body.mass = 1f;
                body.constraints = RigidbodyConstraints.FreezeRotation;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.Continuous;

                var shape = root.AddComponent<CapsuleCollider>();
                shape.radius = spec.radius * ModelScale;
                shape.height = spec.height * ModelScale;
                shape.center = new Vector3(0f, shape.height * 0.5f, 0f);

                var health = new SerializedObject(root.AddComponent<Health>());
                health.FindProperty("maxHealth").intValue = spec.health;
                health.ApplyModifiedPropertiesWithoutUndo();
                root.AddComponent<Knockback>();
                root.AddComponent<HitFlash>();

                var settings = new SerializedObject(root.AddComponent<MiniCrop>());
                settings.FindProperty("displayName").stringValue = spec.displayName;
                settings.FindProperty("temper").enumValueIndex = (int)spec.temper;
                settings.FindProperty("crop").objectReferenceValue = crop;
                settings.FindProperty("model").objectReferenceValue = model.transform;
                settings.FindProperty("dropPrefab").objectReferenceValue = drop;
                settings.FindProperty("damage").intValue = spec.damage;
                settings.FindProperty("chaseSpeed").floatValue = spec.chaseSpeed;
                settings.ApplyModifiedPropertiesWithoutUndo();

                string path = $"{PrefabFolder}/PFB_{spec.model}.prefab";
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<MiniCrop>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void PlaceSpawner(FarmField field, MiniCrop[] prefabs)
        {
            var inventory = UnityEngine.Object.FindAnyObjectByType<Inventory>();
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            if (inventory == null || player == null || player.GetComponent<Health>() == null)
                Debug.LogWarning("The mini crop spawner needs the Inventory and a player with Health. Run Rushed Crops > Combat > Set up mini crops once they exist.", field);

            var spawner = field.GetComponent<MiniCropSpawner>();
            if (spawner == null) spawner = Undo.AddComponent<MiniCropSpawner>(field.gameObject);

            var settings = new SerializedObject(spawner);
            SerializedProperty list = settings.FindProperty("cropPrefabs");
            list.arraySize = prefabs.Length;
            for (int i = 0; i < prefabs.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            }
            settings.FindProperty("inventory").objectReferenceValue = inventory;
            settings.FindProperty("player").objectReferenceValue = player != null ? player.GetComponent<Health>() : null;
            // Same area as the crop field.
            settings.FindProperty("areaSize").vector2Value = new SerializedObject(field).FindProperty("size").vector2Value;
            settings.ApplyModifiedProperties();
        }

        // One flat cartoon material per Blender slot, made from Straw so it shares the shader.
        private static void SetUpModelMaterials(string modelPath)
        {
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing model " + modelPath);
            var template = AssetDatabase.LoadAssetAtPath<Material>(Template);
            if (template == null) throw new InvalidOperationException("Missing material " + Template);

            bool changed = false;
            foreach (KeyValuePair<string, Color> pair in Colours)
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
