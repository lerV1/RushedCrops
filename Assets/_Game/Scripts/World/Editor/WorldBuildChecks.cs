using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RushedCrops.Editor
{
    // The same map checks can run from the editor menu or Unity's batch build command.
    public static class WorldBuildChecks
    {
        [MenuItem("Rushed Crops/World/Validate saved map")]
        public static void ValidateSavedMap()
        {
            if (Application.isPlaying) throw new BuildFailedException("Validate the saved scene outside Play.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != FarmWorldBuilder.ScenePath)
            {
                if (scene.isDirty) throw new BuildFailedException("Save the current scene before validating SC_Farm.");
                scene = EditorSceneManager.OpenScene(FarmWorldBuilder.ScenePath);
            }
            Validate(scene);
            Debug.Log("World validation passed: landmarks, player, farm plots, colliders, materials and environment references.");
        }

        private static void Validate(Scene scene)
        {
            GameObject root = GameObject.Find(FarmWorldBuilder.RootName);
            Require(root != null, "The saved farm map is missing.");
            foreach (string group in FarmWorldBuilder.Groups)
                Require(root.transform.Find(group) != null, "Missing world group: " + group);

            Require(GameObject.Find("Player")?.GetComponent<PlayerController>() != null, "Player movement is missing.");
            Require(Camera.main != null, "Main camera is missing.");
            Require(UnityEngine.Object.FindFirstObjectByType<Inventory>() != null, "The shared Inventory is missing.");
            Require(UnityEngine.Object.FindFirstObjectByType<InventoryHud>() != null, "The existing inventory HUD is missing.");
            var terrain = root.transform.Find(FarmWorldBuilder.TerrainPath);
            Require(terrain != null && terrain.GetComponent<MeshCollider>()?.sharedMesh != null, "Terrain must have a saved collision mesh.");
            Require(terrain.gameObject.layer == 6, "Terrain must use the player's existing Ground layer.");
            Require(root.transform.Find(FarmWorldBuilder.BridgePath) != null, "The river bridge to the quarry is missing.");
            Transform garden = root.transform.Find(FarmWorldBuilder.FarmGroup);
            int plots = 0;
            foreach (Transform child in garden) if (child.name.StartsWith("Planting plot")) plots++;
            Require(plots == 6, "Keep all six empty soil plots.");
            foreach (Transform child in garden.GetComponentsInChildren<Transform>())
                Require(child == garden || !child.name.Contains("Plant") || child.name.StartsWith("Planting plot"), "The farming plots should remain unplanted.");
            foreach (Transform child in garden)
                if (child.name.StartsWith("Planting plot")) ValidateFarmPlot(child);

            GameObject player = GameObject.Find("Player");
            Require(player.GetComponent<PlayerInteractor>() != null, "The player can't use things: PlayerInteractor is missing.");
            Require(player.GetComponent<SeedSelector>() != null, "The player can't switch seeds: SeedSelector is missing.");
            Require(UnityEngine.Object.FindAnyObjectByType<SelectedSeedHud>() != null, "The selected seed display is missing.");

            var cycle = UnityEngine.Object.FindFirstObjectByType<EnvironmentCycle>();
            Require(cycle != null, "The day/night cycle is missing.");
            var settings = new SerializedObject(cycle);
            Require(settings.FindProperty("sun").objectReferenceValue != null, "Assign the sun.");
            Require(settings.FindProperty("moon").objectReferenceValue != null, "Assign the moon.");
            Require(settings.FindProperty("lanterns").arraySize >= 8, "The night paths need their lantern references.");
            for (int i = 0; i < settings.FindProperty("lanterns").arraySize; i++)
                Require(settings.FindProperty("lanterns").GetArrayElementAtIndex(i).objectReferenceValue != null, "A lantern reference is missing.");
            Require(UnityEngine.Object.FindFirstObjectByType<EnvironmentClockHud>() != null, "The day/time display is missing.");
            int misplaced = WorldGrounding.CountMisplaced(out string report);
            Require(misplaced == 0, misplaced + " scenery objects are not sitting on the ground. Run Align scenery to ground.\n" + report);

            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            foreach (Transform child in sceneRoot.GetComponentsInChildren<Transform>(true))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0, "Missing script on " + child.name);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
                Require(material != null && material.shader != null && material.shader.name != "Hidden/InternalErrorShader", "Missing material or shader on " + renderer.name);
        }

        // Each soil bed must be ready to farm: a FarmPlot with the Inventory, crops that have
        // every growth model, and squares (FarmTile) the player can aim at, each with its soil and outline.
        private static void ValidateFarmPlot(Transform plot)
        {
            var farmPlot = plot.GetComponent<FarmPlot>();
            Require(farmPlot != null, plot.name + " is missing its FarmPlot. Run Rushed Crops > Farming > Set up farm plots.");
            var settings = new SerializedObject(farmPlot);
            Require(settings.FindProperty("inventory").objectReferenceValue != null, plot.name + " needs the Inventory assigned.");

            SerializedProperty crops = settings.FindProperty("crops");
            Require(crops.arraySize > 0, plot.name + " has no crops to grow.");
            for (int i = 0; i < crops.arraySize; i++)
            {
                var crop = crops.GetArrayElementAtIndex(i).objectReferenceValue as CropData;
                Require(crop != null, plot.name + " has an empty crop slot.");
                for (int stage = 0; stage < crop.StageCount; stage++)
                    Require(crop.GetStageModel(stage) != null, crop.name + " is missing the model for growth stage " + (stage + 1) + ".");
            }

            Transform tiles = plot.Find("Tiles");
            Require(tiles != null && tiles.childCount > 0, plot.name + " has no farm squares. Run Rushed Crops > Farming > Set up farm plots.");
            foreach (Transform tile in tiles)
            {
                var farmTile = tile.GetComponent<FarmTile>();
                Require(farmTile != null, tile.name + " in " + plot.name + " is missing its FarmTile.");
                var tileSettings = new SerializedObject(farmTile);
                Require(tileSettings.FindProperty("soil").objectReferenceValue != null && tileSettings.FindProperty("highlight").objectReferenceValue != null,
                    tile.name + " in " + plot.name + " needs its soil and highlight assigned.");
                var trigger = tile.GetComponent<Collider>();
                Require(trigger != null && trigger.isTrigger, tile.name + " in " + plot.name + " needs a trigger so the player can aim at it.");
            }
        }

        // CI creates a downloadable Windows player only after the scene checks pass.
        public static void BuildWindows()
        {
            ValidateSavedMap();
            const string output = "Builds/Windows/RushedCrops.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { FarmWorldBuilder.ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Windows build failed: " + report.summary.result);
            Debug.Log("Windows player built at " + output);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new BuildFailedException(message);
        }
    }
}
