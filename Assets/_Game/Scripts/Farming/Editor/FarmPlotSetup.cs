using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RushedCrops.Editor
{
    // Turns the garden's soil beds into working FarmPlots: the pack's ridges are hidden so the bed
    // starts as flat ground, and each bed gets a 5 x 5 grid of FarmTile squares. Every square has a
    // trigger the player can aim at, a dug-soil mound (shown once tilled) and an outline (shown while aimed at).
    // The map builder calls this for every new bed, and it can be run again from the menu.
    public static class FarmPlotSetup
    {
        public static readonly string[] CropAssets =
        {
            "Assets/_Game/Data/DA_Carrot.asset",
            "Assets/_Game/Data/DA_Tomato.asset",
        };
        private const string SoilModel = "Assets/_Game/Models/Farm/SoilMound.fbx";
        private const string FrameModel = "Assets/_Game/Models/Farm/TileFrame.fbx";
        private const int GroundLayer = 6;

        [MenuItem("Rushed Crops/Farming/Set up farm plots")]
        public static void SetUpAllPlots()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Set up the plots outside Play.");
            var garden = GameObject.Find(FarmWorldBuilder.RootName)?.transform.Find(FarmWorldBuilder.FarmGroup);
            if (garden == null) throw new InvalidOperationException("Open SC_Farm with the farm map built first.");

            int count = 0;
            foreach (Transform child in garden)
            {
                if (!child.name.StartsWith("Planting plot")) continue;
                SetUp(child);
                count++;
            }
            EditorSceneManager.MarkSceneDirty(garden.gameObject.scene);
            Debug.Log($"Set up {count} farm plots.");
        }

        public static void SetUp(Transform plot)
        {
            foreach (string old in new[] { "Crop spots", "Interact area", "Tiles" })
            {
                Transform t = plot.Find(old);
                if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
            }
            List<Vector3> squares = SquareCentres(plot);
            HideRidges(plot);

            var root = new GameObject("Tiles").transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Set up farm plot");
            root.SetParent(plot, false);
            for (int i = 0; i < squares.Count; i++) MakeTile(root, i, squares[i], plot);

            var farmPlot = plot.GetComponent<FarmPlot>();
            if (farmPlot == null) farmPlot = Undo.AddComponent<FarmPlot>(plot.gameObject);
            var settings = new SerializedObject(farmPlot);
            var inventory = UnityEngine.Object.FindAnyObjectByType<Inventory>();
            if (inventory == null) Debug.LogWarning("No Inventory in the scene yet. Assign it to each FarmPlot.", plot);
            settings.FindProperty("inventory").objectReferenceValue = inventory;
            SerializedProperty crops = settings.FindProperty("crops");
            crops.arraySize = CropAssets.Length;
            for (int i = 0; i < CropAssets.Length; i++)
                crops.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<CropData>(CropAssets[i]);
            settings.ApplyModifiedProperties();
        }

        // One square where each of the pack's (hidden) planted beets sat: a 5 x 5 grid, 1 m apart.
        // Row by row, back to front, left to right, so "Tile 01" is always the same corner.
        private static List<Vector3> SquareCentres(Transform plot)
        {
            var beets = new List<Transform>();
            foreach (LODGroup lod in plot.GetComponentsInChildren<LODGroup>(true))
                if (lod.name.StartsWith("BeetPlant")) beets.Add(lod.transform);
            beets.Sort((a, b) =>
            {
                int byRow = b.localPosition.z.CompareTo(a.localPosition.z);
                return byRow != 0 ? byRow : a.localPosition.x.CompareTo(b.localPosition.x);
            });
            var centres = new List<Vector3>();
            foreach (Transform beet in beets) centres.Add(GroundUnder(beet.position, plot));
            return centres;
        }

        // The pack's soil ridges are replaced by per-square mounds, so the bed starts as flat ground.
        private static void HideRidges(Transform plot)
        {
            foreach (LODGroup lod in plot.GetComponentsInChildren<LODGroup>(true))
            {
                if (!lod.name.StartsWith("Ridge")) continue;
                Undo.RecordObject(lod.gameObject, "Hide ridge");
                lod.gameObject.SetActive(false);
            }
        }

        private static void MakeTile(Transform root, int index, Vector3 centre, Transform plot)
        {
            var tile = new GameObject("Tile " + (index + 1).ToString("00"), typeof(BoxCollider));
            tile.layer = 0; // Default, not Ground: the trigger must never count as floor for jumping
            tile.transform.SetParent(root, false);
            tile.transform.position = centre;
            var box = tile.GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.2f, 0f);
            box.size = new Vector3(0.98f, 0.4f, 0.98f);

            GameObject soil = Child(tile.transform, SoilModel, "Soil");
            GameObject highlight = Child(tile.transform, FrameModel, "Highlight");
            foreach (Renderer r in highlight.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var farmTile = tile.AddComponent<FarmTile>();
            var settings = new SerializedObject(farmTile);
            settings.FindProperty("soil").objectReferenceValue = soil;
            settings.FindProperty("highlight").objectReferenceValue = highlight;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Child(Transform parent, string model, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(model), parent);
            go.name = name;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.SetActive(false);
            return go;
        }

        // The terrain height under a point, ignoring the bed's own ridges.
        private static Vector3 GroundUnder(Vector3 at, Transform plot)
        {
            RaycastHit[] hits = Physics.RaycastAll(at + Vector3.up * 5f, Vector3.down, 20f, 1 << GroundLayer);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
                if (!hit.collider.transform.IsChildOf(plot)) return hit.point;
            return new Vector3(at.x, plot.position.y, at.z);
        }
    }
}
