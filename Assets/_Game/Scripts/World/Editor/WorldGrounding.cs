using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RushedCrops.Editor
{
    // Grounds scenery against the saved terrain collider, never against another prop or a prefab pivot.
    //   Embed: trees, stumps, rocks, plants, fences and posts sink slightly into the lowest ground under them,
    //          so no edge hangs in the air on a slope.
    //   Rest:  buildings, carts, crates and other props sit on top of the ground. They are only placed
    //          on the flat pads, so resting on the highest point leaves no gap underneath.
    //   Supported items (on a counter, block or stack) and authored structures are left alone.
    public static class WorldGrounding
    {
        private const float RestTolerance = 0.05f;
        private const float MaxRestSlope = 0.3f;

        [MenuItem("Rushed Crops/World/Align scenery to ground")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Align the saved scene outside Play.");
            MeshCollider[] terrain = FindTerrain(out GameObject world);
            int aligned = 0;
            foreach (Transform item in Scenery(world))
            {
                if (!Sample(terrain, item, out Bounds bounds, out float lowest, out float highest)) continue;
                float target = FitFor(item, bounds, out float sink) == Fit.Embed ? lowest - sink : highest + 0.01f;
                item.position += Vector3.up * (target - bounds.min.y);
                aligned++;
            }
            EditorSceneManager.MarkSceneDirty(world.scene);
            Debug.Log("Ground alignment complete: " + aligned + " scenery objects.");
        }

        // Returns how many objects float above, or are sunk into, the ground. Lists the first few in report.
        public static int CountMisplaced(out string report)
        {
            MeshCollider[] terrain = FindTerrain(out GameObject world);
            var problems = new List<string>();
            foreach (Transform item in Scenery(world))
            {
                if (!Sample(terrain, item, out Bounds bounds, out float lowest, out float highest)) continue;
                string problem = null;
                if (FitFor(item, bounds, out float sink) == Fit.Embed)
                {
                    if (bounds.min.y > lowest + 0.02f) problem = "floats " + (bounds.min.y - lowest).ToString("F2") + " m";
                }
                else if (Mathf.Abs(bounds.min.y - highest - 0.01f) > RestTolerance) problem = "base is " + (bounds.min.y - highest).ToString("F2") + " m off the ground";
                else if (highest - lowest > MaxRestSlope) problem = "stands on a slope of " + (highest - lowest).ToString("F2") + " m";
                if (problem != null) problems.Add(item.name + " at " + item.position.ToString("F0") + " " + problem);
            }
            report = string.Join("\n", problems.GetRange(0, Mathf.Min(problems.Count, 12)));
            return problems.Count;
        }

        private enum Fit { Embed, Rest }

        private static Fit FitFor(Transform item, Bounds bounds, out float sink)
        {
            string name = item.name;
            sink = 0f;
            if (name.StartsWith("Oak") || name.StartsWith("Pine") || name.StartsWith("Spruce") || name.StartsWith("Willow")) { sink = 0.15f; return Fit.Embed; }
            if (name.StartsWith("Stone") || name.StartsWith("QuarryFace")) { sink = bounds.size.y * 0.3f; return Fit.Embed; }
            if (name.StartsWith("Rubble") || name.StartsWith("Patch") || name.StartsWith("Planting plot")) { sink = 0.05f; return Fit.Embed; }
            if (name.StartsWith("Grass") || name.StartsWith("Fern") || name.StartsWith("Bush") || name.StartsWith("Flower") || name.StartsWith("Cattail")) { sink = 0.05f; return Fit.Embed; }
            if (name.StartsWith("Fence") || name.StartsWith("Garden gate") || name.StartsWith("Dusk lantern")) { sink = 0.1f; return Fit.Embed; }
            return Fit.Rest;
        }

        // Imported prefabs plus authored lantern posts; supported items and the hung lantern models are excluded.
        private static List<Transform> Scenery(GameObject world)
        {
            var items = new List<Transform>();
            foreach (Transform child in world.GetComponentsInChildren<Transform>(true))
            {
                bool imported = PrefabUtility.IsOutermostPrefabInstanceRoot(child.gameObject);
                if (!(imported || child.name == "Dusk lantern")) continue;
                if (child.name.Contains("(on ") || child.name.Contains("(stacked)") || child.name.StartsWith("Lantern0")) continue;
                // A farm square's soil and outline are placed by FarmPlotSetup, not aligned as scenery.
                if (child.GetComponentInParent<FarmTile>(true) != null) continue;
                items.Add(child);
            }
            return items;
        }

        // Lowest and highest terrain heights under the object's footprint (its lowest vertices).
        private static bool Sample(MeshCollider[] terrain, Transform item, out Bounds bounds, out float lowest, out float highest)
        {
            lowest = float.MaxValue;
            highest = float.MinValue;
            if (!VisibleBounds(item, out bounds)) return false;
            foreach (MeshFilter filter in item.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<Renderer>();
                if (renderer == null || !renderer.enabled || filter.sharedMesh == null) continue;
                Vector3[] vertices = filter.sharedMesh.vertices;
                int step = Mathf.Max(1, vertices.Length / 400);
                for (int i = 0; i < vertices.Length; i += step)
                {
                    Vector3 point = filter.transform.TransformPoint(vertices[i]);
                    if (point.y > bounds.min.y + 0.3f) continue;
                    if (!TerrainHeight(terrain, point, out float surface)) continue;
                    lowest = Mathf.Min(lowest, surface);
                    highest = Mathf.Max(highest, surface);
                }
            }
            if (highest != float.MinValue) return true;
            if (!TerrainHeight(terrain, bounds.center, out float centre)) return false;
            lowest = highest = centre;
            return true;
        }

        // Highest hit among the walkable terrain and the distant hills beneath it.
        private static bool TerrainHeight(MeshCollider[] terrain, Vector3 point, out float height)
        {
            var ray = new Ray(new Vector3(point.x, 100f, point.z), Vector3.down);
            height = float.MinValue;
            foreach (MeshCollider surface in terrain)
                if (surface != null && surface.Raycast(ray, out RaycastHit hit, 200f)) height = Mathf.Max(height, hit.point.y);
            return height != float.MinValue;
        }

        private static bool VisibleBounds(Transform root, out Bounds bounds)
        {
            bool found = false;
            bounds = new Bounds();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer is ParticleSystemRenderer) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        private static MeshCollider[] FindTerrain(out GameObject world)
        {
            world = GameObject.Find(FarmWorldBuilder.RootName);
            if (world == null) throw new InvalidOperationException("The farm map is missing. Use Rushed Crops > World > Rebuild map.");
            var hills = world.transform.Find(FarmWorldBuilder.HillsPath);
            return new[] {
                world.transform.Find(FarmWorldBuilder.TerrainPath).GetComponent<MeshCollider>(),
                hills != null ? hills.GetComponent<MeshCollider>() : null
            };
        }
    }
}
