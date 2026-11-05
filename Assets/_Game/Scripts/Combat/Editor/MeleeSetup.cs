using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RushedCrops.Editor
{
    // Sets up melee combat:
    //   the player prefab gets a hatchet model in the right hand and a MeleeAttack,
    //   and SC_Farm gets a hatchet stuck in a chopping block for the player to pick up.
    // The map builder calls Place, and both steps can be run again from the menu.
    public static class MeleeSetup
    {
        public const string RootName = "Hatchet block";
        private const string PlayerPrefab = "Assets/_Game/Prefabs/Player/PFB_Player.prefab";
        private const string HatchetPrefab = "Assets/_Game/Prefabs/Tools/PFB_Hatchet01.prefab";
        private const string HandPath = "CameraRoot/Main Camera/HandsRoot/HandRight/Rig/Forearm/Hand";
        private const string CameraPath = "CameraRoot/Main Camera";
        private const string Pack = "Assets/Holotna/Medieval Rural/Prefabs/";
        private static readonly Vector3 Spot = new Vector3(-34.5f, 0f, -17f); // open grass west of the tool stump
        private const float FacingYaw = 41f;                                  // looks toward the spawn point

        [MenuItem("Rushed Crops/Combat/Set up player melee")]
        public static void SetUpPlayer()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Set up the player outside Play.");

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                Transform hand = root.transform.Find(HandPath);
                Transform view = root.transform.Find(CameraPath);
                var hands = root.GetComponentInChildren<HandAnimator>(true);
                var tools = root.GetComponent<PlayerTools>();
                if (hand == null || view == null || hands == null || tools == null)
                    throw new InvalidOperationException("PFB_Player is missing its hand, camera, HandAnimator or PlayerTools.");

                // The hatchet model in the hand, hidden until the hatchet is selected.
                Transform old = hand.Find("Hatchet");
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                MeshFilter source = ToolStumpSetup.FirstLod0(AssetDatabase.LoadAssetAtPath<GameObject>(HatchetPrefab));
                var model = new GameObject("Hatchet", typeof(MeshFilter), typeof(MeshRenderer));
                model.transform.SetParent(hand, false);
                model.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                var renderer = model.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = source.GetComponent<MeshRenderer>().sharedMaterial;
                // Match the hoe, so the held tools are lit and layered the same way.
                Transform hoe = hand.Find("Hoe");
                if (hoe != null)
                {
                    model.layer = hoe.gameObject.layer;
                    var hoeRenderer = hoe.GetComponent<MeshRenderer>();
                    renderer.shadowCastingMode = hoeRenderer.shadowCastingMode;
                    renderer.receiveShadows = hoeRenderer.receiveShadows;
                }
                model.SetActive(false);
                // Line the handle up through the fist, the same way as the hoe and the watering can.
                HandToolFitter.Fit(root);

                var handSettings = new SerializedObject(hands);
                SerializedProperty models = handSettings.FindProperty("toolModels");
                if (models.arraySize <= (int)ToolType.Hatchet) models.arraySize = (int)ToolType.Hatchet + 1;
                models.GetArrayElementAtIndex((int)ToolType.Hatchet).objectReferenceValue = model;
                handSettings.ApplyModifiedPropertiesWithoutUndo();

                // The swing itself. Its numbers are left alone if it is already there.
                var melee = root.GetComponent<MeleeAttack>();
                if (melee == null) melee = root.AddComponent<MeleeAttack>();
                var meleeSettings = new SerializedObject(melee);
                meleeSettings.FindProperty("viewPoint").objectReferenceValue = view;
                meleeSettings.ApplyModifiedPropertiesWithoutUndo();

                var toolSettings = new SerializedObject(tools);
                toolSettings.FindProperty("melee").objectReferenceValue = melee;
                toolSettings.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log("The player can now swing the hatchet.");
        }

        [MenuItem("Rushed Crops/Combat/Place hatchet block")]
        public static void PlaceInScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Place the hatchet block outside Play.");
            var farm = GameObject.Find(FarmWorldBuilder.RootName)?.transform.Find(FarmWorldBuilder.FarmGroup);
            if (farm == null) throw new InvalidOperationException("Open SC_Farm with the farm map built first.");
            Place(farm);
            EditorSceneManager.MarkSceneDirty(farm.gameObject.scene);
        }

        public static GameObject Place(Transform parent)
        {
            Transform old = parent.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Place hatchet block");
            root.transform.SetParent(parent, false);
            root.transform.position = ToolStumpSetup.Ground(Spot);
            root.transform.rotation = Quaternion.Euler(0f, FacingYaw, 0f);

            // The hatchet, blade down in a chopping block.
            var block = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "Wood/ChoppingBlock01.prefab"), root.transform);
            block.transform.position = ToolStumpSetup.Ground(root.transform.TransformPoint(new Vector3(1.4f, 0f, 0.2f)));
            block.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
            float top = TopOf(block);

            MeshFilter source = ToolStumpSetup.FirstLod0(AssetDatabase.LoadAssetAtPath<GameObject>(HatchetPrefab));
            var hatchet = new GameObject("Hatchet pickup", typeof(MeshFilter), typeof(MeshRenderer));
            hatchet.transform.SetParent(root.transform, false);
            hatchet.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            hatchet.GetComponent<MeshRenderer>().sharedMaterial = source.GetComponent<MeshRenderer>().sharedMaterial;
            Quaternion lean = Quaternion.Euler(0f, FacingYaw - 60f, 0f) * Quaternion.AngleAxis(200f, Vector3.forward); // blade (+Y) down, tilted 20 degrees
            hatchet.transform.rotation = lean;
            Vector3 blockTop = new Vector3(block.transform.position.x, top, block.transform.position.z);
            hatchet.transform.position = blockTop - lean * new Vector3(0f, 0.2f, 0f);
            ToolStumpSetup.AddPickup(hatchet, ToolType.Hatchet, "hatchet");
            return root;
        }

        private static float TopOf(GameObject go)
        {
            float top = float.MinValue;
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>())
                if (renderer.enabled) top = Mathf.Max(top, renderer.bounds.max.y);
            return top;
        }
    }
}
