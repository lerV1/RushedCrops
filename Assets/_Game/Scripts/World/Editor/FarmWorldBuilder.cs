using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RushedCrops.Editor
{
    // Editor-only authoring tool. The saved scene contains the map; nothing is spawned at runtime.
    //
    // Layout (X = east, Z = north):
    //   Farm (spawn)   south-west  - inherited barn, farmyard and a fenced garden of six plots
    //   Market         centre      - merchant stall where crops are sold and seeds bought
    //   Logging camp   east        - wood source, surrounded by the densest forest
    //   Quarry         north       - stone source, reached over the only river bridge
    // Every building and prop stands on a flat pad; paths are painted into the terrain mesh,
    // so nothing floats above or sinks below the ground.
    public static class FarmWorldBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/SC_Farm.unity";
        public const string RootName = "WORLD_RushedCrops";
        public const string TerrainPath = "00_Terrain/Walkable valley";
        public const string HillsPath = "00_Terrain/Distant hills";
        public const string BridgePath = "02_River/River bridge";
        public const string FarmGroup = "03_Farm";
        public static readonly string[] Groups = { "00_Terrain", "02_River", FarmGroup, "04_Market", "05_Quarry", "06_LoggingCamp", "07_Forest", "08_Boundary" };

        private const string Pack = "Assets/Holotna/Medieval Rural/Prefabs/";
        private const string Generated = "Assets/_Game/World/";
        private const int GroundLayer = 6;
        private const float HalfWidth = 80f, HalfDepth = 70f;

        // Landmarks.
        private static readonly Vector2 BarnSpot = V(-41, -2);
        private static readonly Vector2 Yard = V(-36, -15);
        private static readonly Vector2 Garden = V(-25, -31);
        private static readonly Vector2 GardenHalfSize = V(11, 9); // whole metres, so the tilled soil edge follows the terrain grid
        private static readonly Vector2 Market = V(4, -6);
        private static readonly Vector2 Logging = V(42, -22);
        private static readonly Vector2 Quarry = V(10, 49);
        private static readonly Vector2 Spawn = V(-31, -13);

        // Flat building pads: centre, radius. Terrain is exactly level inside each radius.
        private static readonly Vector3[] Pads = {
            new Vector3(-36f, -15f, 27f),   // farm: barn, yard and garden
            new Vector3(4f, -6f, 14f),      // market
            new Vector3(42f, -22f, 15f),    // logging camp
            new Vector3(10f, 49f, 15f),     // quarry
        };

        private static readonly Vector2[] RiverGuide = {
            V(-175, 40), V(-120, 30), V(-86, 31), V(-62, 35), V(-40, 29), V(-18, 27), V(6, 24), V(28, 27), V(48, 21), V(66, 25), V(86, 19), V(120, 24), V(175, 14)
        };
        private static readonly Vector2[][] RouteGuides = {
            new[] { V(-33, -14), V(-20, -11), V(-8, -8), V(4, -6) },                             // farm -> market
            new[] { V(4, -6), V(5, 5), V(6, 15), V(6, 24), V(7, 33), V(9, 41), V(10, 47) },      // market -> bridge -> quarry
            new[] { V(4, -6), V(16, -12), V(28, -18), V(42, -22) },                              // market -> logging camp
        };
        private static readonly Vector2[] River = Smooth(RiverGuide);
        private static readonly Vector2[][] Routes = SmoothRoutes(RouteGuides);

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static readonly List<Light> Lanterns = new List<Light>();
        private static System.Random random;
        private static Transform world;

        [MenuItem("Rushed Crops/World/Rebuild map")]
        public static void RebuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Rebuild map", "Delete " + RootName + " and build the farm map again? Unsaved hand edits to the map are lost.", "Rebuild", "Cancel"))
                return;
            Rebuild();
        }

        public static void Rebuild()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play before authoring the map.");
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open SC_Farm before building its map.");

            var oldWorld = GameObject.Find(RootName);
            if (oldWorld != null) Object.DestroyImmediate(oldWorld);
            DeleteGeneratedMeshes();

            random = new System.Random(451);
            Materials.Clear();
            Lanterns.Clear();
            EnsureFolder(Generated + "Meshes");
            EnsureFolder(Generated + "Materials");
            world = new GameObject(RootName).transform;

            MakeMaterials();
            BuildGround(Group("00_Terrain"));
            BuildRiver(Group("02_River"));
            BuildFarm(Group(FarmGroup));
            BuildMarket(Group("04_Market"));
            BuildQuarry(Group("05_Quarry"));
            BuildLogging(Group("06_LoggingCamp"));
            BuildForest(Group("07_Forest"));
            BuildBoundaries(Group("08_Boundary"));

            CartoonWorldStyler.Apply();
            WorldGrounding.Apply();
            PlaceOnTops();
            ConnectEnvironment();

            var oldGround = GameObject.Find("Ground");
            if (oldGround != null) oldGround.SetActive(false);
            var player = GameObject.Find("Player");
            if (player != null)
            {
                player.transform.position = new Vector3(Spawn.x, 1.1f, Spawn.y);
                player.transform.rotation = Quaternion.Euler(0f, 150f, 0f);
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = world.gameObject;
            Debug.Log("Rushed Crops: rebuilt farm map with farm, market, logging camp, quarry and one river bridge.");
        }

        // ---------------------------------------------------------------- materials

        private static void MakeMaterials()
        {
            Mat("Meadow", new Color(0.44f, 0.61f, 0.25f));
            Mat("MeadowLight", new Color(0.51f, 0.66f, 0.28f));
            Mat("MeadowShade", new Color(0.39f, 0.55f, 0.22f));
            Mat("MeadowGold", new Color(0.59f, 0.67f, 0.30f));
            Mat("Path", new Color(0.72f, 0.54f, 0.31f));
            Mat("PathShoulder", new Color(0.61f, 0.49f, 0.27f));
            Mat("Gravel", new Color(0.66f, 0.62f, 0.48f));
            Mat("Tilled", new Color(0.50f, 0.36f, 0.20f));
            Mat("Riverbank", new Color(0.55f, 0.53f, 0.40f));
            Mat("Water", new Color(0.13f, 0.55f, 0.67f));
            Mat("Wood", new Color(0.30f, 0.18f, 0.10f));
            Mat("Plank", new Color(0.49f, 0.31f, 0.17f));
            Mat("CanvasCream", new Color(0.92f, 0.83f, 0.62f));
            Mat("CanvasRed", new Color(0.53f, 0.19f, 0.12f));
            Mat("Moon", new Color(0.76f, 0.84f, 1f), true);
        }

        private static Material Mat(string name, Color colour, bool emissive = false)
        {
            string path = Generated + "Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", null);
            material.color = colour;
            material.SetFloat("_Smoothness", name == "Water" ? 0.65f : 0f);
            material.enableInstancing = true;
            if (emissive) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", colour * 1.4f); }
            EditorUtility.SetDirty(material);
            Materials[name] = material;
            return material;
        }

        // ---------------------------------------------------------------- terrain

        private enum Surface { Meadow, MeadowLight, MeadowShade, MeadowGold, Path, PathShoulder, Gravel, Tilled, Riverbank }

        private static void BuildGround(Transform parent)
        {
            int surfaceCount = Enum.GetValues(typeof(Surface)).Length;
            var vertices = new List<Vector3>();
            var triangles = new List<int>[surfaceCount];
            for (int i = 0; i < surfaceCount; i++) triangles[i] = new List<int>();

            // One-metre faceted grid. Paths, yards and soil are coloured into the ground mesh itself
            // instead of being separate decals that could float above or clip into it.
            int columns = (int)(HalfWidth * 2f), rows = (int)(HalfDepth * 2f);
            var corners = new Surface[columns + 1, rows + 1];
            for (int zi = 0; zi <= rows; zi++)
            for (int xi = 0; xi <= columns; xi++)
                corners[xi, zi] = SurfaceAt(V(xi - HalfWidth, zi - HalfDepth));
            for (int zi = 0; zi < rows; zi++)
            for (int xi = 0; xi < columns; xi++)
            {
                float x = xi - HalfWidth, z = zi - HalfDepth;
                GroundTriangle(vertices, triangles, V(x, z), V(x, z + 1f), V(x + 1f, z), corners[xi, zi], corners[xi, zi + 1], corners[xi + 1, zi]);
                GroundTriangle(vertices, triangles, V(x + 1f, z), V(x, z + 1f), V(x + 1f, z + 1f), corners[xi + 1, zi], corners[xi, zi + 1], corners[xi + 1, zi + 1]);
            }

            var mesh = new Mesh { name = "Farm valley terrain", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = surfaceCount;
            for (int i = 0; i < surfaceCount; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SaveMesh(mesh, "ValleyTerrain");

            var surfaceMaterials = new Material[surfaceCount];
            for (int i = 0; i < surfaceCount; i++) surfaceMaterials[i] = Materials[((Surface)i).ToString()];
            var ground = MeshObject("Walkable valley", parent, mesh, surfaceMaterials);
            ground.layer = GroundLayer;
            ground.AddComponent<MeshCollider>().sharedMesh = mesh;
            BuildDistantHills(parent);
        }

        // Coarse, collider-free hills beyond the map edge so the horizon and the river's exits
        // fade into fog instead of ending at the edge of the world.
        private static void BuildDistantHills(Transform parent)
        {
            const float cell = 5f, reachX = 170f, reachZ = 160f, overlap = 2f;
            var vertices = new List<Vector3>();
            var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
            for (float z = -reachZ; z < reachZ; z += cell)
            for (float x = -reachX; x < reachX; x += cell)
            {
                bool inside = x >= -HalfWidth + overlap && x + cell <= HalfWidth - overlap && z >= -HalfDepth + overlap && z + cell <= HalfDepth - overlap;
                if (inside) continue;
                Vector3 a = Hill(x, z), b = Hill(x + cell, z), c = Hill(x, z + cell), d = Hill(x + cell, z + cell);
                Vector2 centre = V(x + cell * 0.5f, z + cell * 0.5f);
                int surface = Distance(centre, River) < 6f ? 2 : Mathf.PerlinNoise(centre.x * 0.03f + 5f, centre.y * 0.03f) > 0.5f ? 1 : 0;
                AddTriangle(vertices, triangles[surface], a, c, b);
                AddTriangle(vertices, triangles[surface], b, c, d);
            }
            var mesh = new Mesh { name = "Distant hills" };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SaveMesh(mesh, "DistantHills");
            var hills = MeshObject("Distant hills", parent, mesh, new[] { Materials["MeadowShade"], Materials["Meadow"], Materials["Riverbank"] });
            hills.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            hills.AddComponent<MeshCollider>().sharedMesh = mesh; // lets trees beyond the edge be grounded; the player cannot reach it
        }

        // Inside the map edge the hills sit just below the walkable terrain, so the seam is hidden.
        private static Vector3 Hill(float x, float z)
        {
            float edge = Mathf.Max(Mathf.Abs(x) / HalfWidth, Mathf.Abs(z) / HalfDepth);
            float height = Height(x, z) - 0.25f;
            float rise = Mathf.InverseLerp(1f, 1.6f, edge) * (10f + Mathf.PerlinNoise(x * 0.02f, z * 0.02f) * 16f);
            float river = Distance(V(x, z), River);
            height += rise * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(9f, 30f, river));
            return new Vector3(x, height, z);
        }

        // A triangle whose corners lie on different surfaces is split through its edge midpoints and
        // centre, so each corner keeps its own colour and boundaries run smoothly instead of saw-toothed.
        private static void GroundTriangle(List<Vector3> vertices, List<int>[] triangles, Vector2 a, Vector2 b, Vector2 c, Surface sa, Surface sb, Surface sc)
        {
            if (sa == sb && sb == sc)
            {
                AddTriangle(vertices, triangles[(int)sa], P(a), P(b), P(c));
                return;
            }
            Vector2 ab = (a + b) * 0.5f, bc = (b + c) * 0.5f, ca = (c + a) * 0.5f, centre = (a + b + c) / 3f;
            GroundCorner(vertices, triangles[(int)sa], a, ab, centre, ca);
            GroundCorner(vertices, triangles[(int)sb], b, bc, centre, ab);
            GroundCorner(vertices, triangles[(int)sc], c, ca, centre, bc);
        }

        private static void GroundCorner(List<Vector3> vertices, List<int> triangles, Vector2 corner, Vector2 next, Vector2 centre, Vector2 previous)
        {
            AddTriangle(vertices, triangles, P(corner), P(next), P(centre));
            AddTriangle(vertices, triangles, P(corner), P(centre), P(previous));
        }

        private static Surface SurfaceAt(Vector2 p)
        {
            // Low-frequency wobble keeps edges organic without spiky single triangles.
            float wobble = (Mathf.PerlinNoise(p.x * 0.12f + 50f, p.y * 0.12f + 50f) - 0.5f) * 1.4f;
            float river = Distance(p, River);
            if (river < 8f + wobble) return Surface.Riverbank;

            if (Inside(p, Garden, GardenHalfSize)) return Surface.Tilled;
            if (Ellipse(p, Yard + V(-2, 2), 11f, 8f, wobble)) return Surface.Path;
            if (Ellipse(p, Market, 9f, 8f, wobble)) return Surface.Path;
            if (Ellipse(p, Quarry + V(0, 2), 12f, 10f, wobble)) return Surface.Gravel;
            if (Ellipse(p, Logging, 11f, 9f, wobble)) return Surface.PathShoulder;

            float path = PathDistance(p);
            if (path < 1.9f + wobble * 0.4f) return Surface.Path;
            if (path < 2.7f + wobble * 0.6f) return Surface.PathShoulder;

            float shade = Mathf.PerlinNoise((p.x + 190f) * 0.05f, (p.y + 210f) * 0.05f);
            return shade < 0.35f ? Surface.MeadowShade : shade > 0.68f ? Surface.MeadowGold : shade > 0.52f ? Surface.MeadowLight : Surface.Meadow;
        }

        // Gentle meadow undulation, hills along the map rim, flat pads/paths and a carved river channel.
        public static float Height(float x, float z)
        {
            Vector2 p = V(x, z);
            float natural = (Mathf.PerlinNoise((x + 200f) * 0.03f, (z + 200f) * 0.03f) - 0.5f) * 1.6f;
            float edge = Mathf.Max(Mathf.Abs(x) / HalfWidth, Mathf.Abs(z) / HalfDepth);
            natural += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.74f, 1f, edge)) * 9f;

            float level = 0f;
            foreach (Vector3 pad in Pads)
                level = Mathf.Max(level, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(pad.z, pad.z + 9f, Vector2.Distance(p, V(pad.x, pad.y)))));
            level = Mathf.Max(level, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3.5f, 9f, PathDistance(p))));
            float height = Mathf.Lerp(natural, 0f, level);

            float riverDistance = Distance(p, River);
            if (riverDistance < 9f) height = Mathf.Min(height, Mathf.Lerp(-1.8f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3f, 9f, riverDistance))));
            return height;
        }

        // ---------------------------------------------------------------- river

        private static void BuildRiver(Transform parent)
        {
            Ribbon("River water", River, 7.6f, parent, Materials["Water"], -0.65f);

            // A few smooth stones along the banks, never near the bridge or a path.
            for (int i = 4; i < River.Length - 1; i += 7)
            {
                if (Mathf.Abs(River[i].x) > 70f) continue;
                Vector2 direction = (River[i + 1] - River[i]).normalized;
                Vector2 side = V(-direction.y, direction.x) * (i % 2 == 0 ? 1f : -1f);
                Vector2 bank = River[i] + side * R(6.2f, 7.4f);
                if (PathDistance(bank) < 7f) continue;
                Prefab("Rocks/Stone0" + random.Next(1, 4), parent, bank, R(0f, 360f), R(1.0f, 1.8f));
                if (random.NextDouble() < 0.6) Prefab("Vegetation/Cattail01", parent, River[i] + side * 6.5f, R(0f, 360f), R(1.1f, 1.5f));
            }

            Vector2 crossing = RiverGuide[6];
            int index = NearestIndex(River, crossing);
            Vector2 flow = (River[Mathf.Min(index + 1, River.Length - 1)] - River[Mathf.Max(index - 1, 0)]).normalized;
            Bridge(parent, crossing, V(-flow.y, flow.x), "River bridge");
            Lantern(parent, crossing + V(3.4f, -10f), 0f);
            Lantern(parent, crossing + V(-3.4f, 10f), 180f);
        }

        private static void Bridge(Transform parent, Vector2 centre, Vector2 forward, string name)
        {
            Transform bridge = new GameObject(name).transform;
            bridge.SetParent(parent, false);
            bridge.position = new Vector3(centre.x, 0f, centre.y);
            bridge.rotation = Quaternion.LookRotation(new Vector3(forward.x, 0f, forward.y));
            const float halfLength = 9f;
            for (int i = 0; i < 45; i++)
            {
                float z = -halfLength + 0.2f + i * 0.4f;
                float y = Mathf.Sin((z + halfLength) / (halfLength * 2f) * Mathf.PI) * 0.45f - 0.05f;
                LocalBox("Bridge plank", bridge, new Vector3(0f, y, z), new Vector3(3.6f, 0.15f, 0.39f), Materials["Plank"], true);
            }
            foreach (float x in new[] { -1.72f, 1.72f })
            {
                for (float z = -7.5f; z <= 7.5f; z += 2.5f)
                    LocalBox("Timber post", bridge, new Vector3(x, 0.1f, z), new Vector3(0.17f, 2.5f, 0.17f), Materials["Wood"], true);
                LocalBox("Handrail", bridge, new Vector3(x, 1.25f, 0f), new Vector3(0.13f, 0.15f, 15.4f), Materials["Plank"], true);
                LocalBox("Low rail", bridge, new Vector3(x, 0.75f, 0f), new Vector3(0.10f, 0.12f, 15.4f), Materials["Wood"], true);
            }
        }

        // ---------------------------------------------------------------- farm

        private static void BuildFarm(Transform parent)
        {
            // The inherited barn: a few missing roof boards are the repair goal.
            var barn = Prefab("Barn01", parent, BarnSpot, 0f, 1.5f);
            barn.name = "Inherited barn";
            int roof = 0;
            foreach (Transform child in barn.GetComponentsInChildren<Transform>())
                if (child.name.StartsWith("RoofCenter") && ++roof % 7 == 0) child.gameObject.SetActive(false);

            // Repair materials stacked neatly by the east wall, ladder against the wall.
            float eastWall = BarnSpot.x + 3.5f * 1.5f;
            Prefab("Other/Ladder01", parent, V(eastWall + 0.12f, BarnSpot.y + 1f), 90f, 1.6f);
            Transform planks = new GameObject("Repair plank stack").transform;
            planks.SetParent(parent, false);
            planks.position = new Vector3(eastWall + 2.2f, 0f, BarnSpot.y - 3f);
            for (int layer = 0; layer < 3; layer++)
            for (int i = 0; i < 4 - layer; i++)
                LocalBox("Repair plank", planks, new Vector3((i - (3 - layer) * 0.5f) * 0.32f, 0.07f + layer * 0.14f, 0f), new Vector3(0.28f, 0.12f, 2.8f), Materials["Plank"], false);
            Prefab("Wood/FirewoodPile02", parent, V(BarnSpot.x - 6.2f, BarnSpot.y + 2f), 90f, 2.2f);
            Prefab("Containers/Barrel01", parent, V(BarnSpot.x - 6.1f, BarnSpot.y - 3.5f), 0f, 1.5f);
            Prefab("Containers/Barrel01", parent, V(BarnSpot.x - 6.1f, BarnSpot.y - 5f), 40f, 1.5f);
            Prefab("Other/Rack01", parent, V(BarnSpot.x + 3.6f, BarnSpot.y - 8.3f), 0f, 1.1f);
            Prefab("Transport/Wheelbarrow01", parent, V(BarnSpot.x - 4.6f, BarnSpot.y - 10.5f), 150f, 1.5f);
            Lantern(parent, V(Yard.x + 7f, Yard.y + 4f), 270f);

            // Six empty planting plots in a fenced garden, gate facing the farmyard.
            for (int row = 0; row < 2; row++)
            for (int col = 0; col < 3; col++)
            {
                Vector2 spot = Garden + V((col - 1) * 6.6f, (0.5f - row) * 7.8f);
                var patch = Prefab("Patch02", parent, spot, 0f, 1f);
                patch.name = "Planting plot " + (row * 3 + col + 1);
                HideStockCrops(patch.transform);
            }
            Vector2 min = Garden - GardenHalfSize, max = Garden + GardenHalfSize;
            FenceLine(parent, V(min.x, min.y), V(max.x, min.y));
            FenceLine(parent, V(min.x, min.y), V(min.x, max.y));
            FenceLine(parent, V(max.x, min.y), V(max.x, max.y));
            FenceLine(parent, V(min.x, max.y), V(Garden.x - 1.1f, max.y));
            FenceLine(parent, V(Garden.x + 1.1f, max.y), V(max.x, max.y));
            var gate = Prefab("Other/Gate01", parent, V(Garden.x, max.y), 0f, 1.08f);
            gate.name = "Garden gate";
            Prefab("Containers/Bucket01", parent, V(Garden.x + 2.4f, max.y + 1.2f), 0f, 1.3f);
            Prefab("Containers/Crate01", parent, V(Garden.x - 2.6f, max.y + 1.1f), 10f, 1.6f);
            Lantern(parent, V(Garden.x + 1.6f, max.y + 0.6f), 180f);
        }

        [MenuItem("Rushed Crops/World/Clear farming plots")]
        public static void ClearFarmingPlots()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Clear the saved plots outside Play.");
            var garden = GameObject.Find(RootName).transform.Find(FarmGroup);
            foreach (Transform child in garden)
                if (child.name.StartsWith("Planting plot")) HideStockCrops(child);
            EditorSceneManager.MarkSceneDirty(garden.gameObject.scene);
            Debug.Log("Cleared all six farming plots; only empty soil beds remain.");
        }

        private static void HideStockCrops(Transform plot)
        {
            // The imported patch includes planted beets. Disable only those nested crop roots,
            // retaining the soil, original pack asset, and prefab connection.
            foreach (LODGroup lod in plot.GetComponentsInChildren<LODGroup>(true))
            {
                if (!lod.name.Contains("Plant")) continue;
                Undo.RecordObject(lod.gameObject, "Clear stock planting");
                lod.gameObject.SetActive(false);
            }
        }

        // ---------------------------------------------------------------- market

        private static void BuildMarket(Transform parent)
        {
            // Stall on the south edge of the plaza, facing the paths that meet here.
            Transform stall = new GameObject("Merchant stall").transform;
            stall.SetParent(parent, false);
            stall.position = At(Market + V(0, -6.5f));
            stall.rotation = Quaternion.Euler(0f, 180f, 0f);
            for (int i = 0; i < 4; i++)
                LocalBox("Awning post", stall, new Vector3(i % 2 == 0 ? -3f : 3f, 1.5f, i < 2 ? -1.5f : 2f), new Vector3(0.16f, 3.2f, 0.16f), Materials["Wood"], true);
            for (int i = 0; i < 10; i++)
            {
                var panel = LocalBox("Striped market awning", stall, new Vector3(-2.88f + i * 0.64f, 3.15f, 0.25f), new Vector3(0.64f, 0.09f, 4.2f), Materials[i % 2 == 0 ? "CanvasCream" : "CanvasRed"], false);
                panel.transform.localRotation = Quaternion.Euler(-9f, 0f, 0f);
                LocalBox("Awning valance", stall, new Vector3(-2.88f + i * 0.64f, 2.68f, -1.82f), new Vector3(0.64f, 0.4f, 0.07f), Materials[i % 2 == 0 ? "CanvasCream" : "CanvasRed"], false);
            }
            var counter = Prefab("Other/Table01", parent, Market + V(0, -6.2f), 0f, 2.2f);
            counter.name = "Stall counter";
            Prefab("Containers/CrateOpen01", parent, Market + V(-2.2f, -7.6f), 0f, 1.6f);
            Prefab("Containers/CrateOpen02", parent, Market + V(2.2f, -7.6f), 0f, 1.6f);
            Prefab("Containers/Barrel01", parent, Market + V(3.9f, -5.2f), 0f, 1.5f);
            Prefab("Containers/Crate01", parent, Market + V(-3.9f, -5.4f), 15f, 1.6f);

            // Merchant's storehouse, door towards the plaza.
            Vector2 store = Market + V(-9f, 6.5f);
            Prefab("Storage01", parent, store, YawTowards(store, Market), 1.2f);
            Prefab("Transport/Cart01", parent, Market + V(9f, 3f), -110f, 1.35f);
            Lantern(parent, Market + V(-6.5f, -2.5f), 90f);
            Lantern(parent, Market + V(6.5f, -2.5f), 270f);
        }

        // ---------------------------------------------------------------- quarry

        private static void BuildQuarry(Transform parent)
        {
            // A horseshoe of cliff faces open to the south, backed into the rim hills.
            for (int i = 0; i < 7; i++)
            {
                float angle = Mathf.Lerp(15f, 165f, i / 6f) * Mathf.Deg2Rad;
                Vector2 spot = Quarry + V(Mathf.Cos(angle) * 13.5f, Mathf.Sin(angle) * 11.5f + 2f);
                Prefab("Rocks/QuarryFace01", parent, spot, YawTowards(spot, Quarry) + 180f, R(1.6f, 2.0f));
            }
            // Loose boulders at the cliff foot, cut blocks stacked by the cart.
            for (int i = 0; i < 8; i++)
            {
                float angle = R(25f, 155f) * Mathf.Deg2Rad;
                Prefab("Rocks/Stone0" + random.Next(1, 4), parent, Quarry + V(Mathf.Cos(angle) * 9.5f, Mathf.Sin(angle) * 7.5f + 2f), R(0f, 360f), R(1.2f, 2.0f));
            }
            Prefab("Rocks/Rubble01", parent, Quarry + V(-6f, 6f), 30f, 1.4f);
            Prefab("Rocks/Rubble01", parent, Quarry + V(6.5f, 5.5f), 200f, 1.2f);
            for (int layer = 0; layer < 2; layer++)
            for (int i = 0; i < 3 - layer; i++)
            {
                var block = Prefab("Rocks/Block03", parent, Quarry + V(5.2f + (i - (2 - layer) * 0.5f) * 1.1f, -3.5f), 90f, 2f);
                block.name += layer == 0 ? "" : " (stacked)";
                block.transform.position += Vector3.up * (0.24f + layer * 0.44f);
            }
            Prefab("Transport/Cart01", parent, Quarry + V(7f, -6.5f), 160f, 1.5f);
            var pick = Prefab("Tools/Pickaxe01", parent, Quarry + V(-1.5f, -1f), 0f, 1.6f);
            pick.transform.rotation = Quaternion.Euler(0f, 35f, 90f);
            Prefab("Shed01", parent, Quarry + V(-9f, -3f), YawTowards(Quarry + V(-9f, -3f), Quarry) + 180f, 1.2f);
            Lantern(parent, Quarry + V(-3.2f, -7f), 0f);
        }

        // ---------------------------------------------------------------- logging camp

        private static void BuildLogging(Transform parent)
        {
            Vector2 shed = Logging + V(5f, 8f);
            Prefab("Shed01", parent, shed, YawTowards(shed, Logging) + 180f, 1.35f);
            Prefab("Wood/LogPile03", parent, Logging + V(0f, -7.5f), 90f, 2.6f);
            Prefab("Wood/LogPile02", parent, Logging + V(8.5f, -5f), 0f, 2.4f);
            Prefab("Other/Sawbuck01", parent, Logging + V(6.5f, 1f), 90f, 1.9f);
            Prefab("Wood/ChoppingBlock01", parent, Logging + V(4.5f, -2.5f), 0f, 2f);
            Prefab("Wood/FirewoodPile03", parent, shed + V(-3.6f, 0f), 0f, 2.2f);
            Prefab("Transport/Cart01", parent, Logging + V(-4f, 6f), 80f, 1.5f);
            // Stumps where trees have already been felled, at the edge of the clearing.
            for (int i = 0; i < 8; i++)
            {
                float angle = (i * 45f + R(-12f, 12f)) * Mathf.Deg2Rad;
                Vector2 spot = Logging + V(Mathf.Cos(angle), Mathf.Sin(angle)) * R(11.5f, 13.5f);
                if (PathDistance(spot) < 4f) continue;
                Prefab("Vegetation/PineStump0" + random.Next(1, 3), parent, spot, R(0f, 360f), R(1.6f, 2.1f));
            }
            Lantern(parent, Logging + V(-3f, -7.5f), 0f);
        }

        // ---------------------------------------------------------------- forest and meadows

        private static void BuildForest(Transform parent)
        {
            string[] broadleaf = { "Oak01", "Oak02" };
            string[] conifer = { "Pine01", "Pine02", "Spruce01", "Spruce02" };
            // The grid runs a little past the map edge so the rim and the distant hills stay wooded.
            for (float z = -HalfDepth - 12f; z < HalfDepth + 12f; z += 4.6f)
            for (float x = -HalfWidth - 12f; x < HalfWidth + 12f; x += 4.6f)
            {
                Vector2 p = V(x + R(-1.8f, 1.8f), z + R(-1.8f, 1.8f));
                if (Blocked(p, 4f, 5.5f, 8.5f)) continue;
                float edge = Mathf.Max(Mathf.Abs(p.x) / HalfWidth, Mathf.Abs(p.y) / HalfDepth);
                float grove = Mathf.PerlinNoise(p.x * 0.045f + 11f, p.y * 0.045f + 7f);
                float woods = Vector2.Distance(p, Logging) < 32f || p.x > 50f || p.y < -45f ? 0.85f : 0f;
                float chance = edge > 0.78f ? 0.95f : Mathf.Max(woods, grove > 0.58f ? 0.75f : 0.06f);
                if (random.NextDouble() > chance) continue;
                bool pine = edge > 0.78f || p.x > 30f || random.NextDouble() < 0.45;
                string tree = pine ? conifer[random.Next(conifer.Length)] : broadleaf[random.Next(broadleaf.Length)];
                Prefab("Vegetation/" + tree, parent, p, R(0f, 360f), R(1.3f, 2.0f));
            }
            // Willows along the river.
            for (int i = 2; i < River.Length - 1; i += 9)
            {
                if (Mathf.Abs(River[i].x) > 70f) continue;
                Vector2 direction = (River[i + 1] - River[i]).normalized;
                Vector2 bank = River[i] + V(-direction.y, direction.x) * (i % 2 == 0 ? 9.5f : -9.5f);
                if (!Blocked(bank, 3f, 6f, 8.5f)) Prefab("Vegetation/Willow0" + random.Next(1, 3), parent, bank, R(0f, 360f), R(1.2f, 1.5f));
            }
            // A handful of rock groups in the open meadow.
            for (int group = 0; group < 10; group++)
            {
                Vector2 centre = V(R(-70f, 70f), R(-60f, 60f));
                if (Blocked(centre, 6f, 7f, 10f)) continue;
                for (int i = 0; i < 3; i++)
                    Prefab("Rocks/Stone0" + random.Next(1, 4), parent, centre + V(R(-1.6f, 1.6f), R(-1.6f, 1.6f)), R(0f, 360f), R(1.2f, 2.6f) / (i + 1));
            }
            // Low grass and bushes in the meadows; flowers framing the farm and market.
            string[] small = { "Grass01", "Grass02", "Fern01", "Bush01" };
            for (int i = 0; i < 520; i++)
            {
                Vector2 p = V(R(-74f, 74f), R(-64f, 64f));
                if (Blocked(p, 1.5f, 3.4f, 7f)) continue;
                Prefab("Vegetation/" + small[random.Next(small.Length)], parent, p, R(0f, 360f), R(0.8f, 1.4f), false);
            }
            foreach (Vector3 pad in new[] { Pads[0], Pads[1] })
            for (int i = 0; i < 26; i++)
            {
                float angle = R(0f, Mathf.PI * 2f);
                Vector2 p = V(pad.x, pad.y) + V(Mathf.Cos(angle), Mathf.Sin(angle)) * R(pad.z - 3f, pad.z + 4f);
                if (Blocked(p, -1f, 3.2f, 7f) || Vector2.Distance(p, BarnSpot) < 10f) continue;
                Prefab("Vegetation/Flowers0" + random.Next(1, 4), parent, p, R(0f, 360f), R(1f, 1.5f), false);
            }
        }

        // True when a spot is inside a landmark clearing, too near a path, or too near the river.
        private static bool Blocked(Vector2 p, float clearingMargin, float pathMargin, float riverMargin)
        {
            if (PathDistance(p) < pathMargin || Distance(p, River) < riverMargin) return true;
            if (Inside(p, Garden, GardenHalfSize + V(2f, 2f))) return true;
            foreach (Vector3 pad in Pads)
                if (Vector2.Distance(p, V(pad.x, pad.y)) < pad.z - 4f + clearingMargin) return true;
            return false;
        }

        private static void BuildBoundaries(Transform parent)
        {
            foreach (float x in new[] { -HalfWidth + 3f, HalfWidth - 3f })
                Boundary(parent, new Vector3(x, 10f, 0f), new Vector3(1f, 40f, HalfDepth * 2f));
            foreach (float z in new[] { -HalfDepth + 3f, HalfDepth - 3f })
                Boundary(parent, new Vector3(0f, 10f, z), new Vector3(HalfWidth * 2f, 40f, 1f));
        }

        // ---------------------------------------------------------------- finishing

        // Items that rest on other props rather than on the ground.
        private static void PlaceOnTops()
        {
            Transform market = world.Find("04_Market");
            Transform counter = market.Find("Stall counter");
            if (counter != null && Bounds(counter, out Bounds top))
            {
                string[] produce = { "Crops/Carrot01", "Crops/Cabbage01", "Crops/Carrot01", "Crops/Beet01", "Crops/Cabbage01" };
                for (int i = 0; i < produce.Length; i++)
                {
                    var item = Prefab(produce[i], market, V(top.min.x + 0.3f + i * (top.size.x - 0.6f) / (produce.Length - 1), top.center.z), R(0f, 360f), 1.6f);
                    item.name += " (on counter)";
                    if (Bounds(item.transform, out Bounds b)) item.transform.position += Vector3.up * (top.max.y - b.min.y);
                }
            }
            Transform camp = world.Find("06_LoggingCamp");
            foreach (Transform child in camp)
            {
                if (!child.name.StartsWith("ChoppingBlock") || !Bounds(child, out Bounds block)) continue;
                var axe = Prefab("Tools/Axe01", camp, V(block.center.x, block.center.z), 20f, 1.4f);
                axe.name += " (on block)";
                axe.transform.rotation = Quaternion.Euler(0f, 20f, 12f);
                if (Bounds(axe.transform, out Bounds a)) axe.transform.position += Vector3.up * (block.max.y - 0.12f - a.min.y);
            }
        }

        private static void ConnectEnvironment()
        {
            var cycle = Object.FindAnyObjectByType<EnvironmentCycle>();
            if (cycle == null) throw new InvalidOperationException("WORLD_Environment with EnvironmentCycle is missing from SC_Farm.");
            var serialized = new SerializedObject(cycle);
            var lights = serialized.FindProperty("lanterns");
            lights.arraySize = Lanterns.Count;
            for (int i = 0; i < Lanterns.Count; i++) lights.GetArrayElementAtIndex(i).objectReferenceValue = Lanterns[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Lantern(Transform parent, Vector2 position, float yaw)
        {
            Transform post = new GameObject("Dusk lantern").transform;
            post.SetParent(parent, false);
            post.position = At(position);
            post.rotation = Quaternion.Euler(0f, yaw, 0f);
            LocalBox("Lantern post", post, new Vector3(0f, 1.4f, 0f), new Vector3(0.15f, 3.2f, 0.15f), Materials["Wood"], true);
            LocalBox("Hanging beam", post, new Vector3(0f, 2.9f, 0.3f), new Vector3(0.13f, 0.13f, 0.85f), Materials["Plank"], false);
            var model = Prefab("Other/Lantern01", post, Vector2.zero, 0f, 1.5f, false);
            model.transform.localPosition = new Vector3(0f, 2.85f, 0.58f);
            model.transform.localRotation = Quaternion.identity;
            var go = new GameObject("Warm lantern light");
            go.transform.SetParent(post, false);
            go.transform.localPosition = new Vector3(0f, 2.4f, 0.58f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.57f, 0.22f);
            light.range = 10f;
            light.intensity = 3f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            Lanterns.Add(light);
        }

        private static void FenceLine(Transform parent, Vector2 from, Vector2 to)
        {
            Vector2 direction = (to - from).normalized;
            float length = Vector2.Distance(from, to);
            int count = Mathf.Max(1, Mathf.RoundToInt(length / 1.92f));
            float stretch = length / (count * 1.92f);
            for (int i = 0; i < count; i++)
            {
                var fence = Prefab("Other/Fence01", parent, Vector2.Lerp(from, to, (i + 0.5f) / count), -Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, 1f);
                fence.transform.localScale = new Vector3(stretch, 1f, 1f);
            }
        }

        // ---------------------------------------------------------------- helpers

        private static GameObject Prefab(string relative, Transform parent, Vector2 position, float yaw, float scale, bool collision = true)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + relative + ".prefab");
            if (asset == null) throw new InvalidOperationException("Missing Holotna prefab: " + relative);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.position = At(position);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var coloured = AssetDatabase.LoadAssetAtPath<Material>("Assets/Holotna/Medieval Rural/Materials/Colored/" + mats[i].name + ".mat");
                    if (coloured != null) mats[i] = coloured;
                }
                renderer.sharedMaterials = mats;
            }
            foreach (var collider in go.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = collision;
                collider.gameObject.layer = GroundLayer;
            }
            foreach (var light in go.GetComponentsInChildren<Light>(true))
            {
                light.enabled = false;
                light.shadows = LightShadows.None;
                Lanterns.Add(light);
            }
            return go;
        }

        private static void Ribbon(string name, Vector2[] points, float halfWidth, Transform parent, Material material, float height)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 direction = (points[Mathf.Min(i + 1, points.Length - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;
                Vector2 side = V(-direction.y, direction.x) * halfWidth;
                Vector2 nextDirection = (points[Mathf.Min(i + 2, points.Length - 1)] - points[i]).normalized;
                Vector2 nextSide = V(-nextDirection.y, nextDirection.x) * halfWidth;
                Vector3 a = Flat(points[i] - side, height), b = Flat(points[i] + side, height);
                Vector3 c = Flat(points[i + 1] - nextSide, height), d = Flat(points[i + 1] + nextSide, height);
                AddTriangle(vertices, triangles, a, b, c);
                AddTriangle(vertices, triangles, b, d, c);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SaveMesh(mesh, name.Replace(" ", ""));
            MeshObject(name, parent, mesh, new[] { material });
        }

        private static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<Renderer>().sharedMaterials = materials;
            return go;
        }

        private static void SaveMesh(Mesh mesh, string name) { AssetDatabase.CreateAsset(mesh, Generated + "Meshes/" + name + ".asset"); }

        private static void DeleteGeneratedMeshes()
        {
            if (!AssetDatabase.IsValidFolder(Generated + "Meshes")) return;
            foreach (string guid in AssetDatabase.FindAssets("", new[] { Generated + "Meshes" }))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
        }

        private static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }

        private static GameObject LocalBox(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material, bool collision)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (collision) go.layer = GroundLayer;
            else Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static void Boundary(Transform parent, Vector3 position, Vector3 size)
        {
            var go = new GameObject("Map edge collider");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.AddComponent<BoxCollider>().size = size;
        }

        private static bool Bounds(Transform root, out Bounds bounds)
        {
            bool found = false;
            bounds = new Bounds();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        private static Transform Group(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(world, false);
            return go.transform;
        }

        private static Vector2 V(float x, float z) => new Vector2(x, z);
        private static Vector3 P(float x, float z) => new Vector3(x, Height(x, z), z);
        private static Vector3 P(Vector2 p) => new Vector3(p.x, Height(p.x, p.y), p.y);
        private static Vector3 At(Vector2 p) => new Vector3(p.x, Height(p.x, p.y), p.y);
        private static Vector3 Flat(Vector2 p, float height) => new Vector3(p.x, height, p.y);
        private static float R(float min, float max) => min + (float)random.NextDouble() * (max - min);
        private static float YawTowards(Vector2 from, Vector2 to) => Mathf.Atan2(to.x - from.x, to.y - from.y) * Mathf.Rad2Deg;
        private static bool Inside(Vector2 p, Vector2 centre, Vector2 halfSize) => Mathf.Abs(p.x - centre.x) < halfSize.x && Mathf.Abs(p.y - centre.y) < halfSize.y;

        private static bool Ellipse(Vector2 p, Vector2 centre, float radiusX, float radiusZ, float wobble)
        {
            float dx = (p.x - centre.x) / radiusX, dz = (p.y - centre.y) / radiusZ;
            return dx * dx + dz * dz < 1f + wobble * 0.15f;
        }

        private static Vector2[][] SmoothRoutes(Vector2[][] routes)
        {
            var result = new Vector2[routes.Length][];
            for (int i = 0; i < routes.Length; i++) result[i] = Smooth(routes[i]);
            return result;
        }

        // Catmull-Rom curve through the guide points, sampled every two metres or so.
        private static Vector2[] Smooth(Vector2[] points)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 a = points[Mathf.Max(0, i - 1)], b = points[i], c = points[i + 1], d = points[Mathf.Min(points.Length - 1, i + 2)];
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(b, c) / 2f));
                for (int n = 0; n < steps; n++)
                {
                    float t = n / (float)steps;
                    result.Add(0.5f * ((2f * b) + (-a + c) * t + (2f * a - 5f * b + 4f * c - d) * t * t + (-a + 3f * b - 3f * c + d) * t * t * t));
                }
            }
            result.Add(points[points.Length - 1]);
            return result.ToArray();
        }

        private static float PathDistance(Vector2 p)
        {
            float nearest = float.MaxValue;
            foreach (var route in Routes) nearest = Mathf.Min(nearest, Distance(p, route));
            return nearest;
        }

        private static float Distance(Vector2 p, Vector2[] points)
        {
            float nearest = float.MaxValue;
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 segment = points[i + 1] - points[i];
                float t = Mathf.Clamp01(Vector2.Dot(p - points[i], segment) / segment.sqrMagnitude);
                nearest = Mathf.Min(nearest, Vector2.Distance(p, points[i] + segment * t));
            }
            return nearest;
        }

        private static int NearestIndex(Vector2[] points, Vector2 p)
        {
            int best = 0;
            for (int i = 1; i < points.Length; i++)
                if ((points[i] - p).sqrMagnitude < (points[best] - p).sqrMagnitude) best = i;
            return best;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, split));
            AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }
    }
}
