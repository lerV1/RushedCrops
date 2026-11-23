using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RushedCrops.Editor
{
    // Places the gathering spots (see ResourceNode): choppable oaks round the edge of the logging camp and
    // mineable rocks on the quarry floor. Every spot is solid and gets moved until there is room to walk round it.
    // The map builder calls Place, and it can be run again from the menu.
    public static class GatheringSetup
    {
        public const string TreesName = "Choppable trees";
        public const string RocksName = "Mineable rocks";
        public const float WalkRoom = 1.4f; // open ground, in metres, kept round every spot
        private const string Pack = "Assets/Holotna/Medieval Rural/Prefabs/";
        private const int GroundLayer = 6;
        private const int WaterLayer = 4;

        // At the edge of the camp clearing among the old stumps. All the way round
        // except the west, where the path comes in (degrees: 0 is east, 90 is north). A crowded spot tries a
        // little either side, then a little further in or out.
        private static readonly Vector2 Camp = new Vector2(42f, -22f);
        private static readonly float[] TreeRings = { 12f, 14f, 10f };
        private static readonly float[] TreeAngles = { -155f, -119f, -83f, -47f, -11f, 25f, 61f, 97f, 133f };
        // Oaks, so they stand out from the pines and spruces of the forest round the camp.
        private static readonly string[] TreeModels = { "Oak01", "Oak02" };

        // Along the back wall and the sides of the quarry floor, where boulders would fall, leaving the middle open.
        private static readonly Vector2[] RockSpots =
        {
            new Vector2(4.5f, 53.5f), new Vector2(8.5f, 55.5f), new Vector2(12.5f, 55.5f), new Vector2(16f, 53.5f),
            new Vector2(2.8f, 48.8f), new Vector2(18f, 48.5f), new Vector2(6f, 44.5f),
        };

        [MenuItem("Rushed Crops/World/Place gathering spots")]
        public static void PlaceInScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Place the gathering spots outside Play.");
            Transform world = GameObject.Find(FarmWorldBuilder.RootName)?.transform;
            if (world == null) throw new InvalidOperationException("Open SC_Farm with the map built first.");
            Place(world, true);
            EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
        }

        // style is false when the map builder calls this: it styles the whole map afterwards anyway.
        public static void Place(Transform world, bool style)
        {
            Transform camp = world.Find("06_LoggingCamp"), quarry = world.Find("05_Quarry"), terrain = world.Find("00_Terrain");
            if (camp == null || quarry == null || terrain == null) throw new InvalidOperationException("The map is missing the logging camp, the quarry or the terrain.");
            var inventory = UnityEngine.Object.FindAnyObjectByType<Inventory>();
            var drop = AssetDatabase.LoadAssetAtPath<ItemDrop>(ItemDropSetup.DropPath);
            if (drop == null) throw new InvalidOperationException("Missing " + ItemDropSetup.DropPath + ". Run Rushed Crops > Items > Set up item drops.");

            Transform trees = NewGroup(camp, TreesName), rocks = NewGroup(quarry, RocksName);
            var placed = new List<(Vector3 spot, float footprint)>();
            var added = new List<GameObject>();
            int skipped = 0;

            for (int i = 0; i < TreeAngles.Length; i++)
            {
                bool done = false;
                foreach (float ring in TreeRings)
                foreach (float nudge in new[] { 0f, 6f, -6f, 12f, -12f })
                {
                    if (done) break;
                    float angle = (TreeAngles[i] + nudge) * Mathf.Deg2Rad;
                    Vector2 at = Camp + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring;
                    if (!HasRoom(at, 0.5f, terrain, placed, out Vector3 ground)) continue;
                    var node = MakeNode(trees, "Choppable tree " + (i + 1), ground + Vector3.down * 0.05f, i * 73f,
                        Pack + "Vegetation/" + TreeModels[i % TreeModels.Length], 1.15f + (i % 3) * 0.12f,
                        Pack + "Vegetation/OakStump0" + (1 + i % 2), 1.5f, inventory, drop, added);
                    node.Describe("tree", "Chop", ToolType.Hatchet, ResourceType.Wood, 3, 3, 120f, true);
                    placed.Add((ground, 0.5f));
                    done = true;
                }
                if (!done) skipped++;
            }

            for (int i = 0; i < RockSpots.Length; i++)
            {
                bool done = false;
                foreach (Vector2 nudge in new[] { Vector2.zero, Vector2.right, Vector2.left, Vector2.up, Vector2.down, new Vector2(1.5f, 1.5f), new Vector2(-1.5f, 1.5f) })
                {
                    if (!HasRoom(RockSpots[i] + nudge, 1.15f, terrain, placed, out Vector3 ground)) continue;
                    var node = MakeNode(rocks, "Mineable rock " + (i + 1), ground + Vector3.down * 0.05f, i * 61f,
                        Pack + "Rocks/Stone0" + (1 + i % 3), 1.8f, Pack + "Rocks/Rubble01", 1f, inventory, drop, added);
                    node.Describe("rock", "Mine", ToolType.Pickaxe, ResourceType.Stone, 2, 4, 150f, false);
                    placed.Add((ground, 1.15f));
                    done = true;
                    break;
                }
                if (!done) skipped++;
            }

            if (style) CartoonWorldStyler.StyleAdded(added);
            Debug.Log($"Placed {trees.childCount} choppable trees and {rocks.childCount} mineable rocks" + (skipped > 0 ? $"; {skipped} spots had no room." : "."));
        }

        private static Transform NewGroup(Transform parent, string name)
        {
            for (Transform old = parent.Find(name); old != null; old = parent.Find(name))
                Undo.DestroyObjectImmediate(old.gameObject);
            var group = new GameObject(name).transform;
            Undo.RegisterCreatedObjectUndo(group.gameObject, "Place gathering spots");
            group.SetParent(parent, false);
            return group;
        }

        private static ResourceNode MakeNode(Transform group, string name, Vector3 at, float yaw, string wholeModel, float wholeScale,
            string brokenModel, float brokenScale, Inventory inventory, ItemDrop drop, List<GameObject> added)
        {
            var node = new GameObject(name).transform;
            node.SetParent(group, false);
            node.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            GameObject whole = Model(wholeModel, node, wholeScale);
            GameObject broken = Model(brokenModel, node, brokenScale);
            broken.SetActive(false);
            added.Add(whole);
            added.Add(broken);
            var resource = node.gameObject.AddComponent<ResourceNode>();
            resource.Configure(inventory, drop, whole.transform, broken);
            return resource;
        }

        // A pack model, solid on the Ground layer like the map's other scenery.
        private static GameObject Model(string path, Transform parent, float scale)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path + ".prefab");
            if (asset == null) throw new InvalidOperationException("Missing prefab " + path);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * scale;
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = true;
                collider.gameObject.layer = GroundLayer;
            }
            return model;
        }

        // True when a spot with this footprint (radius, in metres) has WalkRoom of open ground round it:
        // nothing solid nearby, and no other gathering spot closer than both footprints plus WalkRoom.
        private static bool HasRoom(Vector2 at, float footprint, Transform terrain, List<(Vector3 spot, float footprint)> placed, out Vector3 ground)
        {
            ground = Ground(at, terrain);
            foreach (var (spot, size) in placed)
            {
                Vector3 between = spot - ground;
                between.y = 0f;
                if (between.magnitude < size + footprint + WalkRoom) return false;
            }
            float reach = footprint + WalkRoom;
            // A capsule from just above the ground up past head height, as wide as the spot plus the walking room.
            foreach (Collider other in Physics.OverlapCapsule(ground + Vector3.up * (reach + 0.15f), ground + Vector3.up * (reach + 1.2f), reach, ~0, QueryTriggerInteraction.Ignore))
            {
                if (other.transform.IsChildOf(terrain) || other.gameObject.layer == WaterLayer) continue;
                return false;
            }
            return true;
        }

        // The terrain's height here, ignoring trees and props.
        private static Vector3 Ground(Vector2 at, Transform terrain)
        {
            Vector3 top = new Vector3(at.x, 60f, at.y);
            float best = float.NegativeInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(top, Vector3.down, 120f, 1 << GroundLayer, QueryTriggerInteraction.Ignore))
                if (hit.collider.transform.IsChildOf(terrain)) best = Mathf.Max(best, hit.point.y);
            return new Vector3(at.x, best > float.NegativeInfinity ? best : 0f, at.y);
        }
    }
}
