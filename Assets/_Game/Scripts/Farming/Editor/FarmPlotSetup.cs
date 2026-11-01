using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RushedCrops.Editor
{
    // Turns the garden's soil beds into working FarmPlots: crop spots on the ridges, a trigger
    // the player can aim at, and the FarmPlot itself with the Inventory and both crops.
    // The map builder calls this for every new bed, and it can be run again from the menu.
    public static class FarmPlotSetup
    {
        public static readonly string[] CropAssets =
        {
            "Assets/_Game/Data/DA_Carrot.asset",
            "Assets/_Game/Data/DA_Tomato.asset",
        };

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
            Transform[] spots = MakeSpots(plot);
            MakeTrigger(plot);

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

            SerializedProperty list = settings.FindProperty("spots");
            list.arraySize = spots.Length;
            for (int i = 0; i < spots.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = spots[i];
            settings.ApplyModifiedProperties();
        }

        // One spot where each of the pack's planted beets sat (they are hidden), so plants line up on the ridges.
        private static Transform[] MakeSpots(Transform plot)
        {
            Transform old = plot.Find("Crop spots");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var beets = new List<Transform>();
            foreach (LODGroup lod in plot.GetComponentsInChildren<LODGroup>(true))
                if (lod.name.StartsWith("BeetPlant")) beets.Add(lod.transform);
            // Row by row, back to front, left to right, so "Spot 01" is always the same corner.
            beets.Sort((a, b) =>
            {
                int byRow = b.localPosition.z.CompareTo(a.localPosition.z);
                return byRow != 0 ? byRow : a.localPosition.x.CompareTo(b.localPosition.x);
            });

            var root = new GameObject("Crop spots").transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Set up farm plot");
            root.SetParent(plot, false);
            var spots = new Transform[beets.Count];
            for (int i = 0; i < beets.Count; i++)
            {
                spots[i] = new GameObject("Spot " + (i + 1).ToString("00")).transform;
                spots[i].SetParent(root, false);
                spots[i].position = beets[i].position - Vector3.up * 0.02f;
            }
            return spots;
        }

        // A trigger box over the whole bed, so the look ray finds the plot between the ridges too.
        // It sits on the Default layer, not Ground, so it never counts as floor for jumping.
        private static void MakeTrigger(Transform plot)
        {
            Transform old = plot.Find("Interact area");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var area = new GameObject("Interact area", typeof(BoxCollider));
            Undo.RegisterCreatedObjectUndo(area, "Set up farm plot");
            area.layer = 0;
            area.transform.SetParent(plot, false);
            var box = area.GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.35f, 0f);
            box.size = new Vector3(5f, 0.7f, 6f);
        }
    }
}
