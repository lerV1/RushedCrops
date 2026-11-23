using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RushedCrops.Editor
{
    // Sets up the pickaxe: the game's copy of the pack model (PFB_Pickaxe01), the model in the player's hand,
    // and the pickaxe lying by the path into the quarry for the player to pick up. The map builder calls Place.
    public static class PickaxeSetup
    {
        public const string PickupName = "Pickaxe pickup";
        public const string PrefabPath = "Assets/_Game/Prefabs/Tools/PFB_Pickaxe01.prefab";
        private const string PackPickaxe = "Assets/Holotna/Medieval Rural/Prefabs/Tools/Pickaxe01.prefab";
        private const string HandPath = "CameraRoot/Main Camera/HandsRoot/HandRight/Rig/Forearm/Hand";
        private static readonly Vector3 Spot = new Vector3(5f, 0f, 41.8f); // at the quarry's mouth, by the lantern where the path comes in
        private const float PickupScale = 1.3f;

        [MenuItem("Rushed Crops/Tools/Set up pickaxe")]
        public static void SetUpAll()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Set up the pickaxe outside Play.");
            MakePrefab();
            SetUpPlayer();
            Transform quarry = GameObject.Find(FarmWorldBuilder.RootName)?.transform.Find("05_Quarry");
            if (quarry == null) throw new InvalidOperationException("Open SC_Farm with the map built first.");
            Place(quarry);
            EditorSceneManager.MarkSceneDirty(quarry.gameObject.scene);
            Debug.Log("The pickaxe lies in the quarry and fits the player's hand.");
        }

        // PFB_Pickaxe01, a copy of the pack's pickaxe kept with the game's other tools.
        public static GameObject MakePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PackPickaxe));
            try
            {
                return PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        // The pickaxe model in the right hand, hidden until the pickaxe is selected.
        public static void SetUpPlayer()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(HandToolFitter.PlayerPrefab);
            try
            {
                Transform hand = root.transform.Find(HandPath);
                var hands = root.GetComponentInChildren<HandAnimator>(true);
                if (hand == null || hands == null) throw new InvalidOperationException("PFB_Player is missing its hand or HandAnimator.");

                Transform old = hand.Find("Pickaxe");
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                MeshFilter source = ToolStumpSetup.FirstLod0(MakePrefab());
                var model = new GameObject("Pickaxe", typeof(MeshFilter), typeof(MeshRenderer));
                model.transform.SetParent(hand, false);
                model.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                var renderer = model.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = source.GetComponent<MeshRenderer>().sharedMaterial;
                // Lit and layered like the other held tools.
                Transform hoe = hand.Find("Hoe");
                if (hoe != null)
                {
                    model.layer = hoe.gameObject.layer;
                    var hoeRenderer = hoe.GetComponent<MeshRenderer>();
                    renderer.shadowCastingMode = hoeRenderer.shadowCastingMode;
                    renderer.receiveShadows = hoeRenderer.receiveShadows;
                }
                model.SetActive(false);
                HandToolFitter.Fit(root);

                var settings = new SerializedObject(hands);
                SerializedProperty models = settings.FindProperty("toolModels");
                if (models.arraySize <= (int)ToolType.Pickaxe) models.arraySize = (int)ToolType.Pickaxe + 1;
                models.GetArrayElementAtIndex((int)ToolType.Pickaxe).objectReferenceValue = model;
                settings.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, HandToolFitter.PlayerPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // The pickaxe lying flat by the path into the quarry. Replaces the pack's decorative one if it is still there.
        public static GameObject Place(Transform quarry)
        {
            foreach (string name in new[] { "Pickaxe01", PickupName })
                for (Transform old = quarry.Find(name); old != null; old = quarry.Find(name))
                    Undo.DestroyObjectImmediate(old.gameObject);

            MeshFilter source = ToolStumpSetup.FirstLod0(MakePrefab());
            var pickaxe = new GameObject(PickupName, typeof(MeshFilter), typeof(MeshRenderer));
            Undo.RegisterCreatedObjectUndo(pickaxe, "Place pickaxe");
            pickaxe.transform.SetParent(quarry, false);
            pickaxe.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            pickaxe.GetComponent<MeshRenderer>().sharedMaterial = source.GetComponent<MeshRenderer>().sharedMaterial;
            pickaxe.transform.rotation = Quaternion.Euler(0f, 35f, 90f); // handle lying along the ground
            pickaxe.transform.localScale = Vector3.one * PickupScale;
            Vector3 ground = ToolStumpSetup.Ground(Spot);
            pickaxe.transform.position = ground;
            float bottom = pickaxe.GetComponent<MeshRenderer>().bounds.min.y;
            pickaxe.transform.position += Vector3.up * (ground.y - bottom + 0.005f);
            ToolStumpSetup.AddPickup(pickaxe, ToolType.Pickaxe, "pickaxe");
            return pickaxe;
        }
    }
}
