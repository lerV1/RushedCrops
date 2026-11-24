using System;
using System.Collections.Generic;
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
            Physics.SyncTransforms(); // colliders where their objects really stand, even right after a rebuild
            Validate(scene);
            Debug.Log("World validation passed: landmarks, player, crop field, tools, river water, merchant, hotbar pictures, barn repairs, gathering spots, bosses, mini crops, edge, scenery, effects, colliders, materials and environment references.");
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
            ValidateGathering(root.transform, player);
            ValidateBosses(player);
            ValidateMiniCrops(garden);
            ValidateBoundary(root.transform, player);
            ValidateScenery(root.transform);
            ValidateFeel(player);
            ValidateAudio(root.transform);

            var cycle =UnityEngine.Object.FindAnyObjectByType<EnvironmentCycle>();
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
            // Every crop plus every boss seed.
            Require(crops.arraySize == FarmPlotSetup.CropAssets.Length + FarmPlotSetup.BossSeedAssets.Length, "The crop field can't grow every crop. Run Rushed Crops > Farming > Update crops in crop field.");
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

        // No boss stands on the map: every boss grows from a boss seed. Each grown boss can be hit and fights back
        // (attacks with warning patches, shots for any spitting), and exactly as many different bosses as
        // GameManager counts are worth credit toward winning. The player's HUD has the boss bar.
        private static void ValidateBosses(GameObject player)
        {
            const string fix = " Run Rushed Crops > Farming > Set up boss seeds.";
            CropBoss[] onMap = UnityEngine.Object.FindObjectsByType<CropBoss>(FindObjectsInactive.Include);
            Require(onMap.Length == 0, "The map should have no bosses (they grow from boss seeds), but it has " + onMap.Length + ".");

            var manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            Require(manager != null, "The scene needs a GameManager.");
            var winNames = new List<string>();
            foreach (string seedPath in FarmPlotSetup.BossSeedAssets)
            {
                var seed = AssetDatabase.LoadAssetAtPath<CropData>(seedPath);
                Require(seed != null && seed.GrownBoss != null, seedPath + " is missing or grows no boss." + fix);
                CropBoss boss = seed.GrownBoss;
                if (boss.CountsTowardVictory)
                {
                    Require(!winNames.Contains(boss.DisplayName), "Two bosses that count toward winning are both called " + boss.DisplayName + "." + fix);
                    winNames.Add(boss.DisplayName);
                }
                var settings = new SerializedObject(boss);
                Require(settings.FindProperty("model").objectReferenceValue != null && settings.FindProperty("dropPrefab").objectReferenceValue != null,
                    boss.name + " is missing its model or seed drop." + fix);
                Require(boss.GetComponent<Collider>() != null, boss.name + " has nothing for the hatchet to hit." + fix);
                var combat = boss.GetComponent<BossCombat>();
                Require(combat != null && combat.Attacks.Count > 0, boss.name + " can't fight back." + fix);
                var combatSettings = new SerializedObject(combat);
                Require(combatSettings.FindProperty("warningMaterial").objectReferenceValue != null, boss.name + " has no warning patches." + fix);
                foreach (BossCombat.Attack attack in combat.Attacks)
                    Require(attack.kind != BossCombat.AttackKind.Spit || combatSettings.FindProperty("projectilePrefab").objectReferenceValue != null,
                        boss.name + " spits but has nothing to spit." + fix);
            }
            Require(winNames.Count == manager.BossCount,
                "GameManager needs " + manager.BossCount + " bosses to win, but " + winNames.Count + " grown bosses count toward winning." + fix);
            Transform hud = player.transform.Find(BossHudSetup.CanvasName);
            Require(hud != null && hud.GetComponent<BossHealthHud>() != null, "The boss health bar is missing. Run Rushed Crops > UI > Set up boss health bar.");
        }

        // The crop field has a spawner with one evil mini crop per crop, each able to be hit and to drop its crop.
        private static void ValidateMiniCrops(Transform garden)
        {
            const string fix = " Run Rushed Crops > Combat > Set up mini crops.";
            var spawner = garden.GetComponentInChildren<MiniCropSpawner>(true);
            Require(spawner != null, "The crop field has no mini crop spawner." + fix);
            var settings = new SerializedObject(spawner);
            Require(settings.FindProperty("inventory").objectReferenceValue != null && settings.FindProperty("player").objectReferenceValue != null,
                "The mini crop spawner is missing the Inventory or the player." + fix);
            Require(spawner.CropPrefabs.Count == FarmPlotSetup.CropAssets.Length,
                "The mini crop spawner needs one prefab per crop." + fix);
            foreach (MiniCrop crop in spawner.CropPrefabs)
            {
                Require(crop != null && crop.Crop != null, "A mini crop prefab or its crop is missing." + fix);
                Require(crop.GetComponent<Collider>() != null && crop.GetComponent<Knockback>() != null,
                    crop.name + " has nothing for the hatchet to hit or push." + fix);
                Require(new SerializedObject(crop).FindProperty("dropPrefab").objectReferenceValue != null, crop.name + " has nothing to drop." + fix);
            }

            // Every boss seed is dropped by a mini crop, grows a boss, and can be planted in the crop field.
            const string seedFix = " Run Rushed Crops > Farming > Set up boss seeds.";
            var plot = garden.GetComponentInChildren<FarmPlot>(true);
            int droppers = 0;
            foreach (MiniCrop crop in spawner.CropPrefabs)
            {
                if (crop.BossSeed == null) continue;
                droppers++;
                Require(crop.BossSeed.GrownBoss != null, crop.BossSeed.name + " doesn't grow a boss." + seedFix);
                Require(plot != null && plot.CanGrow(crop.BossSeed), "The crop field can't grow " + crop.BossSeed.name + "." + seedFix);
            }
            Require(droppers == FarmPlotSetup.BossSeedAssets.Length, "Only " + droppers + " mini crops drop a boss seed." + seedFix);
        }

        // Every sound has at least one clip, the ambience loops and the player's sounds are wired, and the
        // river can be heard along its course.
        private static void ValidateAudio(Transform world)
        {
            const string fix = " Run Rushed Crops > Audio > Set up sounds.";
            var audio = UnityEngine.Object.FindAnyObjectByType<GameAudio>();
            Require(audio != null, "The scene has no GameAudio, so the game is silent." + fix);
            var settings = new SerializedObject(audio);
            var bank = settings.FindProperty("bank").objectReferenceValue as SoundBank;
            Require(bank != null, "GameAudio has no sound bank." + fix);
            foreach (Sfx sound in Enum.GetValues(typeof(Sfx)))
            {
                SoundBank.Entry entry = bank.Get(sound);
                Require(entry != null && entry.clips.Length > 0 && Array.TrueForAll(entry.clips, clip => clip != null), "The " + sound + " sound has no clips." + fix);
            }
            foreach (string field in new[] { "birds", "crickets", "wind", "tools", "playerHealth", "feel" })
                Require(settings.FindProperty(field).objectReferenceValue != null, "GameAudio is missing its " + field + "." + fix);
            Require(UnityEngine.Object.FindAnyObjectByType<AudioListener>() != null, "Nothing in the scene listens: the camera needs an AudioListener.");

            var river = world.GetComponentInChildren<RiverSound>(true);
            Require(river != null && river.GetComponent<AudioSource>().clip != null && new SerializedObject(river).FindProperty("course").arraySize >= 5,
                "The river makes no sound. Rebuild the map.");
        }

        // Hits throw chips, dirt and leaves, and the camera bobs, dips on landing and shakes when the player is hurt.
        private static void ValidateFeel(GameObject player)
        {
            const string fix = " Run Rushed Crops > Effects > Set up game feel.";
            var effects = UnityEngine.Object.FindAnyObjectByType<GameEffects>();
            Require(effects != null, "The hit effects (GameEffects) are missing from the scene." + fix);
            var settings = new SerializedObject(effects);
            Require(settings.FindProperty("glow").objectReferenceValue != null && settings.FindProperty("soft").objectReferenceValue != null,
                "GameEffects needs both particle materials." + fix);
            var feel = player.GetComponentInChildren<CameraFeel>(true);
            Require(feel != null, "The camera has no CameraFeel (bob, landing dip, hurt shake)." + fix);
            var feelSettings = new SerializedObject(feel);
            Require(feelSettings.FindProperty("body").objectReferenceValue != null && feelSettings.FindProperty("health").objectReferenceValue != null,
                "CameraFeel needs the player's Rigidbody and Health." + fix);
        }

        // The living scenery: the windmill's sails turn, the camp's fire burns and lights the night, clouds
        // drift, pollen and fireflies fill the air, and the still scenery is set up to be drawn in batches.
        private static void ValidateScenery(Transform world)
        {
            const string fix = " Rebuild the map.";
            Transform mill = world.Find(FarmWorldBuilder.FarmGroup + "/" + FarmWorldBuilder.WindmillName);
            Require(mill != null && mill.GetComponentInChildren<Spinner>(true) != null, "The windmill or its turning sails are missing." + fix);
            Transform fire = world.Find("06_LoggingCamp/" + FarmWorldBuilder.CampfireName);
            Require(fire != null && fire.GetComponent<FireFlicker>() != null && fire.GetComponentsInChildren<ParticleSystem>(true).Length >= 2
                && fire.GetComponentInChildren<Light>(true) != null, "The campfire is missing its flames, smoke or light." + fix);
            var drift = world.GetComponentInChildren<CloudDrift>(true);
            Require(drift != null && drift.transform.childCount >= 10, "The sky has no drifting clouds." + fix);
            var life = world.GetComponentInChildren<AmbientLife>(true);
            Require(life != null && life.GetComponentsInChildren<ParticleSystem>(true).Length == 2, "The pollen and fireflies are missing." + fix);
            foreach (ParticleSystemRenderer particles in world.GetComponentsInChildren<ParticleSystemRenderer>(true))
                Require(particles.sharedMaterial != null && particles.sharedMaterial.name.StartsWith("MAT_Particle"),
                    particles.name + " should use a particle material, not " + (particles.sharedMaterial != null ? particles.sharedMaterial.name : "none") + "." + fix);

            int still = 0;
            foreach (Renderer renderer in world.Find("07_Forest").GetComponentsInChildren<Renderer>(true))
                if (GameObjectUtility.AreStaticEditorFlagsSet(renderer.gameObject, StaticEditorFlags.BatchingStatic)) still++;
            Require(still > 100, "The forest isn't set up for static batching, so it costs far more draw calls." + fix);
        }

        // A wall runs all the way round the valley rim, and everything worth visiting stands well inside it.
        // The quarry's rock faces are left out: they are the valley's wall there.
        private static void ValidateBoundary(Transform world, GameObject player)
        {
            const string fix = " Move it, or the rim in FarmWorldBuilder, and rebuild the map.";
            const float room = 6f;
            int walls = 0;
            foreach (BoxCollider wall in world.Find("08_Boundary").GetComponentsInChildren<BoxCollider>(true))
                if (wall.enabled && !wall.isTrigger) walls++;
            Require(walls >= 40, "The valley rim has only " + walls + " wall pieces; the player could walk out of the map. Rebuild the map.");

            var places = new List<Transform> { player.transform };
            foreach (string group in new[] { FarmWorldBuilder.FarmGroup, "04_Market", "05_Quarry", "06_LoggingCamp" })
            foreach (Transform child in world.Find(group))
                if (!child.name.StartsWith("QuarryFace")) places.Add(child);
            places.AddRange(Array.ConvertAll(world.GetComponentsInChildren<ResourceNode>(true), node => node.transform));
            foreach (Transform place in places)
            {
                float inside = -FarmWorldBuilder.RimDistance(place.position);
                Require(inside >= room, place.name + " is only " + inside.ToString("F1") + " m inside the map's edge." + fix);
            }
            Vector2 field = FarmWorldBuilder.Garden, half = FarmWorldBuilder.GardenHalfSize;
            foreach (Vector2 corner in new[] { field + half, field - half, field + new Vector2(half.x, -half.y), field + new Vector2(-half.x, half.y) })
                Require(-FarmWorldBuilder.RimDistance(new Vector3(corner.x, 0f, corner.y)) >= room, "A corner of the crop field is too close to the map's edge." + fix);
        }

        // The pickaxe waits in the quarry and fits the hand. Trees to chop stand round the logging camp and
        // rocks to mine lie in the quarry; each is saved whole, is solid, and has room to walk round it.
        private static void ValidateGathering(Transform world, GameObject player)
        {
            const string pickaxeFix = " Run Rushed Crops > Tools > Set up pickaxe.";
            Transform pickaxe = world.Find("05_Quarry/" + PickaxeSetup.PickupName);
            var pickup = pickaxe != null ? pickaxe.GetComponent<ToolPickup>() : null;
            Require(pickup != null && new SerializedObject(pickup).FindProperty("tool").enumValueIndex == (int)ToolType.Pickaxe,
                "The pickaxe is missing from the quarry." + pickaxeFix);
            var hands = player.GetComponentInChildren<HandAnimator>(true);
            SerializedProperty models = new SerializedObject(hands).FindProperty("toolModels");
            Require(models.arraySize > (int)ToolType.Pickaxe && models.GetArrayElementAtIndex((int)ToolType.Pickaxe).objectReferenceValue != null,
                "The player can't hold the pickaxe." + pickaxeFix);

            Transform terrain = world.Find("00_Terrain");
            ValidateSpots(world.Find("06_LoggingCamp/" + GatheringSetup.TreesName), terrain, ToolType.Hatchet, ResourceType.Wood, 6, "choppable trees");
            ValidateSpots(world.Find("05_Quarry/" + GatheringSetup.RocksName), terrain, ToolType.Pickaxe, ResourceType.Stone, 4, "mineable rocks");
        }

        private static void ValidateSpots(Transform group, Transform terrain, ToolType tool, ResourceType item, int atLeast, string label)
        {
            const string fix = " Run Rushed Crops > World > Place gathering spots.";
            Require(group != null, "The " + label + " are missing." + fix);
            ResourceNode[] spots = group.GetComponentsInChildren<ResourceNode>(true);
            Require(spots.Length >= atLeast, "There should be at least " + atLeast + " " + label + ", but there are " + spots.Length + "." + fix);
            foreach (ResourceNode spot in spots)
            {
                var settings = new SerializedObject(spot);
                var whole = settings.FindProperty("whole").objectReferenceValue as Transform;
                var broken = settings.FindProperty("broken").objectReferenceValue as GameObject;
                Require(settings.FindProperty("inventory").objectReferenceValue != null && settings.FindProperty("dropPrefab").objectReferenceValue != null
                    && whole != null && broken != null, spot.name + " is missing its Inventory, drop or looks." + fix);
                Require(spot.Tool == tool && spot.Item == item, spot.name + " should give " + item + " for the " + tool + "." + fix);
                Require(whole.gameObject.activeSelf && !broken.activeSelf, spot.name + " should be saved whole." + fix);

                // Solid, so the player walks round it, not through it.
                bool solid = false;
                Bounds body = new Bounds(spot.transform.position, Vector3.zero);
                foreach (Collider part in whole.GetComponentsInChildren<Collider>())
                {
                    if (!part.enabled || part.isTrigger) continue;
                    if (!solid) body = part.bounds; else body.Encapsulate(part.bounds);
                    solid = true;
                }
                Require(solid, spot.name + " isn't solid, so the player would walk through it." + fix);

                // At least a metre of open ground all round it.
                float reach = Mathf.Max(body.extents.x, body.extents.z) + 1f;
                Vector3 ground = spot.transform.position;
                foreach (Collider other in Physics.OverlapCapsule(ground + Vector3.up * (reach + 0.15f), ground + Vector3.up * (reach + 1.2f), reach, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (other.transform.IsChildOf(spot.transform) || other.transform.IsChildOf(terrain) || other.gameObject.layer == 4) continue;
                    Require(false, spot.name + " leaves no room to walk round it: " + other.name + " is too close." + fix);
                }
            }
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
            Require(repair.Jobs[repair.Jobs.Count - 1].needsBossBeaten, "The last barn repair should wait for a boss to be beaten." + fix);

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
