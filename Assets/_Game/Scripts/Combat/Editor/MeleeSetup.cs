using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RushedCrops.Editor
{
    // Sets up melee combat:
    //   the player prefab gets a hatchet model in the right hand and a MeleeAttack,
    //   and SC_Farm gets a straw practice dummy with a hatchet stuck in a chopping block beside it.
    // The map builder calls Place, and both steps can be run again from the menu.
    public static class MeleeSetup
    {
        public const string RootName = "Practice dummy";
        public const string DummyName = "Dummy";
        private const string PlayerPrefab = "Assets/_Game/Prefabs/Player/PFB_Player.prefab";
        private const string HatchetPrefab = "Assets/_Game/Prefabs/Tools/PFB_Hatchet01.prefab";
        private const string HandPath = "CameraRoot/Main Camera/HandsRoot/HandRight/Rig/Forearm/Hand";
        private const string CameraPath = "CameraRoot/Main Camera";
        private const string Pack = "Assets/Holotna/Medieval Rural/Prefabs/";
        private const string Cartoon = "Assets/_Game/Materials/Cartoon/";
        private static readonly Vector3 Spot = new Vector3(-34.5f, 0f, -17f); // open grass west of the tool stump
        private const float FacingYaw = 41f;                                  // looks toward the spawn point

        // The hatchet held in the right hand, set by eye to match the hoe's grip.
        private static readonly Vector3 HandPosition = new Vector3(-0.03f, 0.1f, 0.03f);
        private static readonly Vector3 HandRotation = new Vector3(327f, 102f, 354f);

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
                model.transform.localPosition = HandPosition;
                model.transform.localRotation = Quaternion.Euler(HandRotation);
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

        [MenuItem("Rushed Crops/Combat/Place practice dummy")]
        public static void PlaceInScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Place the dummy outside Play.");
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
            Undo.RegisterCreatedObjectUndo(root, "Place practice dummy");
            root.transform.SetParent(parent, false);
            root.transform.position = ToolStumpSetup.Ground(Spot);
            root.transform.rotation = Quaternion.Euler(0f, FacingYaw, 0f);

            BuildDummy(root.transform);

            // The hatchet, blade down in a chopping block beside the dummy.
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

        // A straw dummy on a post: health, a hit flash, knockback, and a reset on Interact.
        // Its body, arms, head and base are separate colliders on one Health, so a swing that
        // touches several of them must still count as one hit.
        private static void BuildDummy(Transform parent)
        {
            var dummy = new GameObject(DummyName);
            dummy.transform.SetParent(parent, false);
            dummy.transform.localPosition = Vector3.up * 0.02f;

            var body = dummy.AddComponent<Rigidbody>();
            body.mass = 20f;
            body.linearDamping = 0f;
            body.constraints = RigidbodyConstraints.FreezeRotation; // pushed across the ground, never tipped over
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var health = dummy.AddComponent<Health>();
            var healthSettings = new SerializedObject(health);
            healthSettings.FindProperty("maxHealth").intValue = 100;
            healthSettings.ApplyModifiedPropertiesWithoutUndo();
            dummy.AddComponent<Knockback>();
            dummy.AddComponent<HitFlash>();
            dummy.AddComponent<PracticeDummy>();

            Material straw = Load("Straw"), canvas = Load("Canvas"), lightWood = Load("LightWood"), darkWood = Load("DarkWood");
            Part(dummy.transform, "Base", PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0f), new Vector3(0.7f, 0.05f, 0.7f), darkWood, Collision.Box);
            Part(dummy.transform, "Post", PrimitiveType.Cylinder, new Vector3(0f, 0.6f, 0f), new Vector3(0.09f, 0.55f, 0.09f), lightWood, Collision.None);
            Part(dummy.transform, "Body", PrimitiveType.Capsule, new Vector3(0f, 1.2f, 0f), new Vector3(0.5f, 0.45f, 0.4f), straw, Collision.Keep);
            Part(dummy.transform, "Arms", PrimitiveType.Cube, new Vector3(0f, 1.42f, 0f), new Vector3(1.15f, 0.13f, 0.13f), straw, Collision.Keep);
            Part(dummy.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.86f, 0f), Vector3.one * 0.4f, canvas, Collision.Keep);
            Part(dummy.transform, "Left eye", PrimitiveType.Sphere, new Vector3(-0.07f, 1.9f, 0.18f), Vector3.one * 0.06f, darkWood, Collision.None);
            Part(dummy.transform, "Right eye", PrimitiveType.Sphere, new Vector3(0.07f, 1.9f, 0.18f), Vector3.one * 0.06f, darkWood, Collision.None);
        }

        private enum Collision { None, Keep, Box }

        private static void Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material material, Collision collision)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (collision != Collision.Keep)
            {
                // A flat cylinder's capsule collider is rounded underneath, so the base gets a box to stand on.
                UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
                if (collision == Collision.Box) part.AddComponent<BoxCollider>();
            }
        }

        private static Material Load(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Cartoon + name + ".mat");
            if (material == null) throw new InvalidOperationException("Missing material " + Cartoon + name + ".mat");
            return material;
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
