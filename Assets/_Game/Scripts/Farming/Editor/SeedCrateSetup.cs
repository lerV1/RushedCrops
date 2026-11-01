using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RushedCrops.Editor
{
    // Places a row of open crates outside the garden fence, just east of the gate: one crate per crop,
    // with that crop's vegetable on top so the player can tell them apart. Each crate gives seeds
    // (see SeedCrate). The map builder calls this, and it can be run again from the menu.
    public static class SeedCrateSetup
    {
        public const string RootName = "Seed crates";
        private const string Pack = "Assets/Holotna/Medieval Rural/Prefabs/";
        private const string CrateModel = Pack + "Containers/CrateOpen01.prefab";
        private const string TomatoModel = "Assets/_Game/Models/Crops/Tomato01.fbx";
        private const int GroundLayer = 6;

        // The first crate sits by the bucket east of the gate; the rest follow the fence eastwards.
        private static readonly Vector3 FirstSpot = new Vector3(-20.8f, 0f, -20.9f);
        private const float Spacing = 1.3f;
        private const float CrateScale = 1.6f;
        private const float ProduceScale = 1.6f;

        [MenuItem("Rushed Crops/Farming/Place seed crates")]
        public static void PlaceInScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Place the crates outside Play.");
            var farm = GameObject.Find(FarmWorldBuilder.RootName)?.transform.Find(FarmWorldBuilder.FarmGroup);
            if (farm == null) throw new InvalidOperationException("Open SC_Farm with the farm map built first.");
            Place(farm);
            EditorSceneManager.MarkSceneDirty(farm.gameObject.scene);
            Debug.Log($"Placed {FarmPlotSetup.CropAssets.Length} seed crates by the garden gate.");
        }

        public static GameObject Place(Transform parent)
        {
            Transform old = parent.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var inventory = UnityEngine.Object.FindAnyObjectByType<Inventory>();
            if (inventory == null) Debug.LogWarning("No Inventory in the scene yet. Assign it to each SeedCrate.");

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Place seed crates");
            root.transform.SetParent(parent, false);

            for (int i = 0; i < FarmPlotSetup.CropAssets.Length; i++)
            {
                var crop = AssetDatabase.LoadAssetAtPath<CropData>(FarmPlotSetup.CropAssets[i]);
                if (crop == null) throw new InvalidOperationException("Missing crop asset: " + FarmPlotSetup.CropAssets[i]);
                MakeCrate(root.transform, crop, inventory, Ground(FirstSpot + Vector3.right * (i * Spacing)));
            }
            return root;
        }

        private static void MakeCrate(Transform parent, CropData crop, Inventory inventory, Vector3 position)
        {
            var holder = new GameObject(crop.DisplayName + " seed crate");
            holder.transform.SetParent(parent, false);
            holder.transform.position = position;

            GameObject crate = Instantiate(CrateModel, holder.transform);
            crate.transform.localScale = Vector3.one * CrateScale;
            foreach (Collider c in crate.GetComponentsInChildren<Collider>(true)) c.gameObject.layer = GroundLayer;
            float top = TopOf(crate);

            // The crop's vegetable resting on top of the crate, as a label.
            GameObject produce = Instantiate(ProduceModel(crop.CropType), holder.transform);
            produce.name = crop.DisplayName + " on crate";
            produce.transform.localScale = Vector3.one * ProduceScale;
            produce.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
            foreach (Collider c in produce.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            Bounds produceBounds = BoundsOf(produce);
            produce.transform.position += Vector3.up * (top - 0.05f - produceBounds.min.y);

            // A trigger round the crate and vegetable so the look ray finds the crate; it blocks nothing.
            Bounds all = BoundsOf(holder);
            var box = holder.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = all.center - position;
            box.size = all.size + Vector3.one * 0.1f;

            var seedCrate = holder.AddComponent<SeedCrate>();
            var settings = new SerializedObject(seedCrate);
            settings.FindProperty("inventory").objectReferenceValue = inventory;
            settings.FindProperty("crop").objectReferenceValue = crop;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string ProduceModel(CropType crop)
        {
            switch (crop)
            {
                case CropType.Tomato: return TomatoModel;
                case CropType.Beet: return Pack + "Crops/Beet01.prefab";
                case CropType.Cabbage: return Pack + "Crops/Cabbage01.prefab";
                case CropType.Onion: return Pack + "Crops/Onion01.prefab";
                case CropType.Wheat: return Pack + "Crops/Wheat01.prefab";
                default: return Pack + "Crops/Carrot01.prefab";
            }
        }

        // Keeps the prefab link and uses the pack's flat "Colored" materials, like the rest of the map.
        private static GameObject Instantiate(string path, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException("Missing model: " + path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    var coloured = AssetDatabase.LoadAssetAtPath<Material>("Assets/Holotna/Medieval Rural/Materials/Colored/" + materials[i].name + ".mat");
                    if (coloured != null) materials[i] = coloured;
                }
                renderer.sharedMaterials = materials;
            }
            return go;
        }

        private static float TopOf(GameObject go) => BoundsOf(go).max.y;

        private static Bounds BoundsOf(GameObject go)
        {
            bool found = false;
            Bounds bounds = new Bounds(go.transform.position, Vector3.zero);
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                if (found) bounds.Encapsulate(renderer.bounds);
                else bounds = renderer.bounds;
                found = true;
            }
            return bounds;
        }

        private static Vector3 Ground(Vector3 at)
        {
            return Physics.Raycast(at + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 40f, 1 << GroundLayer)
                ? hit.point
                : at;
        }
    }
}
