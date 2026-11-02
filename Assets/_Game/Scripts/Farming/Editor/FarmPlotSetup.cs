using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RushedCrops.Editor
{
    // Builds the garden's crop field: one FarmField over the tilled soil inside the fence, with the
    // shared FarmPlot settings (Inventory, crops, wet soil), the outline that follows the aimed square,
    // and the square prefab (PFB_FarmTile) it copies wherever the player tills.
    // The map builder calls this, and it can be run again from the menu. Running it also removes the
    // old fixed planting beds from scenes made before the field existed.
    public static class FarmPlotSetup
    {
        public static readonly string[] CropAssets =
        {
            "Assets/_Game/Data/DA_Carrot.asset",
            "Assets/_Game/Data/DA_Tomato.asset",
            "Assets/_Game/Data/DA_Beet.asset",
            "Assets/_Game/Data/DA_Cabbage.asset",
            "Assets/_Game/Data/DA_Onion.asset",
            "Assets/_Game/Data/DA_Wheat.asset",
        };
        public const string FieldName = "Crop field";
        private const string TilePrefab = "Assets/_Game/Prefabs/Farm/PFB_FarmTile.prefab";
        private const string SoilModel = "Assets/_Game/Models/Farm/SoilMound.fbx";
        private const string FrameModel = "Assets/_Game/Models/Farm/TileFrame.fbx";
        private const string WetSoilMaterial = "Assets/_Game/Materials/Farm/MAT_SoilWet.mat";
        private const float Margin = 1f; // squares stay this far inside the fence
        private const int GroundLayer = 6;

        [MenuItem("Rushed Crops/Farming/Set up crop field")]
        public static void SetUpInScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Set up the crop field outside Play.");
            var garden = GameObject.Find(FarmWorldBuilder.RootName)?.transform.Find(FarmWorldBuilder.FarmGroup);
            if (garden == null) throw new InvalidOperationException("Open SC_Farm with the farm map built first.");
            SetUpField(garden, FarmWorldBuilder.Garden, FarmWorldBuilder.GardenHalfSize);
            EditorSceneManager.MarkSceneDirty(garden.gameObject.scene);
            Debug.Log("Crop field set up.");
        }

        // centre and halfSize describe the fenced garden on the ground (x, z), in metres.
        public static GameObject SetUpField(Transform garden, Vector2 centre, Vector2 halfSize)
        {
            for (int i = garden.childCount - 1; i >= 0; i--)
            {
                Transform child = garden.GetChild(i);
                if (child.name == FieldName || child.name.StartsWith("Planting plot")) Undo.DestroyObjectImmediate(child.gameObject);
            }

            var field = new GameObject(FieldName);
            Undo.RegisterCreatedObjectUndo(field, "Set up crop field");
            field.transform.SetParent(garden, false);
            field.transform.position = Ground(new Vector3(centre.x, 0f, centre.y));

            var plot = field.AddComponent<FarmPlot>();
            var settings = new SerializedObject(plot);
            var inventory = UnityEngine.Object.FindAnyObjectByType<Inventory>();
            if (inventory == null) Debug.LogWarning("No Inventory in the scene yet. Assign it to the crop field's FarmPlot.", field);
            settings.FindProperty("inventory").objectReferenceValue = inventory;
            settings.FindProperty("wetSoil").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(WetSoilMaterial);
            settings.ApplyModifiedProperties();
            AssignCrops(plot);

            // The outline that follows the aimed square.
            var cursor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FrameModel), field.transform);
            cursor.name = "Cursor";
            foreach (Renderer r in cursor.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cursor.SetActive(false);

            var farmField = field.AddComponent<FarmField>();
            var fieldSettings = new SerializedObject(farmField);
            fieldSettings.FindProperty("size").vector2Value = halfSize * 2f - Vector2.one * (2f * Margin);
            fieldSettings.FindProperty("cellSize").floatValue = 1f;
            fieldSettings.FindProperty("tilePrefab").objectReferenceValue = TilePrefabAsset();
            fieldSettings.FindProperty("cursor").objectReferenceValue = cursor;
            fieldSettings.FindProperty("groundLayers").intValue = 1 << GroundLayer;
            fieldSettings.ApplyModifiedProperties();
            return field;
        }

        // Gives the crop field the full crop list without rebuilding it, so hand edits stay.
        // Run this after adding a crop to CropAssets.
        [MenuItem("Rushed Crops/Farming/Update crops in crop field")]
        public static void UpdateCropsInField()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Update the crop field outside Play.");
            var garden = GameObject.Find(FarmWorldBuilder.RootName)?.transform.Find(FarmWorldBuilder.FarmGroup);
            if (garden == null) throw new InvalidOperationException("Open SC_Farm with the farm map built first.");

            foreach (FarmPlot plot in garden.GetComponentsInChildren<FarmPlot>(true)) AssignCrops(plot);
            EditorSceneManager.MarkSceneDirty(garden.gameObject.scene);
            Debug.Log($"Gave the crop field all {CropAssets.Length} crops.");
        }

        public static void AssignCrops(FarmPlot farmPlot)
        {
            var settings = new SerializedObject(farmPlot);
            SerializedProperty crops = settings.FindProperty("crops");
            crops.arraySize = CropAssets.Length;
            for (int i = 0; i < CropAssets.Length; i++)
            {
                var crop = AssetDatabase.LoadAssetAtPath<CropData>(CropAssets[i]);
                if (crop == null) throw new InvalidOperationException("Missing crop asset: " + CropAssets[i]);
                crops.GetArrayElementAtIndex(i).objectReferenceValue = crop;
            }
            settings.ApplyModifiedProperties();
        }

        // The square the field copies: a FarmTile with its dug-soil mound. Made once and reused.
        private static FarmTile TilePrefabAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<FarmTile>(TilePrefab);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder("Assets/_Game/Prefabs/Farm")) AssetDatabase.CreateFolder("Assets/_Game/Prefabs", "Farm");
            var root = new GameObject("PFB_FarmTile");
            var soil = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SoilModel), root.transform);
            soil.name = "Soil";
            soil.SetActive(false);
            var tile = root.AddComponent<FarmTile>();
            var settings = new SerializedObject(tile);
            settings.FindProperty("soil").objectReferenceValue = soil;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, TilePrefab);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<FarmTile>();
        }

        private static Vector3 Ground(Vector3 at)
        {
            return Physics.Raycast(at + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 40f, 1 << GroundLayer) ? hit.point : at;
        }
    }
}
