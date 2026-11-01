using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RushedCrops.Editor
{
    // Gives the player Health and a RespawnController, then places the respawn checkpoints in SC_Farm:
    //   Farm checkpoint    - the starting checkpoint at the player's spawn
    //   Market checkpoint  - on the path where it reaches the market
    // Everything sits under its own scene root, so "Rebuild map" (which replaces WORLD_RushedCrops) keeps it.
    // Safe to run again: it replaces its own objects and keeps the player's other components.
    public static class RespawnSetup
    {
        public const string RootName = "GAMEPLAY_Respawn";
        public const string StartCheckpointName = "Farm checkpoint";
        private const string PlayerPrefabPath = "Assets/_Game/Prefabs/Player/PFB_Player.prefab";
        private const int GroundLayer = 6;
        // The player's look ray skips this layer, so the trigger boxes never block aiming at plots or tools.
        private const int IgnoreRaycastLayer = 2;
        private const int PlayerMaxHealth = 100;

        private static readonly Vector3 MarketCheckpointSpot = new Vector3(-4f, 0f, -7f);

        [MenuItem("Rushed Crops/Player/Set up health and respawn")]
        public static void SetUpFromMenu()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Set up respawning outside Play.");
            if (EditorSceneManager.GetActiveScene().path != FarmWorldBuilder.ScenePath)
                throw new InvalidOperationException("Open SC_Farm before setting up respawning.");
            SetUp();
        }

        // For Unity's command line: -executeMethod RushedCrops.Editor.RespawnSetup.SetUpInBatch
        public static void SetUpInBatch()
        {
            EditorSceneManager.OpenScene(FarmWorldBuilder.ScenePath);
            SetUp();
        }

        public static void SetUp()
        {
            AddPlayerComponentsToPrefab();

            GameObject player = GameObject.Find("Player");
            if (player == null || player.GetComponent<RespawnController>() == null)
                throw new InvalidOperationException("SC_Farm needs the Player prefab instance before respawning can be set up.");

            GameObject old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);
            var root = new GameObject(RootName).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Set up respawn");

            // Spawn points keep the player's starting height above the ground, so they don't appear inside it.
            Vector3 playerStart = player.transform.position;
            float spawnHeight = Mathf.Max(playerStart.y - Ground(playerStart).y, 1.1f);

            Transform farmSpawn = MakeCheckpoint(root, StartCheckpointName, Ground(playerStart), spawnHeight);
            MakeCheckpoint(root, "Market checkpoint", Ground(MarketCheckpointSpot), spawnHeight);

            var respawn = new SerializedObject(player.GetComponent<RespawnController>());
            respawn.FindProperty("startingCheckpoint").objectReferenceValue = farmSpawn;
            respawn.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.GetComponent<RespawnController>());

            EditorSceneManager.MarkSceneDirty(player.scene);
            EditorSceneManager.SaveScene(player.scene);
            Debug.Log("Respawn set up: player Health and RespawnController, and the farm and market checkpoints.");
        }

        // The components go on the prefab, so every copy of the player has them.
        private static void AddPlayerComponentsToPrefab()
        {
            GameObject prefab = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Health health = prefab.GetComponent<Health>();
                if (health == null)
                {
                    health = prefab.AddComponent<Health>();
                    var settings = new SerializedObject(health);
                    settings.FindProperty("maxHealth").intValue = PlayerMaxHealth;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
                if (prefab.GetComponent<RespawnController>() == null) prefab.AddComponent<RespawnController>();
                PrefabUtility.SaveAsPrefabAsset(prefab, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        // A trigger box on the ground, with the spawn point raised to standing height.
        private static Transform MakeCheckpoint(Transform parent, string name, Vector3 groundPoint, float spawnHeight)
        {
            var checkpoint = new GameObject(name);
            checkpoint.layer = IgnoreRaycastLayer;
            checkpoint.transform.SetParent(parent, false);
            checkpoint.transform.position = groundPoint;

            var box = checkpoint.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(4f, 3f, 4f);
            box.center = new Vector3(0f, 1.5f, 0f);

            var spawn = new GameObject("Spawn point").transform;
            spawn.SetParent(checkpoint.transform, false);
            spawn.localPosition = new Vector3(0f, spawnHeight, 0f);

            var settings = new SerializedObject(checkpoint.AddComponent<Checkpoint>());
            settings.FindProperty("spawnPoint").objectReferenceValue = spawn;
            settings.ApplyModifiedPropertiesWithoutUndo();
            return spawn;
        }

        private static Vector3 Ground(Vector3 at)
        {
            Vector3 top = new Vector3(at.x, 50f, at.z);
            return Physics.Raycast(top, Vector3.down, out RaycastHit hit, 100f, 1 << GroundLayer)
                ? hit.point
                : new Vector3(at.x, 0f, at.z);
        }
    }
}
