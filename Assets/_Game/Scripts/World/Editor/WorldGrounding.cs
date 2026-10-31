using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RushedCrops.Editor
{
    // Ground against the saved terrain collider, never against another prop or a prefab pivot.
    public static class WorldGrounding
    {
        [MenuItem("Rushed Crops/World/Align scenery to ground")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Align the saved scene outside Play.");
            var world = GameObject.Find(FarmWorldBuilder.RootName);
            var terrain = world.transform.Find("00_Terrain/Walkable valley").GetComponent<MeshCollider>();
            var roots = new List<Transform>();
            foreach (Transform child in world.GetComponentsInChildren<Transform>(true))
                if (PrefabUtility.IsOutermostPrefabInstanceRoot(child.gameObject)) roots.Add(child);

            int aligned = 0;
            foreach (Transform root in roots)
            {
                // These props have an explicit support or are hung from an authored beam.
                if (root.name.StartsWith("Lantern") || root.name == "Carrot01" || root.name == "Axe01") continue;
                if (!VisibleBounds(root, out Bounds bounds)) continue;
                if (!SupportHeight(terrain, root, bounds, out float height)) continue;
                AlignBase(root, bounds, height);
                aligned++;
            }

            // Posts and signs are authored groups, rather than imported prefabs.
            foreach (string groupName in new[] { "03_BarnAndPlantingPlots", "04_MerchantClearing", "05_StoneQuarry", "06_LoggingArea", "08_Wayfinding" })
            foreach (Transform group in world.transform.Find(groupName))
            {
                if (!(group.name == "Dusk lantern" || group.name.StartsWith("Sign -") || group.name == "Fallen repair timber")) continue;
                if (VisibleBounds(group, out Bounds bounds) && SupportHeight(terrain, group, bounds, out float height))
                    AlignBase(group, bounds, height);
            }

            // Place display produce on the actual table top and the axe on its chopping block.
            Transform market = world.transform.Find("04_MerchantClearing");
            Transform table = FindChild(market, "Table01");
            Transform logging = world.transform.Find("06_LoggingArea");
            Transform block = FindChild(logging, "ChoppingBlock01");
            foreach (Transform root in roots)
            {
                if (root.name == "Carrot01" && table != null && VisibleBounds(table, out Bounds top) && VisibleBounds(root, out Bounds produce))
                    AlignBase(root, produce, top.max.y);
                if (root.name == "Axe01" && block != null && VisibleBounds(block, out Bounds blockTop) && VisibleBounds(root, out Bounds axe))
                    AlignBase(root, axe, blockTop.max.y);
            }

            // Separate preview crops share the soil surface of the patch beneath them.
            Transform garden = world.transform.Find("03_BarnAndPlantingPlots");
            foreach (Transform crop in garden)
            {
                if (!(crop.name.StartsWith("CarrotPlant") || crop.name.StartsWith("WheatPlant"))) continue;
                foreach (Transform plot in garden)
                {
                    if (!plot.name.StartsWith("Planting plot") || !VisibleBounds(plot, out Bounds plotBounds)) continue;
                    Vector3 p = crop.position;
                    if (p.x < plotBounds.min.x || p.x > plotBounds.max.x || p.z < plotBounds.min.z || p.z > plotBounds.max.z) continue;
                    float soil = float.MinValue;
                    foreach (Renderer renderer in plot.GetComponentsInChildren<Renderer>())
                        if (renderer.enabled && renderer.name.StartsWith("Ridge")) soil = Mathf.Max(soil, renderer.bounds.max.y);
                    if (soil != float.MinValue && VisibleBounds(crop, out Bounds cropBounds)) AlignBase(crop, cropBounds, soil);
                    break;
                }
            }
            EditorSceneManager.MarkSceneDirty(world.scene);
            Debug.Log("Ground alignment complete: " + aligned + " scenery roots; table, tool, crop and hanging supports preserved.");
        }

        public static int CountBuriedScenery()
        {
            var world = GameObject.Find(FarmWorldBuilder.RootName);
            var terrain = world.transform.Find("00_Terrain/Walkable valley").GetComponent<MeshCollider>();
            int buried = 0;
            foreach (Transform root in world.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(root.gameObject)) continue;
                if (root.name.StartsWith("Lantern") || root.name == "Carrot01" || root.name == "Axe01") continue;
                if (VisibleBounds(root, out Bounds bounds) && SupportHeight(terrain, root, bounds, out float height) && height - bounds.min.y > 0.08f)
                    buried++;
            }
            return buried;
        }

        private static void AlignBase(Transform root, Bounds bounds, float supportHeight)
        {
            float difference = supportHeight + 0.015f - bounds.min.y;
            if (Mathf.Abs(difference) < 0.002f) return;
            Undo.RecordObject(root, "Align scenery base");
            root.position += Vector3.up * difference;
        }

        private static bool TerrainHeight(MeshCollider terrain, Vector3 point, out float height)
        {
            var ray = new Ray(new Vector3(point.x, 100f, point.z), Vector3.down);
            if (terrain.Raycast(ray, out RaycastHit hit, 200f)) { height = hit.point.y; return true; }
            height = 0f;
            return false;
        }

        private static bool SupportHeight(MeshCollider terrain, Transform root, Bounds bounds, out float height)
        {
            height = float.MinValue;
            // Probe the mesh's actual low vertices. A broad tree crown must not lift its trunk,
            // and a rock's off-centre base must not be aligned using its bounding-box centre.
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<Renderer>();
                if (renderer == null || !renderer.enabled || filter.sharedMesh == null) continue;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 world = filter.transform.TransformPoint(vertex);
                    if (world.y > bounds.min.y + 0.4f) continue;
                    if (TerrainHeight(terrain, world, out float surface))
                        height = Mathf.Max(height, surface - (world.y - bounds.min.y));
                }
            }
            if (height != float.MinValue) return true;
            return TerrainHeight(terrain, bounds.center, out height);
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

        private static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform child in parent) if (child.name == name) return child;
            return null;
        }
    }
}
