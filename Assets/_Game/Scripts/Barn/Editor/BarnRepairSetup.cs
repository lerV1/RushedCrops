using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RushedCrops.Editor
{
    // Sets up the barn repair (see BarnRepair) and saves the barn broken: boards across the doors, roof holes
    // and a leaning east wall. Running it again restores every barn piece first, so the result is the same.
    public static class BarnRepairSetup
    {
        public const string BarnName = "Inherited barn";
        public const string BoardsName = "Door boards";
        public const string PropName = "Wall prop";
        private const string Cartoon = "Assets/_Game/Materials/Cartoon/";

        // Barn pieces by their spot on the pack's building grid (local to the barn).
        private static readonly Vector3 FrontDoor = new Vector3(-1f, 0f, -5f);
        private static readonly Vector3[] LeaningWall = { new Vector3(3f, 0f, 2f), new Vector3(3f, 0f, 3f) };
        private const float DoorOpenAngle = 100f; // swings out into the yard
        private const float WallLean = 12f;       // degrees the broken wall leans out
        private const float RoofDrop = 0.8f;      // how far above its place a roof board starts falling from

        [MenuItem("Rushed Crops/Farming/Set up barn repair")]
        public static void SetUpInScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Set up the barn outside Play.");
            Transform barn = GameObject.Find(FarmWorldBuilder.RootName)?.transform.Find(FarmWorldBuilder.FarmGroup + "/" + BarnName);
            if (barn == null) throw new InvalidOperationException("Open SC_Farm: it has no " + BarnName + ".");
            SetUp(barn.gameObject);
            EditorSceneManager.MarkSceneDirty(barn.gameObject.scene);
            Debug.Log("The barn is set up for repairs and saved broken.");
        }

        public static BarnRepair SetUp(GameObject barnObject)
        {
            Transform barn = barnObject.transform;
            foreach (string name in new[] { BoardsName, PropName })
                for (Transform old = barn.Find(name); old != null; old = barn.Find(name))
                    Undo.DestroyObjectImmediate(old.gameObject);

            // Every roof board back in place; every 7th one becomes a hole (the same ones as before).
            var holes = new List<Transform>();
            int roofBoard = 0;
            foreach (Transform piece in barn.GetComponentsInChildren<Transform>(true))
            {
                if (!piece.name.StartsWith("RoofCenter")) continue;
                Restore(piece);
                if (++roofBoard % 7 == 0) holes.Add(piece);
            }
            Transform door = Piece(barn, "DoorDouble01", FrontDoor);
            Transform[] walls = LeaningWall.Select(spot => Piece(barn, "WallLogLong01", spot)).ToArray();
            Restore(door);
            foreach (Transform wall in walls) Restore(wall);

            Transform boards = BuildDoorBoards(barn, door);
            Transform prop = BuildWallProp(barn, walls);

            var jobs = new[]
            {
                new BarnRepair.Job
                {
                    name = "Unlock the barn", doneMessage = "Barn unlocked",
                    cost = new[] { new ResourceAmount(ResourceType.Coins, 40) },
                    parts = new[] { Disappears(boards), Turns(door, Quaternion.AngleAxis(DoorOpenAngle, Vector3.up) * door.localRotation, door.localRotation) },
                },
                new BarnRepair.Job
                {
                    name = "Patch the roof", doneMessage = "Roof patched",
                    cost = new[] { new ResourceAmount(ResourceType.Coins, 20), new ResourceAmount(ResourceType.Wood, 4) },
                    parts = holes.Select(DropsIn).ToArray(),
                },
                new BarnRepair.Job
                {
                    name = "Straighten the east wall", doneMessage = "Wall straightened",
                    cost = new[] { new ResourceAmount(ResourceType.Coins, 25), new ResourceAmount(ResourceType.Wood, 3), new ResourceAmount(ResourceType.Stone, 4) },
                    needsBossBeaten = true,
                    // Leaning out: turned about the barn's length, so the wall's bottom edge stays on the ground.
                    parts = walls.Select(wall => Turns(wall, wall.localRotation, Quaternion.AngleAxis(-WallLean, Vector3.forward) * wall.localRotation))
                        .Append(Disappears(prop)).ToArray(),
                },
            };

            var inventory = UnityEngine.Object.FindAnyObjectByType<Inventory>();
            if (inventory == null) Debug.LogWarning("No Inventory in the scene yet. Assign it to the barn's BarnRepair.", barnObject);
            var repair = barnObject.GetComponent<BarnRepair>();
            if (repair == null) repair = Undo.AddComponent<BarnRepair>(barnObject);
            Undo.RecordObject(repair, "Set up barn repair");
            repair.Configure(inventory, jobs);

            foreach (BarnRepair.Job job in jobs)
            foreach (BarnRepair.Part part in job.parts)
            {
                Undo.RecordObject(part.part, "Set up barn repair");
                Undo.RecordObject(part.part.gameObject, "Set up barn repair");
            }
            repair.ShowRepairs(0);
            foreach (BarnRepair.Job job in jobs)
            foreach (BarnRepair.Part part in job.parts)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(part.part);
                PrefabUtility.RecordPrefabInstancePropertyModifications(part.part.gameObject);
            }
            EditorUtility.SetDirty(repair);
            return repair;
        }

        private static Transform Piece(Transform barn, string name, Vector3 spot)
        {
            Transform[] found = barn.Cast<Transform>().Where(t => t.name == name && (t.localPosition - spot).sqrMagnitude < 0.01f).ToArray();
            if (found.Length != 1) throw new InvalidOperationException($"Expected one {name} at {spot} in the barn, found {found.Length}.");
            return found[0];
        }

        private static void Restore(Transform piece)
        {
            Undo.RecordObject(piece, "Set up barn repair");
            Undo.RecordObject(piece.gameObject, "Set up barn repair");
            var source = PrefabUtility.GetCorrespondingObjectFromSource(piece);
            if (source != null)
            {
                piece.localPosition = source.localPosition;
                piece.localRotation = source.localRotation;
                piece.localScale = source.localScale;
            }
            piece.gameObject.SetActive(true);
        }

        // Shown while broken, gone once fixed (boards, prop).
        private static BarnRepair.Part Disappears(Transform t) => new BarnRepair.Part
        {
            part = t, shownBroken = true, shownFixed = false,
            brokenPosition = t.localPosition, fixedPosition = t.localPosition,
            brokenRotation = t.localEulerAngles, fixedRotation = t.localEulerAngles, scale = t.localScale,
        };

        // Always shown, turned from one rotation to another (the door opening, the wall standing up).
        private static BarnRepair.Part Turns(Transform t, Quaternion fixedRotation, Quaternion brokenRotation) => new BarnRepair.Part
        {
            part = t, shownBroken = true, shownFixed = true,
            brokenPosition = t.localPosition, fixedPosition = t.localPosition,
            brokenRotation = brokenRotation.eulerAngles, fixedRotation = fixedRotation.eulerAngles, scale = t.localScale,
        };

        // Missing while broken, falls into its place when fixed (roof boards).
        private static BarnRepair.Part DropsIn(Transform t) => new BarnRepair.Part
        {
            part = t, shownBroken = false, shownFixed = true,
            brokenPosition = t.localPosition + Vector3.up * RoofDrop, fixedPosition = t.localPosition,
            brokenRotation = t.localEulerAngles, fixedRotation = t.localEulerAngles, scale = t.localScale,
        };

        private static Transform BuildDoorBoards(Transform barn, Transform door)
        {
            // The barn is not turned, so its front doors face -Z, out into the yard.
            Bounds doorBounds = BoundsOf(door);
            Vector3 outward = Vector3.back;
            float depth = doorBounds.extents.z;

            var boards = new GameObject(BoardsName).transform;
            Undo.RegisterCreatedObjectUndo(boards.gameObject, "Set up barn repair");
            boards.SetParent(barn, false);
            boards.localScale = new Vector3(1f / barn.lossyScale.x, 1f / barn.lossyScale.y, 1f / barn.lossyScale.z); // children in metres
            boards.SetPositionAndRotation(doorBounds.center + outward * depth, Quaternion.LookRotation(outward));

            Material wood = Load("LightWood"), iron = Load("Iron");
            Box(boards, "Board", new Vector3(0f, 0f, 0.04f), 30f, new Vector3(3.5f, 0.24f, 0.07f), wood);
            Box(boards, "Board", new Vector3(0f, 0f, 0.04f), -30f, new Vector3(3.5f, 0.24f, 0.07f), wood);
            Box(boards, "Board", new Vector3(0f, -0.15f, 0.11f), 0f, new Vector3(3.4f, 0.26f, 0.07f), wood);
            Box(boards, "Lock", new Vector3(0f, -0.15f, 0.18f), 0f, new Vector3(0.24f, 0.3f, 0.1f), iron);
            return boards;
        }

        private static Transform BuildWallProp(Transform barn, Transform[] walls)
        {
            // The barn is not turned, so its east wall faces +X.
            Bounds wall = BoundsOf(walls[0]);
            foreach (Transform w in walls.Skip(1)) wall.Encapsulate(BoundsOf(w));
            const float topHeight = 2f;
            Vector3 top = new Vector3(wall.max.x + topHeight * Mathf.Sin(WallLean * Mathf.Deg2Rad), wall.min.y + topHeight, wall.center.z);
            Vector3 bottom = new Vector3(wall.max.x + 1.7f, wall.min.y, wall.center.z);
            // Rest the beam's lower corner on the ground, never below the barn's own base.
            if (Physics.Raycast(bottom + Vector3.up * 3f, Vector3.down, out RaycastHit ground, 10f, 1 << 6)) bottom.y = Mathf.Max(bottom.y, ground.point.y);
            bottom.y += 0.07f;

            GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.name = PropName;
            Undo.RegisterCreatedObjectUndo(prop, "Set up barn repair");
            prop.GetComponent<MeshRenderer>().sharedMaterial = Load("DarkWood");
            prop.transform.SetPositionAndRotation((top + bottom) * 0.5f, Quaternion.LookRotation(top - bottom));
            prop.transform.localScale = new Vector3(0.14f, 0.14f, Vector3.Distance(top, bottom));
            prop.transform.SetParent(barn, true);
            return prop.transform;
        }

        private static void Box(Transform parent, string name, Vector3 position, float angle, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Bounds BoundsOf(Transform t)
        {
            Renderer[] renderers = t.GetComponentsInChildren<Renderer>();
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        private static Material Load(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Cartoon + name + ".mat");
            if (material == null) throw new InvalidOperationException("Missing material " + Cartoon + name + ".mat");
            return material;
        }
    }
}
