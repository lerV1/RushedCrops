using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RushedCrops.Editor
{
    // Sets up the game feel: the GameEffects bursts in SC_Farm and the camera's bob, landing dip and hurt
    // shake on the player prefab. Safe to run again.
    public static class FeelSetup
    {
        public const string RootName = "GAMEPLAY_Feel";
        private const string CameraPath = "CameraRoot/Main Camera";

        [MenuItem("Rushed Crops/Effects/Set up game feel")]
        public static void SetUp()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Set up the game feel outside Play.");
            if (EditorSceneManager.GetActiveScene().path != FarmWorldBuilder.ScenePath)
                throw new InvalidOperationException("Open SC_Farm first.");

            var root = GameObject.Find(RootName);
            if (root == null) root = new GameObject(RootName);
            var effects = root.GetComponent<GameEffects>();
            if (effects == null) effects = root.AddComponent<GameEffects>();
            effects.Configure(EffectMaterials.Glow(), EffectMaterials.Soft());
            EditorUtility.SetDirty(effects);

            GameObject player = PrefabUtility.LoadPrefabContents(HandToolFitter.PlayerPrefab);
            try
            {
                Transform camera = player.transform.Find(CameraPath);
                if (camera == null) throw new InvalidOperationException("PFB_Player has no " + CameraPath + ".");
                var feel = camera.GetComponent<CameraFeel>();
                if (feel == null) feel = camera.gameObject.AddComponent<CameraFeel>();
                feel.Configure(player.GetComponent<Rigidbody>(), player.GetComponent<Health>());
                PrefabUtility.SaveAsPrefabAsset(player, HandToolFitter.PlayerPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(player);
            }

            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("Game feel set up: hit effects in the scene, camera bob, landing dip and hurt shake on the player.");
        }
    }
}
