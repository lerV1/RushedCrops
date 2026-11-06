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
            Debug.Log("World validation passed: landmarks, player, crop field, tools, river water, merchant, hotbar pictures, barn repairs, colliders, materials and environment references.");
        }

        private static void Validate(Scene scene)
        {
            GameObject root = GameObject.Find(FarmWorldBuilder.RootName);
            Require(root != null, "The saved farm map is missing.");
            foreach (string group in FarmWorldBuilder.Groups)
                Require(root.transform.Find(group) != null, "Missing world group: " + group);

            Require(GameObject.Find("Player")?.GetComponent<PlayerController>() != null, "Player movement is missing.");
            Require(Camera.main != null, "Main camera is missing.");
            Require(UnityEngine.Object.FindAnyObjectByType<Inventory>() != null, "The shared Inventory is missing.");
            Require(UnityEngine.Object.FindAnyObjectByType<InventoryHud>() != null, "The existing inventory HUD is missing.");
            var terrain = root.transform.Find(FarmWorldBuilder.TerrainPath);
            Require(terrain != null && terrain.GetComponent<MeshCollider>()?.sharedMesh != null, "Terrain must have a saved collision mesh.");
            Require(terrain.gameObject.layer == 6, "Terrain must use the player's existing Ground layer.");
            Require(root.transform.Find(FarmWorldBuilder.BridgePath) != null, "The river bridge to the quarry is missing.");
            Transform garden = root.transform.Find(FarmWorldBuilder.FarmGroup);
            foreach (Transform child in garden.GetComponentsInChildren<Transform>())
                Require(child == garden || !child.name.Contains("Plant"), "The garden should be saved unplanted.");
            ValidateCropField(garden);

            GameObject player = GameObject.Find("Player");
            Require(player.GetComponent<PlayerInteractor>() != null, "The player can't use things: PlayerInteractor is missing.");
            var heldTools = player.GetComponent<PlayerTools>();
            Require(heldTools != null && heldTools.Inventory != null, "The player's PlayerTools needs the Inventory assigned, so keys 1-5 can select hotbar slots.");
            Require(player.GetComponent<Health>() != null, "The player has no Health. Run Rushed Crops > Player > Set up health and respawn.");
            var respawn = player.GetComponent<RespawnController>();
            Require(respawn != null, "The player has no RespawnController. Run Rushed Crops > Player > Set up health and respawn.");
            Require(new SerializedObject(respawn).FindProperty("startingCheckpoint").objectReferenceValue != null, "Assign the player's starting checkpoint.");
            Require(UnityEngine.Object.FindAnyObjectByType<Checkpoint>() != null, "The map has no checkpoint trigger.");
            Require(player.GetComponentInChildren<HealthBarHud>(true) != null, "The health bar is missing. Run Rushed Crops > UI > Set up health HUD.");
            Require(player.GetComponentInChildren<DamageIndicatorHud>(true) != null, "The damage indicator is missing. Run Rushed Crops > UI > Set up health HUD.");
            ValidateTools(player, garden, root.transform);
            ValidateMerchant(root.transform);
            ValidateMelee(player, garden);
            ValidateItemIcons(UnityEngine.Object.FindAnyObjectByType<InventoryHud>());
            ValidateBarn(garden);

            var cycle = UnityEngine.Object.FindAnyObjectByType<EnvironmentCycle>();
            Require(cycle != null, "The day/night cycle is missing.");
            var settings = new SerializedObject(cycle);
            Require(settings.FindProperty("sun").objectReferenceValue != null, "Assign the sun.");
            Require(settings.FindProperty("moon").objectReferenceValue != null, "Assign the moon.");
            Require(settings.FindProperty("lanterns").arraySize >= 8, "The night paths need their lantern references.");
            for (int i = 0; i < settings.FindProperty("lanterns").arraySize; i++)
                Require(settings.FindProperty("lanterns").GetArrayElementAtIndex(i).objectReferenceValue != null, "A lantern reference is missing.");
            Require(UnityEngine.Object.FindAnyObjectByType<EnvironmentClockHud>() != null, "The day/time display is missing.");
            int misplaced = WorldGrounding.CountMisplaced(out string report);
            Require(misplaced == 0, misplaced + " scenery objects are not sitting on the ground. Run Align scenery to ground.\n" + report);

            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            foreach (Transform child in sceneRoot.GetComponentsInChildren<Transform>(true))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0, "Missing script on " + child.name);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
                Require(material != null && material.shader != null && material.shader.name != "Hidden/InternalErrorShader", "Missing material or shader on " + renderer.name);
        }

        // The hoe and watering can wait on the stump, and the river fills the can.
        private static void ValidateTools(GameObject player, Transform garden, Transform world)
        {
            Require(player.GetComponent<PlayerTools>() != null, "The player can't hold tools: PlayerTools is missing.");
            var hands = player.GetComponentInChildren<HandAnimator>(true);
            Require(hands != null, "The arms are missing their HandAnimator.");
            SerializedProperty models = new SerializedObject(hands).FindProperty("toolModels");
            foreach (ToolType tool in new[] { ToolType.Hoe, ToolType.WateringCan, ToolType.Hatchet })
                Require(models.arraySize > (int)tool && models.GetArrayElementAtIndex((int)tool).objectReferenceValue != null,
                    "The " + tool + " model in the player's hand is not assigned in HandAnimator.");

            Transform stump = garden.Find(ToolStumpSetup.RootName);
            Require(stump != null, "The tool stump is missing. Run Rushed Crops > Farming > Place tool stump.");
            foreach (ToolType tool in new[] { ToolType.Hoe, ToolType.WateringCan })
            {
                bool found = false;
                foreach (ToolPickup pickup in stump.GetComponentsInChildren<ToolPickup>(true))
                    if (pickup.Tool == tool && pickup.GetComponent<Collider>() != null) found = true;
                Require(found, "The " + tool + " is missing from the tool stump. Run Rushed Crops > Farming > Place tool stump.");
            }

            // One seed crate per crop.
            Transform crates = garden.Find(SeedCrateSetup.RootName);
            Require(crates != null, "The seed crates are missing. Run Rushed Crops > Farming > Place seed crates.");
            foreach (string path in FarmPlotSetup.CropAssets)
            {
                var crop = AssetDatabase.LoadAssetAtPath<CropData>(path);
                bool found = false;
                foreach (SeedCrate crate in crates.GetComponentsInChildren<SeedCrate>(true))
                {
                    var crateSettings = new SerializedObject(crate);
                    if (crate.Crop == crop && crateSettings.FindProperty("inventory").objectReferenceValue != null && crate.GetComponent<Collider>() != null)
                        found = true;
                }
                Require(found, "There is no working seed crate for " + path + ". Run Rushed Crops > Farming > Place seed crates.");
            }

            Transform water = world.Find(RiverWaterSetup.WaterPath);
            var waterCollider = water != null ? water.GetComponent<MeshCollider>() : null;
            Require(water != null && water.GetComponent<WaterSource>() != null && waterCollider != null && waterCollider.sharedMesh != null,
                "The river can't fill the watering can. Run Rushed Crops > Farming > Set up river water.");
            Require(waterCollider.excludeLayers.value == ~0, "The river's collider must touch nothing, or it would block the player.");
        }

        // The merchant needs his price list and trading window, and an EventSystem so its buttons work.
        private static void ValidateMerchant(Transform world)
        {
            const string fix = " Run Rushed Crops > Market > Set up merchant.";
            Transform placed = world.Find("04_Market/" + MerchantSetup.MerchantName);
            var merchant = placed != null ? placed.GetComponent<Merchant>() : null;
            Require(merchant != null, "The merchant is missing from the market stall." + fix);

            var settings = new SerializedObject(merchant);
            var prices = settings.FindProperty("prices").objectReferenceValue as MerchantPrices;
            Require(prices != null && prices.ForSale.Count > 0 && prices.Wanted.Count > 0,
                "The merchant needs a price list with things to sell and crops to buy (DA_MerchantPrices).");
            Require(settings.FindProperty("inventory").objectReferenceValue != null, "The merchant isn't linked to the Inventory." + fix);
            Require(settings.FindProperty("shop").objectReferenceValue != null, "The merchant isn't linked to his trading window." + fix);

            var personality = placed.GetComponent<MerchantPersonality>();
            Require(personality != null, "The merchant is missing his MerchantPersonality (greetings and reactions).");
            var mood = new SerializedObject(personality);
            Require(mood.FindProperty("player").objectReferenceValue != null && mood.FindProperty("animator").objectReferenceValue != null,
                "The merchant's personality needs the player and his Animator assigned." + fix);

            var shop = UnityEngine.Object.FindAnyObjectByType<MerchantShopUI>(FindObjectsInactive.Include);
            Require(shop != null, "The trading window (HUD_Shop) is missing." + fix);
            var window = new SerializedObject(shop);
            foreach (string part in new[] { "panel", "title", "coins", "message", "sellList", "buyList", "rowTemplate" })
                Require(window.FindProperty(part).objectReferenceValue != null, "The trading window is missing its " + part + "." + fix);
            Require(window.FindProperty("pauseWhileOpen").arraySize > 0, "The trading window doesn't pause the player's controls." + fix);
            Require(UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null,
                "The scene has no EventSystem, so the trading window's buttons can't be clicked." + fix);
        }

        // The crop field needs its settings and models, and is saved with no tilled squares.
        private static void ValidateCropField(Transform garden)
        {
            const string fix = " Run Rushed Crops > Farming > Set up crop field.";
            Transform fieldObject = garden.Find(FarmPlotSetup.FieldName);
            var field = fieldObject != null ? fieldObject.GetComponent<FarmField>() : null;
            Require(field != null, "The crop field is missing from the garden." + fix);

            var settings = new SerializedObject(fieldObject.GetComponent<FarmPlot>());
            Require(settings.FindProperty("inventory").objectReferenceValue != null, "The crop field needs the Inventory assigned." + fix);
            Require(settings.FindProperty("wetSoil").objectReferenceValue != null, "The crop field needs its wet soil material assigned." + fix);
            SerializedProperty crops = settings.FindProperty("crops");
            Require(crops.arraySize == FarmPlotSetup.CropAssets.Length, "The crop field can't grow every crop. Run Rushed Crops > Farming > Update crops in crop field.");
            for (int i = 0; i < crops.arraySize; i++)
            {
                var crop = crops.GetArrayElementAtIndex(i).objectReferenceValue as CropData;
                Require(crop != null, "The crop field has an empty crop slot.");
                for (int stage = 0; stage < crop.StageCount; stage++)
                    Require(crop.GetStageModel(stage) != null, crop.name + " is missing the model for growth stage " + (stage + 1) + ".");
            }

            var fieldSettings = new SerializedObject(field);
            var tile = fieldSettings.FindProperty("tilePrefab").objectReferenceValue as FarmTile;
            Require(tile != null && new SerializedObject(tile).FindProperty("soil").objectReferenceValue != null,
                "The crop field needs its square prefab (PFB_FarmTile) with a soil mound." + fix);
            Require(fieldSettings.FindProperty("cursor").objectReferenceValue != null, "The crop field needs the outline that follows the aim." + fix);
            Require(field.GridSize.x > 0 && field.GridSize.y > 0, "The crop field has no room for squares." + fix);
            Require(fieldObject.GetComponentsInChildren<FarmTile>(true).Length == 0, "The crop field should be saved with no tilled squares.");
        }

        // The barn is saved unrepaired, with one job per repair GameManager counts. Every job costs coins.
        private static void ValidateBarn(Transform garden)
        {
            const string fix = " Run Rushed Crops > Farming > Set up barn repair.";
            Transform barn = garden.Find(BarnRepairSetup.BarnName);
            var repair = barn != null ? barn.GetComponent<BarnRepair>() : null;
            Require(repair != null, "The barn can't be repaired: BarnRepair is missing." + fix);
            Require(new SerializedObject(repair).FindProperty("inventory").objectReferenceValue != null, "The barn needs the Inventory assigned." + fix);
            var manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            Require(manager != null, "The GameManager is missing, so barn repairs can't be counted.");
            Require(repair.Jobs.Count == manager.BarnRepairStages,
                "The barn has " + repair.Jobs.Count + " repair jobs but GameManager expects " + manager.BarnRepairStages + ".");
            Require(repair.Jobs[repair.Jobs.Count - 1].needsBossBeaten, "The last barn repair should wait for the carrot boss." + fix);

            foreach (BarnRepair.Job job in repair.Jobs)
            {
                bool costsCoins = false;
                foreach (ResourceAmount line in job.cost)
                    costsCoins |= line.type == ResourceType.Coins && line.amount > 0;
                Require(!string.IsNullOrEmpty(job.name) && costsCoins, "Every barn repair must cost coins: " + job.name + "." + fix);
                foreach (BarnRepair.Part part in job.parts)
                {
                    Require(part.part != null, job.name + " has a missing part." + fix);
                    bool looksBroken = part.part.gameObject.activeSelf == part.shownBroken
                        && Vector3.Distance(part.part.localPosition, part.brokenPosition) < 0.01f
                        && Quaternion.Angle(part.part.localRotation, Quaternion.Euler(part.brokenRotation)) < 0.5f;
                    Require(looksBroken, "The barn should be saved unrepaired, but " + part.part.name + " looks fixed." + fix);
                }
            }
        }

        // Every hotbar slot needs its picture, and every item but coins needs one in DA_ItemIcons.
        private static void ValidateItemIcons(InventoryHud hud)
        {
            var settings = new SerializedObject(hud);
            var icons = settings.FindProperty("icons").objectReferenceValue as ItemIcons;
            Require(icons != null, "The hotbar has no item pictures. Run Rushed Crops > UI > Add item icons to the hotbar.");
            SerializedProperty slots = settings.FindProperty("slots");
            for (int i = 0; i < slots.arraySize; i++)
                Require(slots.GetArrayElementAtIndex(i).FindPropertyRelative("icon").objectReferenceValue != null,
                    "Hotbar slot " + (i + 1) + " has nowhere to show a picture. Run Rushed Crops > UI > Add item icons to the hotbar.");
            foreach (ResourceType item in Enum.GetValues(typeof(ResourceType)))
                Require(item == ResourceType.Coins || icons.Get(item) != null,
                    ResourceTypes.DisplayName(item) + " has no picture. Run Rushed Crops > UI > Render item icons.");
        }

        // The hatchet swing needs its MeleeAttack, and the hatchet must be waiting in its chopping block.
        private static void ValidateMelee(GameObject player, Transform garden)
        {
            var melee = player.GetComponent<MeleeAttack>();
            Require(melee != null && new SerializedObject(melee).FindProperty("viewPoint").objectReferenceValue != null,
                "The player can't swing the hatchet. Run Rushed Crops > Combat > Set up player melee.");
            Require(new SerializedObject(player.GetComponent<PlayerTools>()).FindProperty("melee").objectReferenceValue == melee,
                "PlayerTools needs the player's MeleeAttack assigned. Run Rushed Crops > Combat > Set up player melee.");

            Transform group = garden.Find(MeleeSetup.RootName);
            Require(group != null, "The hatchet block is missing. Run Rushed Crops > Combat > Place hatchet block.");
            bool hatchet = false;
            foreach (ToolPickup pickup in group.GetComponentsInChildren<ToolPickup>(true))
                if (pickup.Tool == ToolType.Hatchet && pickup.GetComponent<Collider>() != null) hatchet = true;
            Require(hatchet, "The hatchet is missing from its chopping block. Run Rushed Crops > Combat > Place hatchet block.");
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
