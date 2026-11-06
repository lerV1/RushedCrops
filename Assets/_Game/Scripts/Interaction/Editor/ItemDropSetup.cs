using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RushedCrops.Editor
{
    // Sets up item drops: DA_ItemModels, the PFB_ItemDrop pickup and the player's ItemDropper (Backspace).
    // The crop boss's seeds use the same drop. Run it again after adding an item.
    public static class ItemDropSetup
    {
        public const string ModelsPath = "Assets/_Game/Data/DA_ItemModels.asset";
        public const string DropPath = "Assets/_Game/Prefabs/Items/PFB_ItemDrop.prefab";
        private const string PlayerPrefab = "Assets/_Game/Prefabs/Player/PFB_Player.prefab";
        private const string CameraPath = "CameraRoot/Main Camera";

        // Long tools lie down on the ground instead of standing on end.
        private static readonly Dictionary<ResourceType, Vector3> GroundTurns = new Dictionary<ResourceType, Vector3>
        {
            { ResourceType.Hoe, new Vector3(0f, 0f, 90f) },
            { ResourceType.Hatchet, new Vector3(0f, 0f, 90f) },
            { ResourceType.Wood, new Vector3(0f, 35f, 0f) },
        };

        [MenuItem("Rushed Crops/Items/Set up item drops")]
        public static void SetUpAll()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Set up item drops outside Play.");
            DropPrefab();
            SetUpPlayer();
            Debug.Log("Backspace now drops the selected hotbar item (Shift + Backspace drops the whole stack).");
        }

        public static ItemModels Models()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ItemModels>(ModelsPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ItemModels>();
                AssetDatabase.CreateAsset(asset, ModelsPath);
            }

            var entries = new List<(ResourceType item, string path)>();
            foreach (var (item, model, _, _) in ItemIconRenderer.Models) entries.Add((item, model));
            foreach (var (seeds, _) in ItemIconRenderer.Seeds) entries.Add((seeds, ItemIconRenderer.SeedSack));

            var settings = new SerializedObject(asset);
            SerializedProperty list = settings.FindProperty("models");
            list.arraySize = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(entries[i].path);
                if (model == null) throw new InvalidOperationException("Missing item model " + entries[i].path);
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("item").enumValueIndex = (int)entries[i].item;
                entry.FindPropertyRelative("model").objectReferenceValue = model;
                entry.FindPropertyRelative("turn").vector3Value =
                    GroundTurns.TryGetValue(entries[i].item, out Vector3 turn) ? turn : Vector3.zero;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return asset;
        }

        // Makes or updates PFB_ItemDrop. Its trigger is what the look ray finds; it blocks nothing.
        public static ItemDrop DropPrefab()
        {
            ItemModels models = Models();
            EnsureFolder("Assets/_Game/Prefabs/Items");
            var root = new GameObject("PFB_ItemDrop");
            try
            {
                var trigger = root.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.center = new Vector3(0f, 0.28f, 0f);
                trigger.radius = 0.4f;
                var drop = root.AddComponent<ItemDrop>();
                var settings = new SerializedObject(drop);
                settings.FindProperty("models").objectReferenceValue = models;
                settings.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, DropPath).GetComponent<ItemDrop>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static void SetUpPlayer()
        {
            var dropPrefab = AssetDatabase.LoadAssetAtPath<ItemDrop>(DropPath);
            if (dropPrefab == null) dropPrefab = DropPrefab();

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                Transform view = root.transform.Find(CameraPath);
                if (view == null || root.GetComponent<PlayerTools>() == null)
                    throw new InvalidOperationException("PFB_Player is missing its camera or PlayerTools.");

                var dropper = root.GetComponent<ItemDropper>();
                if (dropper == null) dropper = root.AddComponent<ItemDropper>();
                var settings = new SerializedObject(dropper);
                settings.FindProperty("viewPoint").objectReferenceValue = view;
                settings.FindProperty("dropPrefab").objectReferenceValue = dropPrefab;
                settings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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
