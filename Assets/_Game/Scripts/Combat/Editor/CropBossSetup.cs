using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace RushedCrops.Editor
{
    // Builds the six crop bosses (modelled in Blender) as prefabs that boss seeds grow in the crop field, with
    // their cartoon materials, shots and the red warning patches their attacks show on the ground. No boss is
    // placed on the map: the only way to meet one is to plant its seed.
    //   Carrot King (easy): stays put and slams the ground round it.
    //   Tomato Brute (medium): hops after the player, spits seeds and stomps.
    //   Onion Overlord (hard): charges, throws tears, sends out shockwaves to jump over, faster at half health.
    //   Beet Baron, Cabbage Colossus and Wheat Warlord: bonus bosses that don't count toward winning.
    // Beating the Carrot King, Tomato Brute and Onion Overlord (each once) is the boss part of winning.
    // How hard each boss is lives in the numbers below.
    public static class CropBossSetup
    {
        public const string RootName = "Crop boss";
        public const string TomatoName = "Tomato boss";
        public const string OnionName = "Onion boss";
        public const string ModelPath = "Assets/_Game/Models/Boss/CropBoss.fbx";
        public const string TomatoModelPath = "Assets/_Game/Models/Boss/TomatoBoss.fbx";
        public const string OnionModelPath = "Assets/_Game/Models/Boss/OnionBoss.fbx";
        // Bosses that only grow from boss seeds (they have no spot on the map).
        public const string BeetModelPath = "Assets/_Game/Models/Boss/BeetBoss.fbx";
        public const string CabbageModelPath = "Assets/_Game/Models/Boss/CabbageBoss.fbx";
        public const string WheatModelPath = "Assets/_Game/Models/Boss/WheatBoss.fbx";
        public const string WarningMaterialPath = MaterialFolder + "/MAT_BossWarning.mat";
        private const string MaterialFolder = "Assets/_Game/Materials/Boss";
        private const string PrefabFolder = "Assets/_Game/Prefabs/Boss";
        private const string Cartoon = "Assets/_Game/Materials/Cartoon/";

        private static readonly Dictionary<string, Color> Colours = new Dictionary<string, Color>
        {
            { "Boss_Orange", new Color(0.95f, 0.45f, 0.1f) },
            { "Boss_OrangeDark", new Color(0.7f, 0.28f, 0.06f) },
            { "Boss_Leaf", new Color(0.35f, 0.7f, 0.2f) },
            { "Boss_LeafDark", new Color(0.18f, 0.48f, 0.12f) },
            { "Boss_EyeWhite", new Color(0.97f, 0.95f, 0.86f) },
            { "Boss_Pupil", new Color(0.75f, 0.08f, 0.05f) },
            { "Boss_Dark", new Color(0.12f, 0.07f, 0.05f) },
            { "Boss_Dirt", new Color(0.4f, 0.26f, 0.14f) },
            { "Boss_Red", new Color(0.9f, 0.2f, 0.12f) },
            { "Boss_RedDark", new Color(0.66f, 0.1f, 0.07f) },
            { "Boss_Onion", new Color(0.62f, 0.27f, 0.5f) },
            { "Boss_OnionDark", new Color(0.44f, 0.16f, 0.36f) },
            { "Boss_Skin", new Color(0.88f, 0.74f, 0.54f) },
            { "Boss_Gold", new Color(0.98f, 0.78f, 0.2f) },
            { "Boss_Tear", new Color(0.6f, 0.85f, 1f) },
            { "Boss_Seed", new Color(0.98f, 0.88f, 0.5f) },
            { "Boss_Beet", new Color(0.56f, 0.08f, 0.26f) },
            { "Boss_BeetDark", new Color(0.36f, 0.04f, 0.16f) },
            { "Boss_Cabbage", new Color(0.62f, 0.84f, 0.42f) },
            { "Boss_CabbageDark", new Color(0.36f, 0.62f, 0.26f) },
            { "Boss_Wheat", new Color(0.95f, 0.78f, 0.32f) },
            { "Boss_WheatDark", new Color(0.72f, 0.52f, 0.18f) },
            { "Boss_Steel", new Color(0.7f, 0.72f, 0.76f) },
        };

        // Everything that makes one boss different from another.
        private class Spec
        {
            public string rootName, displayName, model;
            public Vector3 spot;
            public float yaw;
            public int health, seedsPerCrop;
            public Vector3 bodyCentre;
            public float bodyRadius, bodyHeight;
            public bool mound;
            public float aggro, moveSpeed, leash, turnSpeed, angryBelow;
            public Vector3 mouth;
            public string shot;           // projectile prefab name, or null
            public string shotMaterial;
            public Vector3 shotSize;
            public BossCombat.Attack[] attacks;
        }

        private static BossCombat.Attack Attack(BossCombat.AttackKind kind, float minRange, float maxRange, float windUp,
            int damage, float reach, int shots, float rest) => new BossCombat.Attack
        {
            kind = kind, minRange = minRange, maxRange = maxRange, windUp = windUp,
            damage = damage, reach = reach, shots = shots, rest = rest,
        };

        // Easy: never moves, one slow slam with a long warning and a long rest after it. 3 hatchet hits.
        private static readonly Spec Carrot = new Spec
        {
            rootName = RootName, displayName = "Carrot King", model = ModelPath,
            spot = new Vector3(-10.5f, 0f, -31f), yaw = -90f, health = 75, seedsPerCrop = 3,
            bodyCentre = new Vector3(0f, 1.9f, 0f), bodyRadius = 0.95f, bodyHeight = 3.4f, mound = true,
            aggro = 8f, moveSpeed = 0f, leash = 0f, turnSpeed = 120f, angryBelow = 0f,
            mouth = new Vector3(0f, 1.8f, 1f),
            attacks = new[] { Attack(BossCombat.AttackKind.Slam, 0f, 4.5f, 1.3f, 15, 3.6f, 1, 2.5f) },
        };

        // Medium: hops after the player, spits three seeds from range and stomps up close. 7 hatchet hits.
        private static readonly Spec Tomato = new Spec
        {
            rootName = TomatoName, displayName = "Tomato Brute", model = TomatoModelPath,
            spot = new Vector3(41f, 0f, -21f), yaw = -90f, health = 175, seedsPerCrop = 4,
            bodyCentre = new Vector3(0f, 1.3f, 0f), bodyRadius = 1.45f, bodyHeight = 2.7f, mound = false,
            aggro = 10f, moveSpeed = 2.2f, leash = 5f, turnSpeed = 160f, angryBelow = 0f,
            mouth = new Vector3(0f, 1f, 1.45f), shot = "PFB_BossSeed", shotMaterial = "Boss_Seed", shotSize = new Vector3(0.28f, 0.28f, 0.42f),
            attacks = new[]
            {
                Attack(BossCombat.AttackKind.Spit, 5.5f, 11f, 0.9f, 10, 1.7f, 3, 1.6f),
                Attack(BossCombat.AttackKind.Slam, 0f, 3.8f, 0.9f, 15, 3.8f, 1, 1.8f),
            },
        };

        // Hard: charges, throws five tears, sends out shockwaves to jump over, and speeds up at half health.
        // Short warnings and short rests. 12 hatchet hits.
        private static readonly Spec Onion = new Spec
        {
            rootName = OnionName, displayName = "Onion Overlord", model = OnionModelPath,
            spot = new Vector3(10f, 0f, 48.5f), yaw = 180f, health = 300, seedsPerCrop = 6,
            bodyCentre = new Vector3(0f, 1.95f, 0f), bodyRadius = 1.8f, bodyHeight = 3.9f, mound = false,
            aggro = 9f, moveSpeed = 3f, leash = 7f, turnSpeed = 220f, angryBelow = 0.5f,
            mouth = new Vector3(0f, 1.5f, 1.85f), shot = "PFB_BossTear", shotMaterial = "Boss_Tear", shotSize = new Vector3(0.32f, 0.32f, 0.5f),
            attacks = new[]
            {
                Attack(BossCombat.AttackKind.Charge, 4f, 11f, 0.8f, 20, 9f, 1, 1.4f),
                Attack(BossCombat.AttackKind.Spit, 3f, 14f, 0.7f, 10, 1.5f, 5, 1.3f),
                Attack(BossCombat.AttackKind.Shockwave, 0f, 5.5f, 0.9f, 20, 8f, 1, 1.6f),
            },
        };

        // Medium: a crowned beet that charges across the field and slams up close. 8 hatchet hits.
        private static readonly Spec Beet = new Spec
        {
            rootName = "Beet boss", displayName = "Beet Baron", model = BeetModelPath,
            spot = Vector3.zero, yaw = 0f, health = 200, seedsPerCrop = 4,
            bodyCentre = new Vector3(0f, 1.7f, 0f), bodyRadius = 1.05f, bodyHeight = 3.2f, mound = false,
            aggro = 10f, moveSpeed = 2.6f, leash = 6f, turnSpeed = 180f, angryBelow = 0f,
            mouth = new Vector3(0f, 1.8f, 1.05f),
            attacks = new[]
            {
                Attack(BossCombat.AttackKind.Charge, 4f, 10f, 0.9f, 15, 8f, 1, 1.6f),
                Attack(BossCombat.AttackKind.Slam, 0f, 4f, 0.9f, 15, 3.8f, 1, 1.8f),
            },
        };

        // Hard and heavy: a slow cabbage with big slams and shockwaves to jump over; faster below 40% health.
        // 11 hatchet hits.
        private static readonly Spec Cabbage = new Spec
        {
            rootName = "Cabbage boss", displayName = "Cabbage Colossus", model = CabbageModelPath,
            spot = Vector3.zero, yaw = 0f, health = 260, seedsPerCrop = 5,
            bodyCentre = new Vector3(0f, 1.6f, 0f), bodyRadius = 1.25f, bodyHeight = 3.0f, mound = false,
            aggro = 9f, moveSpeed = 1.6f, leash = 5f, turnSpeed = 120f, angryBelow = 0.4f,
            mouth = new Vector3(0f, 1.6f, 1.25f),
            attacks = new[]
            {
                Attack(BossCombat.AttackKind.Slam, 0f, 4.5f, 1.1f, 20, 4.5f, 1, 2f),
                Attack(BossCombat.AttackKind.Shockwave, 0f, 6f, 1.1f, 15, 7f, 1, 2.2f),
            },
        };

        // Medium: a wheat sheaf with a sickle that throws grain from range, charges and slams. 8 hatchet hits.
        private static readonly Spec Wheat = new Spec
        {
            rootName = "Wheat boss", displayName = "Wheat Warlord", model = WheatModelPath,
            spot = Vector3.zero, yaw = 0f, health = 180, seedsPerCrop = 4,
            bodyCentre = new Vector3(0f, 1.6f, 0f), bodyRadius = 0.8f, bodyHeight = 3.2f, mound = false,
            aggro = 11f, moveSpeed = 2.8f, leash = 6f, turnSpeed = 200f, angryBelow = 0f,
            mouth = new Vector3(0f, 2f, 0.8f), shot = "PFB_BossSeed", shotMaterial = "Boss_Seed", shotSize = new Vector3(0.28f, 0.28f, 0.42f),
            attacks = new[]
            {
                Attack(BossCombat.AttackKind.Spit, 4f, 12f, 0.8f, 8, 1.4f, 5, 1.4f),
                Attack(BossCombat.AttackKind.Charge, 3f, 9f, 0.8f, 15, 7f, 1, 1.5f),
                Attack(BossCombat.AttackKind.Slam, 0f, 3.5f, 0.8f, 12, 3.2f, 1, 1.6f),
            },
        };

        // One boss per crop saved as a prefab, for boss seeds to grow. The farm square hands it the Inventory when it
        // climbs out. Only the carrot, tomato and onion bosses count toward winning.
        // Order: carrot, tomato, onion, beet, cabbage, wheat. An existing prefab is kept and only its settings updated.
        public static CropBoss[] MakeGrownBossPrefabs()
        {
            return new[]
            {
                GrownBossPrefab(Carrot, "PFB_GrownCarrotKing", true),
                GrownBossPrefab(Tomato, "PFB_GrownTomatoBrute", true),
                GrownBossPrefab(Onion, "PFB_GrownOnionOverlord", true),
                GrownBossPrefab(Beet, "PFB_GrownBeetBaron", false),
                GrownBossPrefab(Cabbage, "PFB_GrownCabbageColossus", false),
                GrownBossPrefab(Wheat, "PFB_GrownWheatWarlord", false),
            };
        }

        private static CropBoss GrownBossPrefab(Spec spec, string prefabName, bool countsTowardVictory)
        {
            string path = $"{PrefabFolder}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<CropBoss>(path);
            if (existing != null)
            {
                // Keep the saved prefab, but make sure it has the grown-boss settings.
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    SetGrownBossSettings(contents.GetComponent<CropBoss>(), countsTowardVictory);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
                return AssetDatabase.LoadAssetAtPath<CropBoss>(path);
            }

            EnsureFolder(PrefabFolder);
            var holder = new GameObject("Grown boss holder");
            try
            {
                GameObject root = PlaceBoss(holder.transform, spec);
                root.name = prefabName;
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                SetGrownBossSettings(root.GetComponent<CropBoss>(), countsTowardVictory);
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<CropBoss>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        private static GameObject PlaceBoss(Transform parent, Spec spec)
        {
            if (parent == null) throw new InvalidOperationException("The map is missing the group for " + spec.displayName + ".");
            Transform old = parent.Find(spec.rootName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            SetUpModelMaterials(spec.model);
            var bag = AssetDatabase.LoadAssetAtPath<ItemDrop>(ItemDropSetup.DropPath);
            if (bag == null) bag = ItemDropSetup.DropPrefab();
            var inventory = UnityEngine.Object.FindAnyObjectByType<Inventory>();
            if (inventory == null) Debug.LogWarning("No Inventory in the scene yet. Assign it to the CropBoss.");

            var root = new GameObject(spec.rootName);
            Undo.RegisterCreatedObjectUndo(root, "Place crop boss");
            root.transform.SetParent(parent, false);
            root.transform.position = ToolStumpSetup.Ground(spec.spot);
            root.transform.rotation = Quaternion.Euler(0f, spec.yaw, 0f);

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.model);
            if (asset == null) throw new InvalidOperationException("Missing model " + spec.model);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            var body = root.AddComponent<CapsuleCollider>();
            body.center = spec.bodyCentre;
            body.radius = spec.bodyRadius;
            body.height = spec.bodyHeight;
            if (spec.mound)
            {
                // The carrot is planted in a dirt mound. No Rigidbody: it never moves.
                var mound = root.AddComponent<BoxCollider>();
                mound.center = new Vector3(0f, 0.15f, 0f);
                mound.size = new Vector3(1.8f, 0.3f, 1.8f);
            }

            var health = root.AddComponent<Health>();
            var healthSettings = new SerializedObject(health);
            healthSettings.FindProperty("maxHealth").intValue = spec.health;
            healthSettings.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<HitFlash>();

            var boss = root.AddComponent<CropBoss>();
            var settings = new SerializedObject(boss);
            settings.FindProperty("displayName").stringValue = spec.displayName;
            settings.FindProperty("model").objectReferenceValue = model.transform;
            settings.FindProperty("inventory").objectReferenceValue = inventory;
            settings.FindProperty("dropPrefab").objectReferenceValue = bag;
            SerializedProperty drops = settings.FindProperty("drops");
            drops.arraySize = FarmPlotSetup.CropAssets.Length;
            for (int i = 0; i < FarmPlotSetup.CropAssets.Length; i++)
            {
                var crop = AssetDatabase.LoadAssetAtPath<CropData>(FarmPlotSetup.CropAssets[i]);
                if (crop == null) throw new InvalidOperationException("Missing crop asset: " + FarmPlotSetup.CropAssets[i]);
                SerializedProperty entry = drops.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("crop").objectReferenceValue = crop;
                entry.FindPropertyRelative("amount").intValue = spec.seedsPerCrop;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();

            var combat = new SerializedObject(root.AddComponent<BossCombat>());
            combat.FindProperty("aggroRange").floatValue = spec.aggro;
            combat.FindProperty("moveSpeed").floatValue = spec.moveSpeed;
            combat.FindProperty("leash").floatValue = spec.leash;
            combat.FindProperty("turnSpeed").floatValue = spec.turnSpeed;
            combat.FindProperty("angryBelow").floatValue = spec.angryBelow;
            combat.FindProperty("mouth").vector3Value = spec.mouth;
            combat.FindProperty("bodyRadius").floatValue = spec.bodyRadius;
            combat.FindProperty("warningMaterial").objectReferenceValue = WarningMaterial();
            if (spec.shot != null)
                combat.FindProperty("projectilePrefab").objectReferenceValue = Projectile(spec.shot, spec.shotMaterial, spec.shotSize);
            SerializedProperty attacks = combat.FindProperty("attacks");
            attacks.arraySize = spec.attacks.Length;
            for (int i = 0; i < spec.attacks.Length; i++)
            {
                BossCombat.Attack a = spec.attacks[i];
                SerializedProperty entry = attacks.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("kind").enumValueIndex = (int)a.kind;
                entry.FindPropertyRelative("minRange").floatValue = a.minRange;
                entry.FindPropertyRelative("maxRange").floatValue = a.maxRange;
                entry.FindPropertyRelative("windUp").floatValue = a.windUp;
                entry.FindPropertyRelative("damage").intValue = a.damage;
                entry.FindPropertyRelative("reach").floatValue = a.reach;
                entry.FindPropertyRelative("shots").intValue = a.shots;
                entry.FindPropertyRelative("rest").floatValue = a.rest;
            }
            combat.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // A grown boss gets the Inventory from its farm square and is removed once it has sunk after dying.
        private static void SetGrownBossSettings(CropBoss boss, bool countsTowardVictory)
        {
            var settings = new SerializedObject(boss);
            settings.FindProperty("inventory").objectReferenceValue = null; // a prefab can't keep a scene object
            settings.FindProperty("countsTowardVictory").boolValue = countsTowardVictory;
            settings.FindProperty("removeAfterDeath").boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        // A see-through red for warning patches, seen from both sides and never casting shadows.
        public static Material WarningMaterial()
        {
            EnsureFolder(MaterialFolder);
            var material = AssetDatabase.LoadAssetAtPath<Material>(WarningMaterialPath);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "MAT_BossWarning" };
            material.SetFloat("_Surface", 1f); // transparent
            material.SetFloat("_Blend", 0f);   // alpha
            material.SetFloat("_Cull", 0f);    // both sides
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor("_BaseColor", new Color(0.95f, 0.1f, 0.05f, 0.42f));
            AssetDatabase.CreateAsset(material, WarningMaterialPath);
            return material;
        }

        // A shot the boss spits: a stretched ball in one of the boss colours, with no collider of its own.
        private static BossProjectile Projectile(string name, string materialName, Vector3 size)
        {
            EnsureFolder(PrefabFolder);
            string path = $"{PrefabFolder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<BossProjectile>(path);
            if (existing != null) return existing;
            GameObject shot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                shot.name = name;
                UnityEngine.Object.DestroyImmediate(shot.GetComponent<Collider>());
                shot.transform.localScale = size;
                var renderer = shot.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = BossMaterial(materialName);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                shot.AddComponent<BossProjectile>();
                return PrefabUtility.SaveAsPrefabAsset(shot, path).GetComponent<BossProjectile>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(shot);
            }
        }

        // One flat cartoon material per Blender slot, made from Straw so it shares the shader.
        public static void SetUpModelMaterials() => SetUpModelMaterials(ModelPath);

        public static void SetUpModelMaterials(string modelPath)
        {
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing model " + modelPath);
            bool changed = false;
            if (modelPath != ModelPath && (!importer.bakeAxisConversion || !importer.preserveHierarchy))
            {
                // The new models are written the way Unity sees them, like the game's other Blender models.
                importer.bakeAxisConversion = true;
                importer.preserveHierarchy = true;
                changed = true;
            }
            // The model's own material slots, by name, so only the colours it uses are linked.
            var slots = new HashSet<string>();
            foreach (Renderer renderer in AssetDatabase.LoadAssetAtPath<GameObject>(modelPath).GetComponentsInChildren<Renderer>(true))
                foreach (Material used in renderer.sharedMaterials)
                    if (used != null) slots.Add(used.name);
            foreach (string slot in slots)
            {
                if (!Colours.ContainsKey(slot)) continue;
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), slot);
                Material material = BossMaterial(slot);
                if (!importer.GetExternalObjectMap().TryGetValue(id, out UnityEngine.Object mapped) || mapped != material)
                {
                    importer.AddRemap(id, material);
                    changed = true;
                }
            }
            if (importer.importAnimation || importer.importCameras || importer.importLights)
            {
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
        }

        private static Material BossMaterial(string name)
        {
            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var template = AssetDatabase.LoadAssetAtPath<Material>(Cartoon + "Straw.mat");
                if (template == null) throw new InvalidOperationException("Missing material " + Cartoon + "Straw.mat");
                material = new Material(template) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.GetColor("_BaseColor") != Colours[name])
            {
                material.SetColor("_BaseColor", Colours[name]);
                if (material.HasProperty("_Color")) material.SetColor("_Color", Colours[name]);
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
